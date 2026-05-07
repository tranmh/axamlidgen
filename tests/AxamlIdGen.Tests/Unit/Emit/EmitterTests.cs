using AxamlIdGen.Emit;
using AxamlIdGen.Models;
using Xunit;

namespace AxamlIdGen.Tests.Unit.Emit;

public class EmitterTests
{
    private static AutomationIdOccurrence Inst(string id, string view = "V", string control = "Button")
        => new()
        {
            File = $"/repo/{view}.axaml",
            Line = 10,
            Column = 5,
            ControlElementName = control,
            Scope = Scope.Instance,
            State = OccurrenceState.Explicit,
            FinalId = id,
            Provenance = ProvenanceSource.Explicit,
            ViewName = view,
            Element = new System.Xml.Linq.XElement(control),
            IdAttribute = null
        };

    private static AutomationIdOccurrence Templated(string id)
        => new()
        {
            File = "/repo/App.xaml",
            Line = 1, Column = 1,
            ControlElementName = "ScrollBar",
            Scope = Scope.ControlTemplate,
            TemplateTargetType = "ScrollViewer",
            State = OccurrenceState.Explicit,
            FinalId = id,
            Provenance = ProvenanceSource.Explicit,
            Element = new System.Xml.Linq.XElement("ScrollBar"),
            IdAttribute = null
        };

    [Fact]
    public void CSharp_emits_sorted_constants()
    {
        var occ = new[] { Inst("View_B"), Inst("View_A") };
        var cs = ManifestEmitter.EmitCSharp(occ, "MyNs");
        var aIdx = cs.IndexOf("View_A");
        var bIdx = cs.IndexOf("View_B");
        Assert.True(aIdx > 0 && bIdx > aIdx, "Constants must appear in sorted order");
        Assert.Contains("namespace MyNs;", cs);
    }

    [Fact]
    public void TypeScript_emits_const_object_and_keyof_type()
    {
        var occ = new[] { Inst("View_X") };
        var ts = ManifestEmitter.EmitTypeScript(occ);
        Assert.Contains("export const AutomationIds = {", ts);
        Assert.Contains("View_X: \"View_X\"", ts);
        Assert.Contains("as const;", ts);
        Assert.Contains("export type AutomationIdKey = keyof typeof AutomationIds;", ts);
    }

    [Fact]
    public void Json_separates_instance_from_template_scoped()
    {
        var occ = new[] { Inst("View_Foo"), Templated("VerticalScrollBar") };
        var json = ManifestEmitter.EmitJson(occ, "/repo");
        Assert.Contains("\"View_Foo\"", json);
        Assert.Contains("\"templateScoped\"", json);
        Assert.Contains("\"VerticalScrollBar\"", json);
    }

    [Fact]
    public void Templated_ids_excluded_from_CSharp_constants()
    {
        var occ = new[] { Inst("View_Foo"), Templated("VerticalScrollBar") };
        var cs = ManifestEmitter.EmitCSharp(occ, "Ns");
        Assert.Contains("View_Foo", cs);
        Assert.DoesNotContain("VerticalScrollBar", cs);
    }
}
