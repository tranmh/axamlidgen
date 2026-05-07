using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AxamlIdGen.Models;
using AxamlIdGen.Scanning;

namespace AxamlIdGen.Synthesis;

public sealed class IdSynthesizer
{
    private readonly ToolConfig _config;
    private readonly Dictionary<(string view, string controlType), int> _ordinals = new();

    private static readonly Regex BindingPathRegex = new(
        @"\{Binding\s+(?:Path\s*=\s*)?([A-Za-z_][\w.]*)",
        RegexOptions.Compiled);

    // Bindings to consider, in priority order. First binding present wins.
    private static readonly string[] BindingAttributePriority =
    {
        "Command", "Text", "SelectedItem", "Value", "Header", "ItemsSource", "Content", "Source"
    };

    public IdSynthesizer(ToolConfig config) { _config = config; }

    /// <summary>
    /// Legacy string-input overload retained for unit tests. Tiers 4–7 are
    /// not exercised through this path; callers that have an XElement should
    /// use the element-aware overload instead.
    /// </summary>
    public (string Id, ProvenanceSource Source, bool IsOrdinal) Synthesize(
        string viewName,
        string controlType,
        string? xName,
        string? staticContent,
        string? owningPropertyName)
    {
        viewName = Slugifier.Slugify(viewName, _config.MaxIdLength) ?? "View";
        controlType = StripPrefix(controlType);

        var slug = TryNamedAnchor(xName, staticContent, owningPropertyName, out var src);
        if (slug != null) return (Build(viewName, controlType, slug), src, false);

        // Legacy ordinal — for callers that don't have an element to inspect.
        var key = (viewName, controlType);
        var n = _ordinals.TryGetValue(key, out var v) ? v + 1 : 1;
        _ordinals[key] = n;
        return (Build(viewName, controlType, n.ToString()), ProvenanceSource.Ordinal, true);
    }

    /// <summary>
    /// Element-aware synthesis. Walks tiers 1–<paramref name="cutoff"/>;
    /// returns the first non-empty slug it can derive.
    /// Tier=0 means "refused" (cutoff blocked all viable tiers).
    /// </summary>
    public (string Id, ProvenanceSource Source, int Tier) Synthesize(
        string viewName,
        XElement element,
        int cutoff)
    {
        viewName = Slugifier.Slugify(viewName, _config.MaxIdLength) ?? "View";
        var controlType = StripPrefix(element.Name.LocalName);

        // Tier 1
        var xName = element.Attribute(XamlNamespaces.XAttrName)?.Value;
        if (cutoff >= 1)
        {
            var s = Slugifier.Slugify(xName, _config.MaxIdLength);
            if (s != null) return (Build(viewName, controlType, s), ProvenanceSource.XName, 1);
        }

        // Tier 2
        if (cutoff >= 2)
        {
            var s = Slugifier.Slugify(ExtractStaticContent(element), _config.MaxIdLength);
            if (s != null) return (Build(viewName, controlType, s), ProvenanceSource.Content, 2);
        }

        // Tier 3
        if (cutoff >= 3)
        {
            var s = Slugifier.Slugify(ExtractOwningPropertyName(element), _config.MaxIdLength);
            if (s != null) return (Build(viewName, controlType, s), ProvenanceSource.PropertyName, 3);
        }

        // Tier 4 — binding path
        if (cutoff >= 4)
        {
            var s = ExtractBindingPath(element);
            if (s != null) return (Build(viewName, controlType, s), ProvenanceSource.Binding, 4);
        }

        // Tier 5 — Grid.Row/Column under nearest named ancestor
        if (cutoff >= 5)
        {
            var s = ExtractGridPositionSlug(element);
            if (s != null) return (Build(viewName, controlType, s), ProvenanceSource.GridPosition, 5);
        }

        // Tier 6 — subtree ordinal under nearest named ancestor (or view root)
        if (cutoff >= 6)
        {
            var s = ExtractSubtreeOrdinalSlug(element, viewName);
            if (s != null) return (Build(viewName, controlType, s), ProvenanceSource.SubtreeOrdinal, 6);
        }

        // Tier 7 — content fingerprint hash
        if (cutoff >= 7)
        {
            var s = ExtractHashSlug(element);
            return (Build(viewName, controlType, s), ProvenanceSource.Hash, 7);
        }

        // Cutoff blocked all viable tiers.
        return ("", ProvenanceSource.Ordinal, 0);
    }

