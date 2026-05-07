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
/// Errors out on:
///  - elements with neither x:Name nor literal Content/Text/Header
///    (no stable anchor → would produce ordinal IDs that flake on reorder)
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

    public InstrumentResult InstrumentFile(string path)
    {
        var viewName = Path.GetFileNameWithoutExtension(path);
        XDocument doc;
        try
        {
            doc = XDocument.Load(path, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            return new InstrumentResult(path, 0, 0, new[]
            {
                new Diagnostic(DiagnosticSeverity.Error, "AID100",
                    $"Failed to parse XAML: {ex.Message}", path, 1, 1)
            });
        }

        if (doc.Root == null) return new InstrumentResult(path, 0, 0, Array.Empty<Diagnostic>());

        var synthesizer = new IdSynthesizer(_config);
        var added = 0;
        var skippedAnchorless = 0;
        var diagnostics = new List<Diagnostic>();

        foreach (var element in doc.Descendants())
        {
            // Skip property-elements (<Button.Background>...)
            if (element.Name.LocalName.Contains('.')) continue;

            // Allow-list filter
            if (!_allowList.Contains(element.Name.LocalName)) continue;

            // Skip if already instrumented
            if (HasAutomationId(element)) continue;

            // Skip template-scope (would need TemplatedParent anyway)
            var scope = ScopeClassifier.Classify(element, out _);
            if (scope != Scope.Instance) continue;

            var xName = element.Attribute(XamlNamespaces.XAttrName)?.Value;
            var staticContent = ExtractStaticContent(element);
            var owningProperty = ExtractOwningPropertyName(element);

            // Refuse to instrument anonymous controls — they would get ordinal
            // IDs that flake on reorder. Force the developer to name them.
            if (string.IsNullOrEmpty(xName) && string.IsNullOrEmpty(staticContent))
            {
                var li = (System.Xml.IXmlLineInfo)element;
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    "AID020",
                    $"Cannot instrument <{element.Name.LocalName}>: no x:Name and no literal Content/Text/Header. " +
                    $"Add an x:Name to make it testable, or remove it from instrument allow-list if not QA-relevant.",
                    path, li.LineNumber, li.LinePosition));
                skippedAnchorless++;
                continue;
            }

            var (id, _, _) = synthesizer.Synthesize(viewName, element.Name.LocalName, xName, staticContent, owningProperty);

            // Attach attribute. The attribute lives in no namespace (XAML's attached-property syntax
            // uses the dotted local-name with the default namespace's URI implicit).
            element.SetAttributeValue(XName.Get(AutomationIdLocalName), id);
            added++;
        }

        if (added > 0)
        {
            // Ephemeral mutation: XDocument-roundtrip reformat is acceptable.
            // The output is consumed by the WPF compiler, not committed.
            doc.Save(path, SaveOptions.DisableFormatting);
        }

        return new InstrumentResult(path, added, skippedAnchorless, diagnostics);
    }

    private static bool HasAutomationId(XElement element)
    {
        foreach (var a in element.Attributes())
            if (a.Name.LocalName == AutomationIdLocalName) return true;
        foreach (var c in element.Elements())
            if (c.Name.LocalName == AutomationIdLocalName) return true;
        return false;
    }

    private static string? ExtractStaticContent(XElement element)
    {
        string[] candidates = ["Content", "Text", "Header", "Title", "ToolTip"];
        foreach (var name in candidates)
        {
            var a = element.Attribute(name);
            if (a == null) continue;
            var v = a.Value?.Trim();
            if (string.IsNullOrEmpty(v)) continue;
            if (v.StartsWith('{') && v.EndsWith('}')) continue;
            return v;
        }
        return null;
    }

    private static string? ExtractOwningPropertyName(XElement element)
    {
        var parent = element.Parent;
        if (parent == null) return null;
        var name = parent.Name.LocalName;
        var dot = name.IndexOf('.');
        if (dot < 0) return null;
        return name[(dot + 1)..];
    }
}

public sealed record InstrumentResult(
    string File,
    int Added,
    int SkippedAnchorless,
    IReadOnlyList<Diagnostic> Diagnostics);
