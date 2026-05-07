using System.Xml;
using System.Xml.Linq;
using AxamlIdGen.Models;

namespace AxamlIdGen.Scanning;

public static class AutomationIdParser
{
    private const string AutomationIdLocalName = "AutomationProperties.AutomationId";

    public static (XDocument Doc, List<AutomationIdOccurrence> Occurrences, List<MissingIdSite> MissingSites) Parse(
        string filePath,
        ToolConfig config)
    {
        var viewName = Path.GetFileNameWithoutExtension(filePath);

        var doc = XDocument.Load(filePath, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);

        var occurrences = new List<AutomationIdOccurrence>();
        var missing = new List<MissingIdSite>();

        if (doc.Root == null) return (doc, occurrences, missing);

        foreach (var element in doc.Descendants())
        {
            // Attribute form: <Foo AutomationProperties.AutomationId="..."/>
            var attr = FindAutomationIdAttribute(element);
            // Element form: <Foo><AutomationProperties.AutomationId>...</...></Foo>
            var childElem = FindAutomationIdChildElement(element);

            string? value = attr?.Value ?? childElem?.Value;
            XAttribute? idAttr = attr;

            if (value == null && childElem == null)
            {
                // Possibly missing — only report missing for "interesting" elements: not properties (dotted local name).
                if (!IsPropertyElement(element) && IsLikelyControl(element))
                {
                    var li = (IXmlLineInfo)element;
                    var miscope = ScopeClassifier.Classify(element, out var tt, out _);
                    missing.Add(new MissingIdSite
                    {
                        File = filePath,
                        Line = li.LineNumber,
                        Column = li.LinePosition,
                        Element = element,
                        Scope = miscope,
                        TemplateTargetType = tt,
                        ViewName = viewName,
                        XName = element.Attribute(XamlNamespaces.XAttrName)?.Value
                    });
                }
                continue;
            }

            var lineInfo = (IXmlLineInfo)(attr as IXmlLineInfo ?? childElem!);
            var scope = ScopeClassifier.Classify(element, out var tplTarget, out var scopeKey);

            OccurrenceState state;
            if (string.IsNullOrEmpty(value)) state = OccurrenceState.Sentinel;
            else if (value == config.SentinelValue) state = OccurrenceState.Sentinel;
            else state = OccurrenceState.Explicit;

            occurrences.Add(new AutomationIdOccurrence
            {
                File = filePath,
                Line = lineInfo.LineNumber,
                Column = lineInfo.LinePosition,
                ControlElementName = element.Name.LocalName,
                XName = element.Attribute(XamlNamespaces.XAttrName)?.Value,
                StaticContent = ExtractStaticContent(element),
                OwningPropertyName = ExtractOwningPropertyName(element),
                Scope = scope,
                TemplateTargetType = tplTarget,
                TemplateScopeKey = scopeKey,
                State = state,
                OriginalValue = value,
                FinalId = state == OccurrenceState.Explicit ? value : null,
                Provenance = state == OccurrenceState.Explicit
                    ? ProvenanceSource.Explicit
                    : ProvenanceSource.Ordinal,
                Element = element,
                IdAttribute = idAttr,
                ViewName = viewName
            });
        }

        return (doc, occurrences, missing);
    }

    private static XAttribute? FindAutomationIdAttribute(XElement element)
    {
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.LocalName == AutomationIdLocalName)
                return attr;
        }
        return null;
    }

    private static XElement? FindAutomationIdChildElement(XElement element)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName == AutomationIdLocalName)
                return child;
        }
        return null;
    }

    private static bool IsPropertyElement(XElement element)
    {
        // XAML property-element syntax: <Button.Background>...</Button.Background>
        return element.Name.LocalName.Contains('.');
    }

    private static bool IsLikelyControl(XElement element)
    {
        // Heuristic: elements directly inside a layout/container with a default xmlns,
        // and not property-elements. We don't try to be exhaustive; this only governs
        // missing-id reporting for "synthesize" policy.
        return !IsPropertyElement(element);
    }

    private static string? ExtractStaticContent(XElement element)
    {
        // Attributes likely to contain user-visible static text
        string[] candidates = ["Content", "Text", "Header", "Title", "ToolTip"];
        foreach (var name in candidates)
        {
            var a = element.Attribute(name);
            if (a == null) continue;
            var v = a.Value?.Trim();
            if (string.IsNullOrEmpty(v)) continue;
            // Skip bindings/markup extensions
            if (v.StartsWith('{') && v.EndsWith('}')) continue;
            return v;
        }
        return null;
    }

    private static string? ExtractOwningPropertyName(XElement element)
    {
        // If the element is the value of a property-element (e.g., <Button.Icon><Path .../></Button.Icon>),
        // the parent's local name will be like "Button.Icon".
        var parent = element.Parent;
        if (parent == null) return null;
        var name = parent.Name.LocalName;
        var dot = name.IndexOf('.');
        if (dot < 0) return null;
        return name[(dot + 1)..];
    }
}

public sealed class MissingIdSite
{
    public required string File { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required XElement Element { get; init; }
    public required Scope Scope { get; init; }
    public string? TemplateTargetType { get; init; }
    public string ViewName { get; init; } = "";
    public string? XName { get; init; }
}
