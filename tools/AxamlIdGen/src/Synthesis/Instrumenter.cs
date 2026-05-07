using System.Xml.Linq;
using AxamlIdGen.Models;
using AxamlIdGen.Scanning;

namespace AxamlIdGen.Synthesis;

/// <summary>
/// Mutates XAML files in-place by injecting AutomationProperties.AutomationId
/// onto every element matching <see cref="ToolConfig.InstrumentControlTypes"/>
/// that does not already have one. Intended for *ephemeral* build-time use:
/// run once before compilation, never commit the output.
///
/// Skips:
///  - elements inside ControlTemplate / DataTemplate / Style.Setter (template scope)
///  - elements that already have AutomationProperties.AutomationId
///  - property-elements (e.g. Button.Background)
///
/// When <see cref="ToolConfig.AnchorTierCutoff"/> &lt; 4, refuses anchorless
/// controls with AID020. With cutoff &gt;= 4, always synthesizes via tiers
/// 4–7 (binding / grid position / subtree ordinal / hash).
/// </summary>
public sealed class Instrumenter
{
    private const string AutomationIdLocalName = "AutomationProperties.AutomationId";

    private readonly ToolConfig _config;
    private readonly HashSet<string> _allowList;

    public Instrumenter(ToolConfig config)
    {
        _config = config;
        _allowList = new HashSet<string>(config.InstrumentControlTypes, StringComparer.Ordinal);
    }

    public InstrumentResult InstrumentFile(string path) => InstrumentFile(path, sharedTaken: null);

    /// <summary>
    /// Instrument a single XAML file. <paramref name="sharedTaken"/> is a
    /// cross-file pool of IDs already in use; pass the same instance across
    /// successive files in a build to guarantee global uniqueness.
    /// </summary>
    public InstrumentResult InstrumentFile(string path, HashSet<string>? sharedTaken)
    {
        var viewName = Path.GetFileNameWithoutExtension(path);
        XDocument doc;
        try
        {
            doc = XDocument.Load(path, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            return new InstrumentResult(path, 0, 0, new int[8], new[]
            {
                new Diagnostic(DiagnosticSeverity.Error, "AID100",
                    $"Failed to parse XAML: {ex.Message}", path, 1, 1)
            });
        }

        if (doc.Root == null) return new InstrumentResult(path, 0, 0, new int[8], Array.Empty<Diagnostic>());

        var synthesizer = new IdSynthesizer(_config);
        var added = 0;
        var refused = 0;
        var perTierCounts = new int[8];
        var diagnostics = new List<Diagnostic>();

        // Build a "taken" set that already contains every explicit ID in this file
        // (so collision resolution doesn't clash with what's already there).
        var taken = sharedTaken ?? new HashSet<string>(StringComparer.Ordinal);
        foreach (var existing in doc.Descendants())
        {
            foreach (var attr in existing.Attributes())
                if (attr.Name.LocalName == AutomationIdLocalName)
                    taken.Add(attr.Value);
        }

        foreach (var element in doc.Descendants())
        {
            if (element.Name.LocalName.Contains('.')) continue;
            if (!_allowList.Contains(element.Name.LocalName)) continue;
            if (HasAutomationId(element)) continue;

            var scope = ScopeClassifier.Classify(element, out _);
            if (scope != Scope.Instance) continue;

            var (id, source, tier) = synthesizer.Synthesize(viewName, element, _config.AnchorTierCutoff);

            if (tier == 0)
            {
                var li = (System.Xml.IXmlLineInfo)element;
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    "AID020",
                    $"Cannot instrument <{element.Name.LocalName}>: no x:Name and no literal Content/Text/Header. " +
                    $"Add an x:Name to make it testable, raise anchorTierCutoff to 4+, or remove from instrument allow-list.",
                    path, li.LineNumber, li.LinePosition));
                refused++;
                continue;
            }

            // Resolve any collision with already-taken IDs (in this file or pool).
            var resolved = synthesizer.ResolveCollision(id, taken);
            taken.Add(resolved);

            element.SetAttributeValue(XName.Get(AutomationIdLocalName), resolved);
            added++;
            perTierCounts[tier]++;
        }

        if (added > 0)
        {
            doc.Save(path, SaveOptions.DisableFormatting);
        }

        return new InstrumentResult(path, added, refused, perTierCounts, diagnostics);
    }

    private static bool HasAutomationId(XElement element)
    {
        foreach (var a in element.Attributes())
            if (a.Name.LocalName == AutomationIdLocalName) return true;
        foreach (var c in element.Elements())
            if (c.Name.LocalName == AutomationIdLocalName) return true;
        return false;
    }
}

public sealed record InstrumentResult(
    string File,
    int Added,
    int Refused,
    int[] PerTierCounts,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Backwards-compatible alias used by older tests.</summary>
    public int SkippedAnchorless => Refused;
}
