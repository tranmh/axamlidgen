using System.Text;
using System.Text.RegularExpressions;

namespace AxamlIdGen.Synthesis;

/// <summary>
/// Text-based, byte-preserving mutations of XAML files. Two strategies:
///  - <see cref="ReplaceSentinels"/> replaces values of existing
///    <c>AutomationProperties.AutomationId</c> attributes.
///  - <see cref="InsertAttributes"/> splices a new attribute immediately
///    before the closing <c>&gt;</c> or <c>/&gt;</c> of an element's opening
///    tag, given the (line, col) of the element name.
/// Both preserve original formatting, quote style, BOM, and line endings.
/// </summary>
public static class XamlRewriter
{
    private static readonly Regex SentinelAttribute = new(
        @"AutomationProperties\.AutomationId\s*=\s*(""[^""]*""|'[^']*')",
        RegexOptions.Compiled);

    public static bool ReplaceSentinels(string filePath, IList<(string OldValue, string NewId)> replacements)
    {
        if (replacements.Count == 0) return false;

        var (original, hadBom) = ReadTextWithBomFlag(filePath);
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
        WriteTextWithBomFlag(filePath, rewritten, hadBom);
        return true;
    }

    /// <summary>
    /// Inserts new attributes (typically AutomationProperties.AutomationId) into
    /// element opening tags identified by (1-based line, 1-based col) of the
    /// element local name (i.e. the char immediately after <c>&lt;</c>). The new
    /// attribute is spliced just before the <c>&gt;</c> or <c>/</c> of <c>/&gt;</c>,
    /// preserving every other byte of the file (including BOM and line endings).
    /// </summary>
    public static bool InsertAttributes(
        string filePath,
        IReadOnlyList<(int Line, int Col, string LocalName, string Value)> insertions)
    {
        if (insertions.Count == 0) return false;

        var (content, hadBom) = ReadTextWithBomFlag(filePath);
        var lineStarts = ComputeLineStarts(content);

        // Sort descending so later splices don't shift the offsets of pending earlier ones.
        var sorted = insertions
            .OrderByDescending(t => t.Line)
            .ThenByDescending(t => t.Col)
            .ToList();

        foreach (var (line, col, localName, value) in sorted)
        {
            if (line < 1 || line > lineStarts.Length) continue;
            int nameOffset = lineStarts[line - 1] + (col - 1);
            if (nameOffset < 0 || nameOffset >= content.Length) continue;

            int insertAt = FindOpeningTagEnd(content, nameOffset);
            string injection = $" {localName}=\"{EscapeAttributeValue(value)}\"";
            content = content.Insert(insertAt, injection);
        }

        WriteTextWithBomFlag(filePath, content, hadBom);
        return true;
    }

    /// <summary>
    /// Walks forward from the element-name offset until it finds the closing
    /// <c>&gt;</c> or the <c>/</c> of a <c>/&gt;</c>, respecting quoted attribute
    /// values. Returns the offset to splice the new attribute at — splicing here
    /// places the new attribute immediately before the closing bracket.
    /// </summary>
    private static int FindOpeningTagEnd(string content, int nameOffset)
    {
        int i = nameOffset;
        char quote = '\0';
        while (i < content.Length)
        {
            char c = content[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
            }
            else
            {
                if (c == '"' || c == '\'') quote = c;
                else if (c == '/' && i + 1 < content.Length && content[i + 1] == '>') return i;
                else if (c == '>') return i;
            }
            i++;
        }
        throw new InvalidOperationException(
            $"Unterminated opening tag while scanning from offset {nameOffset}.");
    }

    private static int[] ComputeLineStarts(string content)
    {
        // line 1 starts at 0; each '\n' begins the next line at the following char.
        var starts = new List<int> { 0 };
        for (int i = 0; i < content.Length; i++)
            if (content[i] == '\n') starts.Add(i + 1);
        return starts.ToArray();
    }

    private static string EscapeAttributeValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '"': sb.Append("&quot;"); break;
                default:  sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static (string Text, bool HadBom) ReadTextWithBomFlag(string path)
    {
        var raw = File.ReadAllBytes(path);
        bool hadBom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
        int start = hadBom ? 3 : 0;
        var text = Encoding.UTF8.GetString(raw, start, raw.Length - start);
        return (text, hadBom);
    }

    private static void WriteTextWithBomFlag(string path, string text, bool hadBom)
    {
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hadBom);
        File.WriteAllText(path, text, enc);
    }
}
