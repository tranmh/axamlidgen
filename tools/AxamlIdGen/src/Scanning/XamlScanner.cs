using System.Text.RegularExpressions;

namespace AxamlIdGen.Scanning;

public static class XamlScanner
{
    public static IEnumerable<string> EnumerateFiles(
        string root,
        IEnumerable<string> includeGlobs,
        IEnumerable<string> excludeGlobs)
    {
        var includeRegexes = includeGlobs.Select(GlobToRegex).ToArray();
        var excludeRegexes = excludeGlobs.Select(GlobToRegex).ToArray();

        var rootFull = Path.GetFullPath(root);

        foreach (var file in Directory.EnumerateFiles(rootFull, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(rootFull, file).Replace('\\', '/');
            if (!includeRegexes.Any(r => r.IsMatch(rel))) continue;
            if (excludeRegexes.Any(r => r.IsMatch(rel))) continue;
            yield return file;
        }
    }

    private static Regex GlobToRegex(string glob)
    {
        var sb = new System.Text.StringBuilder("^");
        for (int i = 0; i < glob.Length; i++)
        {
            char c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                sb.Append(".*");
                i++;
                if (i + 1 < glob.Length && glob[i + 1] == '/') i++;
            }
            else if (c == '*')
            {
                sb.Append("[^/]*");
            }
            else if (c == '?')
            {
                sb.Append("[^/]");
            }
            else if (".+()[]{}|^$\\".Contains(c))
            {
                sb.Append('\\').Append(c);
            }
            else
            {
                sb.Append(c);
            }
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase);
    }
}
