using AxamlIdGen.Models;

namespace AxamlIdGen.Synthesis;

public sealed class IdSynthesizer
{
    private readonly ToolConfig _config;
    private readonly Dictionary<(string view, string controlType), int> _ordinals = new();

    public IdSynthesizer(ToolConfig config) { _config = config; }

    public (string Id, ProvenanceSource Source, bool IsOrdinal) Synthesize(
        string viewName,
        string controlType,
        string? xName,
        string? staticContent,
        string? owningPropertyName)
    {
        // Slugify view name so unusual filenames still yield convention-conformant IDs.
        viewName = Slugifier.Slugify(viewName, _config.MaxIdLength) ?? "View";
        // Strip XAML namespace prefix from controlType if present
        controlType = StripPrefix(controlType);

        // 1. x:Name
        var xNameSlug = Slugifier.Slugify(xName, _config.MaxIdLength);
        if (xNameSlug != null)
            return (Build(viewName, controlType, xNameSlug), ProvenanceSource.XName, false);

        // 2. Static content (Content/Text/Header/Title/ToolTip)
        var contentSlug = Slugifier.Slugify(staticContent, _config.MaxIdLength);
        if (contentSlug != null)
            return (Build(viewName, controlType, contentSlug), ProvenanceSource.Content, false);

        // 3. Owning property name (e.g., Button.Icon → Icon)
        var propSlug = Slugifier.Slugify(owningPropertyName, _config.MaxIdLength);
        if (propSlug != null)
            return (Build(viewName, controlType, propSlug), ProvenanceSource.PropertyName, false);

        // 4. Ordinal fallback
        var key = (viewName, controlType);
        var n = _ordinals.TryGetValue(key, out var v) ? v + 1 : 1;
        _ordinals[key] = n;
        return (Build(viewName, controlType, n.ToString()), ProvenanceSource.Ordinal, true);
    }

    public string ResolveCollision(string baseId, ISet<string> taken)
    {
        if (!taken.Contains(baseId)) return baseId;
        for (int i = 2; i < int.MaxValue; i++)
        {
            var candidate = $"{baseId}_{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
        throw new InvalidOperationException("Exhausted collision suffix range.");
    }

    private static string Build(string view, string controlType, string suffix)
        => $"{view}_{controlType}_{suffix}";

    private static string StripPrefix(string controlType)
    {
        var i = controlType.IndexOf(':');
        return i >= 0 ? controlType[(i + 1)..] : controlType;
    }
}
