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
