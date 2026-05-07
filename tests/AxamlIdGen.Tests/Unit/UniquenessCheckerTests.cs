using AxamlIdGen.Cli;
using AxamlIdGen.Models;
using Xunit;

namespace AxamlIdGen.Tests.Unit;

public class UniquenessCheckerTests
{
    [Fact]
    public void Distinct_instance_ids_pass()
    {
        var dir = TestProject(("View1.axaml", $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"View1_A\"/></Window>"),
                              ("View2.axaml", $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"View2_B\"/></Window>"));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Duplicate_instance_id_across_files_fails()
    {
        var dir = TestProject(("V1.axaml", $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"Same_Foo\"/></Window>"),
                              ("V2.axaml", $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"Same_Foo\"/></Window>"));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        var errs = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.NotEmpty(errs);
        Assert.All(errs, e => Assert.Equal("AID001", e.Code));
    }

    [Fact]
    public void Same_id_in_two_sibling_ControlTemplates_passes()
    {
        var xaml = $@"{TestHelpers.WpfRoot}
            <Window.Resources>
                <ControlTemplate TargetType=""Button"">
                    <Border AutomationProperties.AutomationId=""Same""/>
                </ControlTemplate>
                <ControlTemplate TargetType=""Button"">
                    <Border AutomationProperties.AutomationId=""Same""/>
                </ControlTemplate>
            </Window.Resources>
        </Window>";
        var dir = TestProject(("App.xaml", xaml));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Duplicate_id_within_a_single_ControlTemplate_fails()
    {
        var xaml = $@"{TestHelpers.WpfRoot}
            <Window.Resources>
                <ControlTemplate TargetType=""Button"">
                    <Border AutomationProperties.AutomationId=""Dup""/>
                    <Grid AutomationProperties.AutomationId=""Dup""/>
                </ControlTemplate>
            </Window.Resources>
        </Window>";
        var dir = TestProject(("App.xaml", xaml));
        var result = Pipeline.Run(dir, new ToolConfig(), false);
        var errs = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.NotEmpty(errs);
        Assert.All(errs, e => Assert.Equal("AID002", e.Code));
    }

    private static string TestProject(params (string filename, string contents)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "axamlidgen-uc", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        foreach (var (name, body) in files)
        {
            File.WriteAllText(Path.Combine(dir, name), body);
        }
        return dir;
    }
}
