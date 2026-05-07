using AxamlIdGen.Models;
using AxamlIdGen.Scanning;
using AxamlIdGen.Validation;
using Xunit;

namespace AxamlIdGen.Tests.Unit;

public class NamingValidatorTests
{
    private static AutomationIdOccurrence MakeOcc(string id) => new()
    {
        File = "x",
        Line = 1,
        Column = 1,
        ControlElementName = "Button",
        Scope = Scope.Instance,
        State = OccurrenceState.Explicit,
        FinalId = id,
        Element = new System.Xml.Linq.XElement("Button"),
        IdAttribute = null
    };

    [Fact]
    public void Conformant_id_passes_default_pattern()
    {
        var v = new NamingValidator(new ToolConfig().NamingPattern);
        Assert.Empty(v.Check([MakeOcc("LoginView_Submit_Button")]));
    }

    [Fact]
    public void Lower_case_first_letter_fails()
    {
        var v = new NamingValidator(new ToolConfig().NamingPattern);
        var diag = v.Check([MakeOcc("loginView_Foo")]);
        Assert.NotEmpty(diag);
        Assert.Equal("AID003", diag[0].Code);
    }

    [Fact]
    public void Single_segment_id_fails_default_pattern()
    {
        var v = new NamingValidator(new ToolConfig().NamingPattern);
        Assert.NotEmpty(v.Check([MakeOcc("Foo")]));
    }

    [Fact]
    public void Custom_pattern_overrides_default()
    {
        var v = new NamingValidator(@"^[a-z]+$");
        Assert.Empty(v.Check([MakeOcc("foo")]));
        Assert.NotEmpty(v.Check([MakeOcc("Foo")]));
    }

    [Fact]
    public void Template_scope_ids_skip_naming_check()
    {
        // Template-scoped IDs are commonly short legacy names like "VerticalScrollBar"
        // which don't match the default pattern. They must NOT be flagged.
        var v = new NamingValidator(new ToolConfig().NamingPattern);
        var occ = MakeOcc("VerticalScrollBar");
        var template = new AutomationIdOccurrence
        {
            File = occ.File, Line = 1, Column = 1, ControlElementName = "ScrollBar",
            Scope = Scope.ControlTemplate, State = OccurrenceState.Explicit,
            FinalId = "VerticalScrollBar",
            Element = new System.Xml.Linq.XElement("ScrollBar"), IdAttribute = null
        };
        Assert.Empty(v.Check([template]));
    }
}
