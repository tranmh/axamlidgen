using AxamlIdGen.Models;
using AxamlIdGen.Synthesis;
using Xunit;

namespace AxamlIdGen.Tests.Unit;

public class IdSynthesizerTests
{
    private static IdSynthesizer New() => new IdSynthesizer(new ToolConfig());

    [Fact]
    public void XName_takes_precedence()
    {
        var s = New();
        var (id, source, ord) = s.Synthesize("LoginView", "Button", xName: "Submit", staticContent: "Sign in", owningPropertyName: null);
        Assert.Equal("LoginView_Button_Submit", id);
        Assert.Equal(ProvenanceSource.XName, source);
        Assert.False(ord);
    }

    [Fact]
    public void Static_content_used_when_no_XName()
    {
        var s = New();
        var (id, source, _) = s.Synthesize("LoginView", "Button", xName: null, staticContent: "Sign in", owningPropertyName: null);
        Assert.Equal("LoginView_Button_SignIn", id);
        Assert.Equal(ProvenanceSource.Content, source);
    }

    [Fact]
    public void Property_name_used_when_no_content_or_name()
    {
        var s = New();
        var (id, source, _) = s.Synthesize("MainWindow", "Path", xName: null, staticContent: null, owningPropertyName: "Icon");
        Assert.Equal("MainWindow_Path_Icon", id);
        Assert.Equal(ProvenanceSource.PropertyName, source);
    }

    [Fact]
    public void Ordinal_fallback_when_nothing_else_available()
    {
        var s = New();
        var (id1, source, ord) = s.Synthesize("V", "Border", null, null, null);
        var (id2, _, _) = s.Synthesize("V", "Border", null, null, null);
        Assert.Equal("V_Border_1", id1);
        Assert.Equal("V_Border_2", id2);
        Assert.Equal(ProvenanceSource.Ordinal, source);
        Assert.True(ord);
    }

    [Fact]
    public void Collision_resolution_appends_numeric_suffix()
    {
        var s = New();
        var taken = new HashSet<string>(StringComparer.Ordinal) { "View_Button_Submit", "View_Button_Submit_2" };
        var resolved = s.ResolveCollision("View_Button_Submit", taken);
        Assert.Equal("View_Button_Submit_3", resolved);
    }

    [Fact]
    public void XName_prefix_is_stripped()
    {
        // Some XAML uses prefixed control types like ns:Button — the slug should drop the prefix.
        var s = New();
        var (id, _, _) = s.Synthesize("V", "ns:Button", "Submit", null, null);
        Assert.Equal("V_Button_Submit", id);
    }
}
