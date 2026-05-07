using AxamlIdGen.Cli;
using AxamlIdGen.Models;
using Xunit;

namespace AxamlIdGen.Tests.Integration;

public class GoldenFixtureTests
{
    [Fact]
    public void Avalonia_happy_path_emits_sorted_TypeScript()
    {
        var dir = MakeProject(("LoginView.axaml",
            $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"LoginView_Submit_Button\"/></Window>"));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        Assert.Empty(result.Diagnostics);
        Assert.Contains("LoginView_Submit_Button: \"LoginView_Submit_Button\"", result.TypeScriptManifest);
        Assert.Contains("public const string LoginView_Submit_Button", result.CSharpManifest);
        Assert.Contains("\"LoginView_Submit_Button\"", result.JsonManifest);
    }

    [Fact]
    public void Wpf_happy_path_emits_correct_ids()
    {
        var dir = MakeProject(("MainWindow.xaml",
            $"{TestHelpers.WpfRoot}<TextBox AutomationProperties.AutomationId=\"MainWindow_Name_Field\"/></Window>"));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        Assert.Empty(result.Diagnostics);
        Assert.Contains("MainWindow_Name_Field", result.JsonManifest);
    }

    [Fact]
    public void Mixed_dialects_in_one_repo_are_handled()
    {
        var dir = MakeProject(
            ("WpfView.xaml",
             $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"WpfView_Save_Button\"/></Window>"),
            ("AvaView.axaml",
             $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"AvaView_Save_Button\"/></Window>"));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Occurrences.Count);
    }

    [Fact]
    public void Sentinel_only_file_is_filled_idempotently()
    {
        var dir = MakeProject(("LoginView.axaml",
            $"{TestHelpers.AvaRoot}<Button x:Name=\"Submit\" AutomationProperties.AutomationId=\"@auto\"/></Window>"));

        var result1 = Pipeline.Run(dir, new ToolConfig(), writeBack: true);
        Assert.Empty(result1.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(1, result1.Synthesized);

        var path = Directory.GetFiles(dir, "*.axaml")[0];
        var content1 = File.ReadAllText(path);
        Assert.Contains("LoginView_Button_Submit", content1);
        Assert.DoesNotContain("@auto", content1);

        // Idempotence
        var result2 = Pipeline.Run(dir, new ToolConfig(), writeBack: true);
        var content2 = File.ReadAllText(path);
        Assert.Equal(content1, content2);
        Assert.Equal(0, result2.Synthesized);
    }

    [Fact]
    public void Duplicate_explicit_instance_ids_produce_AID001()
    {
        var dir = MakeProject(
            ("A.axaml", $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"View_Button_Submit\"/></Window>"),
            ("B.axaml", $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"View_Button_Submit\"/></Window>"));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        var errs = result.Diagnostics.Where(d => d.Code == "AID001").ToList();
        Assert.NotEmpty(errs);
    }

    [Fact]
    public void Malformed_xaml_is_reported_not_thrown()
    {
        var dir = MakeProject(("Bad.axaml", "<Window><Button>"));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        Assert.Contains(result.Diagnostics, d => d.Code == "AID100");
    }

    [Fact]
    public void Empty_xaml_does_not_crash_or_error()
    {
        // <Window/> is well-formed but has no AutomationIds.
        var dir = MakeProject(("Empty.axaml", $"{TestHelpers.AvaRoot}</Window>"));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Occurrences);
    }

    [Fact]
    public void Crlf_and_lf_files_both_parse()
    {
        var lf   = $"{TestHelpers.AvaRoot}\n<Button AutomationProperties.AutomationId=\"Lf_Button_Test\"/>\n</Window>\n";
        var crlf = $"{TestHelpers.AvaRoot}\r\n<Button AutomationProperties.AutomationId=\"Crlf_Button_Test\"/>\r\n</Window>\r\n";
        var dir = MakeProject(("Lf.axaml", lf), ("Crlf.axaml", crlf));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(2, result.Occurrences.Count);
    }

    private static string MakeProject(params (string filename, string contents)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "axamlidgen-fix", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        foreach (var (name, body) in files)
            File.WriteAllText(Path.Combine(dir, name), body);
        return dir;
    }
}
