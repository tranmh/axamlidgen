using AxamlIdGen.Models;
using AxamlIdGen.Scanning;

namespace AxamlIdGen.Tests;

internal static class TestHelpers
{
    public static string WriteTemp(string filename, string contents, string? subdir = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "axamlidgen-tests", Guid.NewGuid().ToString("N")[..8]);
        var dir = subdir == null ? root : Path.Combine(root, subdir);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, filename);
        File.WriteAllText(path, contents);
        return path;
    }

    public static string WriteTempBytes(string filename, byte[] contents, string? subdir = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "axamlidgen-tests", Guid.NewGuid().ToString("N")[..8]);
        var dir = subdir == null ? root : Path.Combine(root, subdir);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, filename);
        File.WriteAllBytes(path, contents);
        return path;
    }

    /// <summary>
    /// Line-by-line diff counters: how many lines are in <paramref name="before"/>
    /// but not in <paramref name="after"/> at the same index ("modified"),
    /// how many extra lines appear in <paramref name="after"/> ("added"),
    /// how many fell out of <paramref name="after"/> ("removed").
    /// A line modification counts as Modified, not Added+Removed.
    /// </summary>
    public static (int Modified, int Added, int Removed) DiffStats(string before, string after)
    {
        // Split preserving line terminators so newline changes show up.
        var b = before.Split('\n');
        var a = after.Split('\n');
        int min = Math.Min(b.Length, a.Length);
        int modified = 0;
        for (int i = 0; i < min; i++)
            if (b[i] != a[i]) modified++;
        int added = Math.Max(0, a.Length - b.Length);
        int removed = Math.Max(0, b.Length - a.Length);
        return (modified, added, removed);
    }

    public static (List<AutomationIdOccurrence> Occ, List<MissingIdSite> Miss) ParseString(string xaml, string? filename = null)
    {
        var path = WriteTemp(filename ?? "Test.axaml", xaml);
        var (_, occ, miss) = AutomationIdParser.Parse(path, new ToolConfig());
        return (occ, miss);
    }

    public const string WpfRoot = "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">";
    public const string AvaRoot = "<Window xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">";
    public const string XfRoot  = "<ContentPage xmlns=\"http://xamarin.com/schemas/2014/forms\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">";
}
