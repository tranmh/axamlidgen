using AxamlIdGen.Cli;
using AxamlIdGen.Models;

if (args.Length == 0)
{
    PrintUsage();
    return 64;
}

string command = args[0];
string? root = null;
string? configPath = null;

for (int i = 1; i < args.Length; i++)
{
    var a = args[i];
    if (a == "--config" && i + 1 < args.Length) configPath = args[++i];
    else if (a == "--help" || a == "-h") { PrintUsage(); return 0; }
    else if (root == null) root = a;
    else { Console.Error.WriteLine($"Unexpected argument: {a}"); return 64; }
}

if (root == null)
{
    Console.Error.WriteLine("Missing <root> argument.");
    PrintUsage();
    return 64;
}

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Root directory does not exist: {root}");
    return 2;
}

if (configPath == null)
{
    var defaultPath = Path.Combine(root, "axamlidgen.config.json");
    if (File.Exists(defaultPath)) configPath = defaultPath;
    else
    {
        var rootCandidate = Path.Combine(Directory.GetCurrentDirectory(), "axamlidgen.config.json");
        if (File.Exists(rootCandidate)) configPath = rootCandidate;
    }
}

ToolConfig config;
try
{
    config = ToolConfig.LoadOrDefault(configPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load config: {ex.Message}");
    return 2;
}

var repoRoot = configPath != null
    ? Path.GetDirectoryName(Path.GetFullPath(configPath))!
    : Directory.GetCurrentDirectory();

try
{
    return command switch
    {
        "scan"       => RunScan(root, config),
        "check"      => RunCheck(root, repoRoot, config),
        "write"      => RunWrite(root, repoRoot, config),
        "emit"       => RunEmit(root, repoRoot, config),
        "instrument" => RunInstrument(root, repoRoot, config),
        _ => UnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unhandled error: {ex.Message}");
    return 2;
}

static int RunScan(string root, ToolConfig config)
{
    var result = Pipeline.Run(root, config, writeBack: false);
    PrintSummary(result, "scan");
    PrintDiagnostics(result);
    return ExitCodeForDiagnostics(result);
}

static int RunCheck(string root, string repoRoot, ToolConfig config)
{
    var result = Pipeline.Run(root, config, writeBack: false);
    PrintSummary(result, "check");
    PrintDiagnostics(result);
    var errCode = ExitCodeForDiagnostics(result);
    if (errCode != 0) return errCode;

    var (jsonChanged, csChanged, tsChanged) = CompareManifests(result, repoRoot, config);
    if (jsonChanged || csChanged || tsChanged)
    {
        Console.Error.WriteLine("ERROR: Generated manifests are out of date. Run `AxamlIdGen write` or `emit`.");
        if (jsonChanged) Console.Error.WriteLine($"  - {config.Outputs.Json}");
        if (csChanged)   Console.Error.WriteLine($"  - {config.Outputs.CSharp}");
        if (tsChanged)   Console.Error.WriteLine($"  - {config.Outputs.TypeScript}");
        return 3;
    }
    return 0;
}

static int RunWrite(string root, string repoRoot, ToolConfig config)
{
    var result = Pipeline.Run(root, config, writeBack: true);
    PrintSummary(result, "write");
    PrintDiagnostics(result);
    var errCode = ExitCodeForDiagnostics(result);
    if (errCode != 0) return errCode;

    var changed = Pipeline.FlushManifests(result, repoRoot, config.Outputs);
    Console.WriteLine($"Manifests written: json={changed.Json} cs={changed.CSharp} ts={changed.Ts}");
    return 0;
}

static int RunInstrument(string root, string repoRoot, ToolConfig config)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var instrumenter = new AxamlIdGen.Synthesis.Instrumenter(config);

    var files = AxamlIdGen.Scanning.XamlScanner
        .EnumerateFiles(root, config.IncludeGlobs, config.ExcludeGlobs)
        .ToList();

    int totalAdded = 0;
    int totalRefused = 0;
    int filesModified = 0;
    var perTierTotals = new int[8];
    var allDiagnostics = new List<AxamlIdGen.Models.Diagnostic>();

    // Shared pool of taken IDs across all files — enforces global uniqueness.
    // When two files would synthesize the same ID, the second one gets a
    // collision-suffix (_2, _3, ...) deterministically by file order
    // (XamlScanner already sorts lexicographically for reproducibility).
    var globalTaken = new HashSet<string>(StringComparer.Ordinal);

    foreach (var file in files)
    {
        var r = instrumenter.InstrumentFile(file, globalTaken);
        if (r.Added > 0) filesModified++;
        totalAdded += r.Added;
        totalRefused += r.Refused;
        for (int i = 0; i < r.PerTierCounts.Length && i < perTierTotals.Length; i++)
            perTierTotals[i] += r.PerTierCounts[i];
        allDiagnostics.AddRange(r.Diagnostics);
    }

    sw.Stop();

    foreach (var d in allDiagnostics.OrderBy(d => d.File).ThenBy(d => d.Line))
    {
        var sev = d.Severity == DiagnosticSeverity.Error ? "error" : "warning";
        Console.Error.WriteLine($"{d.File}:{d.Line}:{d.Column}: {sev} {d.Code}: {d.Message}");
    }

    Console.WriteLine($"AxamlIdGen instrument: {files.Count} files, +{totalAdded} ids in {filesModified} files, {totalRefused} refused, {sw.ElapsedMilliseconds}ms (cutoff={config.AnchorTierCutoff})");
    PrintTierTable(perTierTotals, totalAdded, totalRefused);

    if (allDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        return 1;

    // Re-run pipeline to emit fresh manifests reflecting the new IDs.
    var result = Pipeline.Run(root, config, writeBack: false);
    if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
    {
        foreach (var d in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            Console.Error.WriteLine($"{d.File}:{d.Line}:{d.Column}: error {d.Code}: {d.Message}");
        return 1;
    }
    var changed = Pipeline.FlushManifests(result, repoRoot, config.Outputs);
    Console.WriteLine($"Manifests written: json={changed.Json} cs={changed.CSharp} ts={changed.Ts}");
    return 0;
}

static int RunEmit(string root, string repoRoot, ToolConfig config)
{
    var result = Pipeline.Run(root, config, writeBack: false);
    var errCode = ExitCodeForDiagnostics(result);
    if (errCode != 0)
    {
        PrintDiagnostics(result);
        return errCode;
    }
    var changed = Pipeline.FlushManifests(result, repoRoot, config.Outputs);
    Console.WriteLine($"Manifests written: json={changed.Json} cs={changed.CSharp} ts={changed.Ts}");
    return 0;
}

static int ExitCodeForDiagnostics(PipelineResult result)
{
    if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) return 1;
    return 0;
}

static (bool, bool, bool) CompareManifests(PipelineResult result, string root, ToolConfig config)
{
    string Resolve(string p) => Path.IsPathRooted(p) ? p : Path.Combine(root, p);
    bool Diff(string path, string expected) =>
        !File.Exists(path) || File.ReadAllText(path) != expected;
    return (
        Diff(Resolve(config.Outputs.Json), result.JsonManifest),
        Diff(Resolve(config.Outputs.CSharp), result.CSharpManifest),
        Diff(Resolve(config.Outputs.TypeScript), result.TypeScriptManifest)
    );
}

static void PrintTierTable(int[] perTierTotals, int totalAdded, int totalRefused)
{
    // (tier => (label, stability))
    (string Source, string Stability)[] tiers =
    {
        ("(refused)",      "n/a"),
        ("x:Name",         "stable (semantic)"),
        ("Content",        "stable (semantic)"),
        ("PropertyName",   "stable (semantic)"),
        ("Binding",        "stable (semantic)"),
        ("GridPosition",   "stable (layout)"),
        ("SubtreeOrdinal", "bounded (ordinal in subtree)"),
        ("Hash",           "fingerprint (own attrs)"),
    };

    Console.WriteLine();
    Console.WriteLine("  Tier   Source              Count        %  Stability");
    Console.WriteLine("  ─────  ────────────────  ───────  ───────  ────────────────────────────");

    int activeTotal = totalAdded > 0 ? totalAdded : 1; // avoid div/0
    for (int t = 1; t < perTierTotals.Length; t++)
    {
        int n = perTierTotals[t];
        double pct = 100.0 * n / activeTotal;
        Console.WriteLine($"  {t,5}  {tiers[t].Source,-16}  {n,7:N0}  {pct,6:F1}%  {tiers[t].Stability}");
    }
    Console.WriteLine("  ─────  ────────────────  ───────");
    Console.WriteLine($"         Total             {totalAdded,7:N0}");
    Console.WriteLine($"         Refused           {totalRefused,7:N0}");

    // Roll up by stability category
    int stable = perTierTotals[1] + perTierTotals[2] + perTierTotals[3] + perTierTotals[4] + perTierTotals[5];
    int bounded = perTierTotals[6];
    int hash = perTierTotals[7];
    Console.WriteLine();
    Console.WriteLine("  Stability distribution:");
    PrintRollup("Stable      (tiers 1-5)", stable, activeTotal);
    PrintRollup("Bounded     (tier 6)   ", bounded, activeTotal);
    PrintRollup("Fingerprint (tier 7)   ", hash, activeTotal);
    PrintRollup("Refused               ", totalRefused, activeTotal + totalRefused);
}

static void PrintRollup(string label, int n, int total)
{
    double pct = total == 0 ? 0 : 100.0 * n / total;
    Console.WriteLine($"    {label}: {n,7:N0}  ({pct,5:F1}%)");
}

static void PrintSummary(PipelineResult result, string command)
{
    Console.WriteLine($"AxamlIdGen {command}: {result.FilesScanned} files, {result.Occurrences.Count} ids, {result.Synthesized} synthesized, {result.Duration.TotalMilliseconds:F0}ms");
}

static void PrintDiagnostics(PipelineResult result)
{
    foreach (var d in result.Diagnostics.OrderBy(d => d.File).ThenBy(d => d.Line))
    {
        var severity = d.Severity == DiagnosticSeverity.Error ? "error" : "warning";
        Console.Error.WriteLine($"{d.File}:{d.Line}:{d.Column}: {severity} {d.Code}: {d.Message}");
    }
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintUsage();
    return 64;
}

static void PrintUsage()
{
    Console.WriteLine("AxamlIdGen — XAML AutomationId codegen tool");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  AxamlIdGen scan   <root> [--config <path>]   Read-only scan; prints diagnostics.");
    Console.WriteLine("  AxamlIdGen check  <root> [--config <path>]   Like scan + assert manifests up to date.");
    Console.WriteLine("  AxamlIdGen write  <root> [--config <path>]   Replace sentinels in XAML, regenerate manifests.");
    Console.WriteLine("  AxamlIdGen emit   <root> [--config <path>]   Regenerate manifests only.");
    Console.WriteLine("  AxamlIdGen instrument <root> [--config <path>]");
    Console.WriteLine("                                               Inject AutomationIds on every allow-listed control");
    Console.WriteLine("                                               that lacks one. Mutates XAML in place. Intended for");
    Console.WriteLine("                                               build-time use against a sandbox copy of the source.");
    Console.WriteLine();
    Console.WriteLine("Exit codes: 0 clean | 1 validation errors | 2 IO/parse errors | 3 manifests stale.");
}
