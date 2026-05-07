using System.Globalization;
using System.Text;

namespace AxamlIdGen.Synthesis;

public static class Slugifier
{
    public static string? Slugify(string? input, int maxLength = 64)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // Normalize and strip diacritics
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }

        var stripped = sb.ToString().Normalize(NormalizationForm.FormC);

        // Build PascalCase from word boundaries
        var result = new StringBuilder(stripped.Length);
        bool capitalizeNext = true;
        foreach (var c in stripped)
        {
            if (char.IsLetterOrDigit(c) && c < 128)
            {
                if (capitalizeNext) { result.Append(char.ToUpperInvariant(c)); capitalizeNext = false; }
                else result.Append(c);
            }
            else
            {
                capitalizeNext = true;
            }
        }

        if (result.Length == 0) return null;

        // Identifier must start with a letter
        if (char.IsDigit(result[0])) result.Insert(0, '_');

        if (result.Length > maxLength)
            result.Length = maxLength;

        return result.ToString();
    }
}