    public string ResolveCollision(string baseId, ISet<string> taken)
    {
        if (!taken.Contains(baseId)) return baseId;
        // Letter-led suffix so the resulting ID still satisfies a naming
        // pattern that requires every underscore-separated segment to begin
        // with [A-Za-z]. _N2, _N3, ... rather than _2, _3.
        for (int i = 2; i < int.MaxValue; i++)
        {
            var candidate = $"{baseId}_N{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("Exhausted collision suffix range.");
    }

    // ---------- Tier helpers ----------

    private string? TryNamedAnchor(string? xName, string? content, string? property, out ProvenanceSource src)
    {
        var s = Slugifier.Slugify(xName, _config.MaxIdLength);
        if (s != null) { src = ProvenanceSource.XName; return s; }
        s = Slugifier.Slugify(content, _config.MaxIdLength);
        if (s != null) { src = ProvenanceSource.Content; return s; }
        s = Slugifier.Slugify(property, _config.MaxIdLength);
        if (s != null) { src = ProvenanceSource.PropertyName; return s; }
        src = ProvenanceSource.Ordinal;
        return null;
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

    private string? ExtractBindingPath(XElement element)
    {
        foreach (var attr in BindingAttributePriority)
        {
            var a = element.Attribute(attr);
            if (a == null) continue;
            var m = BindingPathRegex.Match(a.Value);
            if (!m.Success) continue;
            var path = m.Groups[1].Value;
            if (string.IsNullOrEmpty(path)) continue;
            // Replace dots in property paths with underscores (e.g., User.Name → User_Name).
            var normalized = path.Replace('.', '_');
            var slug = Slugifier.Slugify(normalized, _config.MaxIdLength);
            if (slug != null) return slug;
        }
        return null;
    }

    private string? ExtractGridPositionSlug(XElement element)
    {
        var row = element.Attribute("Grid.Row")?.Value;
        var col = element.Attribute("Grid.Column")?.Value;
        if (row == null && col == null) return null;

        var ancestor = NearestNamedAncestor(element);
        if (ancestor == null) return null;
        var ancestorName = Slugifier.Slugify(ancestor.Attribute(XamlNamespaces.XAttrName)?.Value, _config.MaxIdLength);
        if (ancestorName == null) return null;

        var sb = new StringBuilder(ancestorName);
        if (row != null) sb.Append('_').Append('R').Append(SanitizeNumeric(row));
        if (col != null) sb.Append((row != null ? "" : "_")).Append('C').Append(SanitizeNumeric(col));
        return sb.ToString();
    }

    private string? ExtractSubtreeOrdinalSlug(XElement element, string viewName)
    {
        // Tier 6 fires only when a real named ancestor exists. If nothing
        // closer than the document root is named, fall through to Tier 7
        // (hash) which is more stable for truly anonymous subtrees.
        var ancestor = NearestNamedAncestor(element);
        if (ancestor == null) return null;

        var anchorName = Slugifier.Slugify(ancestor.Attribute(XamlNamespaces.XAttrName)?.Value, _config.MaxIdLength);
        if (anchorName == null) return null;

        // Count same-type siblings within the ancestor's subtree that appear
        // before this element in document order.
        int index = 1;
        foreach (var sibling in ancestor.Descendants(element.Name))
        {
            if (ReferenceEquals(sibling, element)) break;
            index++;
        }
        return $"Under{anchorName}_{index}";
    }

    private static string ExtractHashSlug(XElement element)
    {
        var attrs = element.Attributes()
            .Where(a => a.Name.LocalName != "AutomationProperties.AutomationId")
            .OrderBy(a => a.Name.LocalName, StringComparer.Ordinal)
            .Select(a => $"{a.Name.LocalName}={a.Value}");
        var canonical = string.Join(";", attrs);
        // Include element local name so two anonymous elements of different
        // types don't collide on identical attribute sets.
        canonical = $"{element.Name.LocalName}|{canonical}";

        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(canonical));
        var hex = Convert.ToHexStringLower(bytes).Substring(0, 8);
        return "h_" + hex;
    }

    private static XElement? NearestNamedAncestor(XElement element)
    {
        for (var p = element.Parent; p != null; p = p.Parent)
        {
            if (p.Attribute(XamlNamespaces.XAttrName) != null) return p;
        }
        return null;
    }

    private static string SanitizeNumeric(string raw)
    {
        // Strip whitespace; replace '-' with 'N' (negative); fall back to digits-only.
        var trimmed = raw.Trim();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.Length > 0 ? sb.ToString() : "X";
    }

    private static string Build(string view, string controlType, string suffix)
        => $"{view}_{controlType}_{suffix}";

    private static string StripPrefix(string controlType)
    {
        var i = controlType.IndexOf(':');
        return i >= 0 ? controlType[(i + 1)..] : controlType;
    }
}
