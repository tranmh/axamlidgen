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
    public void Refuses_anonymous_Button_with_AID020_when_cutoff_is_3()
    {
        // With cutoff=3, anchorless controls are refused (strict mode).
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Button/></Window>");

        var config = new ToolConfig { AnchorTierCutoff = 3 };
        var result = new Instrumenter(config).InstrumentFile(path);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Refused);
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
    public void Tier4_extracts_binding_path_from_Command()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Button Command=\"{{Binding LoginCommand}}\"/></Window>");
        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);
        Assert.Equal(1, result.Added);
        Assert.Contains("View_Button_LoginCommand", File.ReadAllText(path));
        Assert.Equal(1, result.PerTierCounts[4]);
    }

    [Fact]
    public void Tier4_extracts_dotted_binding_path_with_underscore_join()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<TextBox Text=\"{{Binding User.Name}}\"/></Window>");
        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);
        Assert.Equal(1, result.Added);
        // Slugifier collapses underscores in PascalCase; "User.Name" becomes "UserName".
        Assert.Contains("View_TextBox_UserName", File.ReadAllText(path));
    }

    [Fact]
    public void Tier4_handles_explicit_Path_in_binding()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Slider Value=\"{{Binding Path=Volume}}\"/></Window>");
        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);
        Assert.Equal(1, result.Added);
        Assert.Contains("View_Slider_Volume", File.ReadAllText(path));
    }

    [Fact]
    public void Tier5_uses_grid_position_under_named_ancestor()
    {
        // Skip Tier 4 by giving control no binding; Grid.Row+Col under named Grid.
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Grid x:Name=\"LoginGrid\"><TextBox Grid.Row=\"1\" Grid.Column=\"2\"/></Grid></Window>");
        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);
        Assert.Equal(1, result.Added);
        Assert.Contains("View_TextBox_LoginGrid_R1C2", File.ReadAllText(path));
        Assert.Equal(1, result.PerTierCounts[5]);
    }

    [Fact]
    public void Tier6_falls_back_to_subtree_ordinal_under_named_ancestor()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<Border x:Name=\"LoginCard\"><StackPanel><TextBox/><TextBox/></StackPanel></Border></Window>");
        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);
        Assert.Equal(2, result.Added);
        var content = File.ReadAllText(path);
        Assert.Contains("UnderLoginCard_1", content);
        Assert.Contains("UnderLoginCard_2", content);
        Assert.Equal(2, result.PerTierCounts[6]);
    }

    [Fact]
    public void Tier7_hash_succeeds_for_attributes_only_control()
    {
        var path = TestHelpers.WriteTemp("View.axaml",
            $"{TestHelpers.AvaRoot}<TextBox Width=\"200\"/></Window>");
        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);
        Assert.Equal(1, result.Added);
        var content = File.ReadAllText(path);
        Assert.Matches(@"View_TextBox_h_[0-9a-f]{8}", content);
        Assert.Equal(1, result.PerTierCounts[7]);
    }

    [Fact]
    public void Tier7_hash_is_deterministic_across_runs()
    {
        var xaml = $"{TestHelpers.AvaRoot}<TextBox Width=\"200\" Margin=\"5\"/></Window>";
        var p1 = TestHelpers.WriteTemp("View.axaml", xaml);
        var p2 = TestHelpers.WriteTemp("View.axaml", xaml);
        new Instrumenter(new ToolConfig()).InstrumentFile(p1);
        new Instrumenter(new ToolConfig()).InstrumentFile(p2);

        var hash1 = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(p1), @"h_[0-9a-f]{8}").Value;
        var hash2 = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(p2), @"h_[0-9a-f]{8}").Value;
        Assert.NotEmpty(hash1);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Cutoff_4_resolves_what_cutoff_3_refuses()
    {
        var xaml = $"{TestHelpers.AvaRoot}<Button Command=\"{{Binding SaveCommand}}\"/></Window>";

        var p3 = TestHelpers.WriteTemp("V.axaml", xaml);
        var r3 = new Instrumenter(new ToolConfig { AnchorTierCutoff = 3 }).InstrumentFile(p3);
        Assert.Equal(1, r3.Refused);

        var p4 = TestHelpers.WriteTemp("V.axaml", xaml);
        var r4 = new Instrumenter(new ToolConfig { AnchorTierCutoff = 4 }).InstrumentFile(p4);
        Assert.Equal(1, r4.Added);
        Assert.Equal(0, r4.Refused);
    }

    [Fact]
    public void Idempotence_with_all_tiers_active()
    {
        var xaml = $@"{TestHelpers.AvaRoot}
            <Grid x:Name=""Outer"">
                <Button x:Name=""Save""/>
                <TextBox Text=""{{Binding User.Email}}""/>
                <ComboBox Grid.Row=""2"" Grid.Column=""1""/>
                <Slider Width=""150""/>
            </Grid>
        </Window>";
        var p = TestHelpers.WriteTemp("V.axaml", xaml);
        var first = new Instrumenter(new ToolConfig()).InstrumentFile(p);
        var afterFirst = File.ReadAllText(p);
        var second = new Instrumenter(new ToolConfig()).InstrumentFile(p);
        var afterSecond = File.ReadAllText(p);
        Assert.True(first.Added > 0);
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
