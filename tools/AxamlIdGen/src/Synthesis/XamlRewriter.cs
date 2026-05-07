using System.Text.RegularExpressions;
using AxamlIdGen.Models;

namespace AxamlIdGen.Synthesis;

/// <summary>
/// Replaces sentinel AutomationId values in XAML files with synthesized IDs,
/// preserving original formatting. Operates per-file via in-place regex replacement.
/// Supports attribute-form only (<c>AutomationProperties.AutomationId="@auto"</c>).
/// </summary>
public static class XamlRewriter
{
    private static readonly Regex SentinelAttribute = new(
        @"AutomationProperties\.AutomationId\s*=\s*(""[^""]*""|'[^']*')",
        RegexOptions.Compiled);

    public static bool ReplaceSentinels(string filePath, IList<(string OldValue, string NewId)> replacements)
    {
        if (replacements.Count == 0) return false;

        var original = File.ReadAllText(filePath);
        int replacementIndex = 0;

        var rewritten = SentinelAttribute.Replace(original, m =>
        {
            if (replacementIndex >= replacements.Count) return m.Value;

            var raw = m.Groups[1].Value;
            var quote = raw[0];
            var inner = raw[1..^1];

            var (oldValue, newId) = replacements[replacementIndex];
            if (inner != oldValue) return m.Value; // not the one we wanted

            replacementIndex++;
            return $"AutomationProperties.AutomationId={quote}{newId}{quote}";
        });

        if (rewritten == original) return false;
        File.WriteAllText(filePath, rewritten);
        return true;
    }
}
