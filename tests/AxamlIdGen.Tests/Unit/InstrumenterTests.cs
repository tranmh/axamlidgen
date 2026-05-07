using AxamlIdGen.Models;
using AxamlIdGen.Synthesis;
using Xunit;

namespace AxamlIdGen.Tests.Unit;

public class InstrumenterTests
{
    [Fact]
    public void Injects_AutomationId_on_named_Button()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Button x:Name=\"Submit\"/></Window>");

        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);

        Assert.Equal(1, result.Added);
        var content = File.ReadAllText(path);
        Assert.Contains("AutomationProperties.AutomationId=\"View_Button_Submit\"", content);
    }

    [Fact]
    public void Refuses_anonymous_Button_with_AID020()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Button/></Window>");

        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.SkippedAnchorless);
        Assert.Single(result.Diagnostics);
        Assert.Equal("AID020", result.Diagnostics[0].Code);
    }

    [Fact]
    public void Uses_static_Content_when_no_xName()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Button Content=\"Save changes\"/></Window>");

        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);

        Assert.Equal(1, result.Added);
        var content = File.ReadAllText(path);
        Assert.Contains("View_Button_SaveChanges", content);
    }

    [Fact]
    public void Skips_elements_with_existing_AutomationId()
    {
        var original = $"{TestHelpers.AvaRoot}<Button x:Name=\"Submit\" AutomationProperties.AutomationId=\"Existing_Id_Value\"/></Window>";
        var path = TestHelpers.WriteTemp("View.axaml", original);

        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);

        Assert.Equal(0, result.Added);
        var content = File.ReadAllText(path);
        Assert.Contains("Existing_Id_Value", content);
    }

    [Fact]
    public void Skips_template_scoped_elements()
    {
        var path = TestHelpers.WriteTemp("App.xaml", $@"{TestHelpers.WpfRoot}
            <Window.Resources>
                <ControlTemplate TargetType=""Button"">
                    <Button x:Name=""TemplatedInner""/>
                </ControlTemplate>
            </Window.Resources>
        </Window>");

        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);

        Assert.Equal(0, result.Added);
        var content = File.ReadAllText(path);
        Assert.DoesNotContain("AutomationProperties.AutomationId", content);
    }

    [Fact]
    public void Skips_elements_not_in_allow_list()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Border x:Name=\"OuterBorder\"/></Window>");

        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);

        Assert.Equal(0, result.Added);
    }

    [Fact]
    public void Idempotent_second_run_makes_no_changes()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Button x:Name=\"Submit\"/><TextBox x:Name=\"Email\"/></Window>");
        var instrumenter = new Instrumenter(new ToolConfig());

        var first = instrumenter.InstrumentFile(path);
        var afterFirst = File.ReadAllText(path);

        var second = instrumenter.InstrumentFile(path);
        var afterSecond = File.ReadAllText(path);

        Assert.Equal(2, first.Added);
        Assert.Equal(0, second.Added);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void Custom_allow_list_overrides_default()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Border x:Name=\"OuterBorder\"/><Button x:Name=\"Submit\"/></Window>");

        var result = new Instrumenter(new ToolConfig
        {
            InstrumentControlTypes = new() { "Border" }
        }).InstrumentFile(path);

        Assert.Equal(1, result.Added);
        var content = File.ReadAllText(path);
        Assert.Contains("View_Border_OuterBorder", content);
        Assert.DoesNotContain("View_Button", content);
    }
}
