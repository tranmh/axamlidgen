using AxamlIdGen.Cli;
using AxamlIdGen.Models;
using Xunit;

namespace AxamlIdGen.Tests.Property;

/// <summary>
/// Property-style invariants for the synthesis pipeline.
/// Uses repeated randomized inputs rather than FsCheck to avoid an extra dependency.
/// </summary>
public class IdSynthesizerProperties
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(1337)]
    public void Write_is_idempotent(int seed)
    {
        var dir = GenerateRandomProject(seed, fileCount: 4, controlsPerFile: 6);

        var first = Pipeline.Run(dir, new ToolConfig(), writeBack: true);
        Assert.Empty(first.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var snapshot = SnapshotFiles(dir);

        var second = Pipeline.Run(dir, new ToolConfig(), writeBack: true);
        Assert.Equal(0, second.Synthesized);
        Assert.Equal(snapshot, SnapshotFiles(dir));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(23)]
    public void Explicit_ids_are_never_modified_by_write(int seed)
    {
        var rng = new Random(seed);
        var explicitIds = Enumerable.Range(0, 5).Select(i => $"View_Button_Explicit{i}").ToList();
        var xaml = $"{TestHelpers.AvaRoot}<StackPanel>" +
            string.Join("", explicitIds.Select(id =>
                $"<Button AutomationProperties.AutomationId=\"{id}\"/>")) +
            $"<Button x:Name=\"Cancel\" AutomationProperties.AutomationId=\"@auto\"/>" +
            "</StackPanel></Window>";

        var dir = Path.Combine(Path.GetTempPath(), "axamlidgen-prop", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "View.axaml");
        File.WriteAllText(path, xaml);

        Pipeline.Run(dir, new ToolConfig(), writeBack: true);
        var content = File.ReadAllText(path);
        foreach (var id in explicitIds)
            Assert.Contains(id, content);
    }

    [Fact]
    public void Synthesized_ids_appear_in_typescript_manifest()
    {
        var dir = GenerateRandomProject(seed: 99, fileCount: 3, controlsPerFile: 4);
        var result = Pipeline.Run(dir, new ToolConfig(), writeBack: true);
        var instanceIds = result.Occurrences
            .Where(o => o.Scope == Scope.Instance && o.FinalId != null)
            .Select(o => o.FinalId!);
        foreach (var id in instanceIds)
            Assert.Contains($"{id}: \"{id}\"", result.TypeScriptManifest);
    }

    private static string GenerateRandomProject(int seed, int fileCount, int controlsPerFile)
    {
        var rng = new Random(seed);
        var dir = Path.Combine(Path.GetTempPath(), "axamlidgen-prop", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);

        for (int f = 0; f < fileCount; f++)
        {
            var view = $"View{f}";
            var sb = new System.Text.StringBuilder();
            sb.Append(TestHelpers.AvaRoot);
            sb.Append("<StackPanel>");
            for (int c = 0; c < controlsPerFile; c++)
            {
                var control = rng.Next(2) == 0 ? "Button" : "TextBox";
                var (xName, content, idValue) = rng.Next(3) switch
                {
                    0 => ($"Item{f}_{c}", (string?)null, "@auto"),
                    1 => (null, $"Label {f}{c}", "@auto"),
                    _ => (null, (string?)null, $"{view}_{control}_Pre_F{f}C{c}")
                };

                sb.Append($"<{control}");
                if (xName != null) sb.Append($" x:Name=\"{xName}\"");
                if (content != null) sb.Append($" Content=\"{content}\"");
                sb.Append($" AutomationProperties.AutomationId=\"{idValue}\"/>");
            }
            sb.Append("</StackPanel></Window>");
            File.WriteAllText(Path.Combine(dir, $"{view}.axaml"), sb.ToString());
        }
        return dir;
    }

    private static Dictionary<string, string> SnapshotFiles(string dir)
        => Directory.EnumerateFiles(dir, "*.axaml")
            .ToDictionary(p => Path.GetFileName(p), p => File.ReadAllText(p));
}
