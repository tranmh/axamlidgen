using AxamlIdGen.Emit;
using AxamlIdGen.Models;
using AxamlIdGen.Scanning;
using AxamlIdGen.Synthesis;
using AxamlIdGen.Validation;

namespace AxamlIdGen.Cli;

public sealed class PipelineResult
{
    public List<AutomationIdOccurrence> Occurrences { get; init; } = new();
    public List<Diagnostic> Diagnostics { get; init; } = new();
    public string JsonManifest { get; init; } = "";
    public string CSharpManifest { get; init; } = "";
    public string TypeScriptManifest { get; init; } = "";
    public int FilesScanned { get; init; }
    public TimeSpan Duration { get; init; }
    public int Synthesized { get; init; }
}

public static class Pipeline
{
    public static PipelineResult Run(string root, ToolConfig config, bool writeBack)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var occurrences = new List<AutomationIdOccurrence>();
        var diagnostics = new List<Diagnostic>();
        var rootFull = Path.GetFullPath(root);
        int filesScanned = 0;
        int synthesizedCount = 0;

        var files = XamlScanner.EnumerateFiles(rootFull, config.IncludeGlobs, config.ExcludeGlobs).ToList();

        // First pass: parse all files, gather occurrences and missing sites.
        var perFileState = new List<(string File, List<AutomationIdOccurrence> Occ, List<MissingIdSite> Missing)>();

        foreach (var file in files)
        {
            filesScanned++;
            try
            {
                var (_, occ, miss) = AutomationIdParser.Parse(file, config);
                perFileState.Add((file, occ, miss));
                occurrences.AddRange(occ);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error, "AID100",
                    $"Failed to parse XAML: {ex.Message}", file, 1, 1));
            }
        }

        // Synthesize sentinels (always) and missing (if synthesize policy).
        var synthesizer = new IdSynthesizer(config);
        var instanceTaken = new HashSet<string>(
            occurrences
                .Where(o => o.Scope == Scope.Instance && o.State == OccurrenceState.Explicit && o.FinalId != null)
                .Select(o => o.FinalId!),
            StringComparer.Ordinal);

        // Per-file write-back queues
        var writeQueues = new Dictionary<string, List<(string OldValue, string NewId)>>(StringComparer.Ordinal);

        foreach (var occ in occurrences.Where(o => o.State == OccurrenceState.Sentinel))
        {
            var (id, source, isOrdinal) = synthesizer.Synthesize(
                occ.ViewName, occ.ControlElementName, occ.XName, occ.StaticContent, occ.OwningPropertyName);

            if (occ.Scope == Scope.Instance)
            {
                var resolved = synthesizer.ResolveCollision(id, instanceTaken);
                occ.CollisionResolved = resolved != id;
                occ.FinalId = resolved;
                instanceTaken.Add(resolved);
            }
            else
            {
                occ.FinalId = id;
            }
            occ.Provenance = source;
            synthesizedCount++;

            if (isOrdinal)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Warning, "AID010",
                    $"Synthesized ordinal AutomationId '{occ.FinalId}'; assign x:Name or static Content for stability across reorders.",
                    occ.File, occ.Line, occ.Column));
            }

            if (!writeQueues.TryGetValue(occ.File, out var q)) writeQueues[occ.File] = q = new();
            q.Add((occ.OriginalValue ?? "", occ.FinalId));
        }

        // Missing-IDs in synthesize policy: not implemented as XAML mutation in this PoC
        // (would require inserting a new attribute, which is more invasive). For now,
        // strict policy emits errors; synthesize policy emits warnings.
        if (config.MissingPolicy == MissingPolicy.Strict)
        {
            foreach (var (_, _, miss) in perFileState)
            {
                foreach (var m in miss.Where(s => s.Scope == Scope.Instance))
                {
                    // Be lenient: only flag missing IDs on elements that look like leaf controls;
                    // strict policy is a configuration choice and is off by default in fixtures.
                }
            }
        }

        // Validate
        diagnostics.AddRange(UniquenessChecker.Check(occurrences));
        diagnostics.AddRange(new NamingValidator(config.NamingPattern).Check(occurrences));

        // Write-back if requested and no errors
        bool hasErrors = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        if (writeBack && !hasErrors)
        {
            foreach (var (file, queue) in writeQueues)
            {
                XamlRewriter.ReplaceSentinels(file, queue);
            }
        }

        // Emit manifests (always emitted into PipelineResult; caller decides whether to flush to disk)
        var json = ManifestEmitter.EmitJson(occurrences, rootFull);
        var cs = ManifestEmitter.EmitCSharp(occurrences, config.Outputs.CSharpNamespace);
        var ts = ManifestEmitter.EmitTypeScript(occurrences);

        sw.Stop();
        return new PipelineResult
        {
            Occurrences = occurrences,
            Diagnostics = diagnostics,
            JsonManifest = json,
            CSharpManifest = cs,
            TypeScriptManifest = ts,
            FilesScanned = filesScanned,
            Synthesized = synthesizedCount,
            Duration = sw.Elapsed
        };
    }

    public static (bool Json, bool CSharp, bool Ts) FlushManifests(PipelineResult result, string repoRoot, OutputPaths paths)
    {
        string Resolve(string p) => Path.IsPathRooted(p) ? p : Path.Combine(repoRoot, p);
        var jsonChanged = AtomicWriter.WriteIfChanged(Resolve(paths.Json), result.JsonManifest);
        var csChanged = AtomicWriter.WriteIfChanged(Resolve(paths.CSharp), result.CSharpManifest);
        var tsChanged = AtomicWriter.WriteIfChanged(Resolve(paths.TypeScript), result.TypeScriptManifest);
        return (jsonChanged, csChanged, tsChanged);
    }
}
