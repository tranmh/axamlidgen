using AxamlIdGen.Models;
using Xunit;

namespace AxamlIdGen.Tests.Unit;

public class AutomationIdParserTests
{
    [Fact]
    public void Wpf_default_xmlns_attribute_is_extracted()
    {
        var xaml = $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"Foo\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Single(occ);
        Assert.Equal("Foo", occ[0].FinalId);
        Assert.Equal(OccurrenceState.Explicit, occ[0].State);
    }

    [Fact]
    public void Avalonia_default_xmlns_attribute_is_extracted()
    {
        var xaml = $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"Bar\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Single(occ);
        Assert.Equal("Bar", occ[0].FinalId);
    }

    [Fact]
    public void Xamarin_forms_attribute_is_extracted()
    {
        var xaml = $"{TestHelpers.XfRoot}<Button AutomationProperties.AutomationId=\"Baz\"/></ContentPage>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Single(occ);
        Assert.Equal("Baz", occ[0].FinalId);
    }

    [Fact]
    public void File_with_no_AutomationIds_returns_empty_list()
    {
        var xaml = $"{TestHelpers.WpfRoot}<Button Content=\"Click me\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Empty(occ);
    }

    [Fact]
    public void Sentinel_value_is_recognised()
    {
        var xaml = $"{TestHelpers.WpfRoot}<Button x:Name=\"Submit\" AutomationProperties.AutomationId=\"@auto\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Single(occ);
        Assert.Equal(OccurrenceState.Sentinel, occ[0].State);
        Assert.Equal("@auto", occ[0].OriginalValue);
    }

    [Fact]
    public void Static_content_is_extracted()
    {
        var xaml = $"{TestHelpers.WpfRoot}<Button Content=\"Sign in\" AutomationProperties.AutomationId=\"@auto\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Equal("Sign in", occ[0].StaticContent);
    }

    [Fact]
    public void Bound_content_is_ignored()
    {
        var xaml = $"{TestHelpers.WpfRoot}<Button Content=\"{{Binding Title}}\" AutomationProperties.AutomationId=\"@auto\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Null(occ[0].StaticContent);
    }

    [Fact]
    public void Line_and_column_are_reported()
    {
        var xaml = $"{TestHelpers.WpfRoot}\n<Button AutomationProperties.AutomationId=\"Foo\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml);
        Assert.Equal(2, occ[0].Line);
        Assert.True(occ[0].Column > 0);
    }

    [Fact]
    public void View_name_comes_from_filename()
    {
        var xaml = $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"Foo\"/></Window>";
        var (occ, _) = TestHelpers.ParseString(xaml, "LoginView.axaml");
        Assert.Equal("LoginView", occ[0].ViewName);
    }
}
