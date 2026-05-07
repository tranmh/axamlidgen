using AxamlIdGen.Models;
using Xunit;

namespace AxamlIdGen.Tests.Unit;

public class ScopeClassifierTests
{
    [Fact]
    public void Element_at_top_level_is_Instance()
    {
        var xaml = $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"Foo\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Equal(Scope.Instance, occ[0].Scope);
    }

    [Fact]
    public void Element_inside_ControlTemplate_is_ControlTemplate()
    {
        var xaml = $@"{TestHelpers.WpfRoot}
            <Window.Resources>
                <ControlTemplate TargetType=""Button"">
                    <Border AutomationProperties.AutomationId=""Inner""/>
                </ControlTemplate>
            </Window.Resources>
        </Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Single(occ);
        Assert.Equal(Scope.ControlTemplate, occ[0].Scope);
        Assert.Equal("Button", occ[0].TemplateTargetType);
    }

    [Fact]
    public void Element_inside_DataTemplate_is_DataTemplate()
    {
        var xaml = $@"{TestHelpers.WpfRoot}
            <Window.Resources>
                <DataTemplate DataType=""local:Item"">
                    <TextBlock AutomationProperties.AutomationId=""ItemText""/>
                </DataTemplate>
            </Window.Resources>
        </Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Equal(Scope.DataTemplate, occ[0].Scope);
    }

    [Fact]
    public void Two_sibling_ControlTemplates_get_distinct_scope_keys()
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
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Equal(2, occ.Count);
        Assert.NotEqual(occ[0].TemplateScopeKey, occ[1].TemplateScopeKey);
    }

    [Fact]
    public void Element_inside_Style_Setter_ControlTemplate_is_ControlTemplate()
    {
        var xaml = $@"{TestHelpers.WpfRoot}
            <Window.Resources>
                <Style TargetType=""Button"">
                    <Setter Property=""Template"">
                        <Setter.Value>
                            <ControlTemplate TargetType=""Button"">
                                <Border AutomationProperties.AutomationId=""Inner""/>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
            </Window.Resources>
        </Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Equal(Scope.ControlTemplate, occ[0].Scope);
    }
}
