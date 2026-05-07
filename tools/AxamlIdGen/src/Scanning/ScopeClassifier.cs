using System.Xml;
using System.Xml.Linq;
using AxamlIdGen.Models;

namespace AxamlIdGen.Scanning;

public static class ScopeClassifier
{
    public static Scope Classify(XElement element, out string? templateTargetType, out int scopeKey)
    {
        templateTargetType = null;
        scopeKey = 0;
        for (var p = element.Parent; p != null; p = p.Parent)
        {
            var local = p.Name.LocalName;
            if (local == "ControlTemplate")
            {
                templateTargetType = p.Attribute("TargetType")?.Value;
                scopeKey = ScopeKey(p);
                return Scope.ControlTemplate;
            }
            if (local == "DataTemplate" || local == "ItemTemplate")
            {
                templateTargetType = p.Attribute("DataType")?.Value;
                scopeKey = ScopeKey(p);
                return Scope.DataTemplate;
            }
            if (local == "Setter" || local == "Style")
            {
                templateTargetType = local == "Style" ? p.Attribute("TargetType")?.Value : null;
                scopeKey = ScopeKey(p);
                return Scope.StyleSetter;
            }
        }
        return Scope.Instance;
    }

    public static Scope Classify(XElement element, out string? templateTargetType)
        => Classify(element, out templateTargetType, out _);

    private static int ScopeKey(XElement scopeRoot)
    {
        // Use line + column of the template element as a stable per-document scope key.
        // (Two sibling <ControlTemplate>s have different line numbers.)
        var li = (IXmlLineInfo)scopeRoot;
        return li.HasLineInfo() ? unchecked((li.LineNumber * 1024) + li.LinePosition) : RuntimeHelpers.GetHashCode(scopeRoot);
    }

    private static class RuntimeHelpers
    {
        public static int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
    }
}
