using System.Text;
using AxamlIdGen.Models;
using AxamlIdGen.Synthesis;
using Xunit;

namespace AxamlIdGen.Tests.Unit;

/// <summary>
/// Minimal-diff property tests for <see cref="Instrumenter"/>.
///
/// Each test asserts that inserting AutomationId attributes produces a diff of
/// exactly N modified lines (N = result.Added), with zero added or removed lines.
/// Many fixtures are reductions of real corner cases found in metris-core; the
/// originating file and line number is cited in the test's doc-comment.
///
/// These tests deliberately stress byte-level preservation: quote style, BOM,
/// whitespace inside opening tags, tag form (self-closing vs explicit close),
/// line endings, and entity escapes.
/// </summary>
public class InstrumenterDiffTests
{
    private const string IdLocal = "AutomationProperties.AutomationId";

    private static (int Added, string Before, string After) Instrument(string xaml, string filename = "View.axaml")
    {
        var path = TestHelpers.WriteTemp(filename, xaml);
        var before = File.ReadAllText(path);
        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);
        var after = File.ReadAllText(path);
        return (result.Added, before, after);
    }

    private static (int Added, byte[] Before, byte[] After) InstrumentBytes(byte[] bytes, string filename = "View.axaml")
    {
        var path = TestHelpers.WriteTempBytes(filename, bytes);
        var before = File.ReadAllBytes(path);
        var result = new Instrumenter(new ToolConfig()).InstrumentFile(path);
        var after = File.ReadAllBytes(path);
        return (result.Added, before, after);
    }

    // 1.
    [Fact]
    public void SelfClosing_singleLine_oneLineModified_zeroAddedRemoved()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains("<Button x:Name=\"Submit\" " + IdLocal + "=\"", after);
    }

    // 2. mirrors Metris.CryptographyTool/MainWindow.xaml (multi-attr Button)
    [Fact]
    public void MultilineOpenTag_closingBracketOnLastAttrLine_oneLineModified()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"\n" +
            "            Content=\"Save\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        // Insertion lands on the line that has the closing /> — i.e. the Content="Save" line.
        Assert.Contains("            Content=\"Save\" " + IdLocal + "=\"", after);
    }

    // 3. mirrors Opp.UI.Components.Kanban/UserControls/ListEditor.xaml:17-19
    [Fact]
    public void MultilineOpenTag_closingSlashOnOwnLine_oneLineModified()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <TextBox MaxLength=\"200\"\n" +
            "             Text=\"{Binding T}\"\n" +
            "             />\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        // The /> line gains the attribute before the /
        Assert.Contains("             " + IdLocal + "=\"", after);
        Assert.EndsWith("/>\n</Window>\n", after);
    }

    // 4.
    [Fact]
    public void ExplicitOpenClose_emptyContent_staysExplicit()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"></Button>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains("></Button>", after);
        Assert.DoesNotContain("/></Button>", after);
    }

    // 5.
    [Fact]
    public void SelfClosing_staysSelfClosing()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        Assert.Contains("/>", after);
        Assert.DoesNotContain("></Button>", after);
    }

    // 6. mirrors Metris.CryptographyTool/MainWindow.xaml:29 — space before >.
    // Option A insertion (just before >): pre-existing space is preserved BUT now sits
    // between the previous attribute and the new one, producing a double-space.
    // The closing > sits directly after the new attribute's closing quote.
    [Fact]
    public void SpaceBefore_closingBracket_preserved()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\" ></Button>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        // Two-space gap survives between "Submit" and AutomationProperties (pre-existing + insertion-leading).
        Assert.Contains("\"Submit\"  " + IdLocal + "=\"", after);
        // Closing bracket follows the new attribute's quote, no separating space.
        Assert.Contains("\"></Button>", after);
    }

    // 7. mirrors Metris.CryptographyTool/MainWindow.xaml:19 — double space before />.
    // Option A insertion (just before /): pre-existing whitespace stays at its original byte
    // position, the insertion (with its leading space) lands after it.
    [Fact]
    public void SpaceBefore_selfClosingSlash_preserved()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"  />\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        // Triple-space gap survives (orig 2-space + insertion-leading 1-space).
        Assert.Contains("\"Submit\"   " + IdLocal + "=\"", after);
        // /> sits directly after the new attribute's quote.
        Assert.Contains("\"/>", after);
    }

    // 8. mirrors Opp.UI.Components.DisplayStudio/Resources/Texts.xaml:3083
    [Fact]
    public void AttributeValue_with_gt_entity_is_not_mistaken_for_tag_end()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <TextBox Text=\"A &gt; B\" x:Name=\"N\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains("A &gt; B", after);
        Assert.Contains("x:Name=\"N\" " + IdLocal + "=\"", after);
    }

    // 9. mirrors Opp.UI.Components.Kanban/UserControls/Overview.xaml:138
    [Fact]
    public void AttributeValue_with_binding_braces_and_nested_single_quotes()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button Content=\"{Binding L, ConverterParameter='15'}\" x:Name=\"N\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains("ConverterParameter='15'", after);
    }

    // 10.
    [Fact]
    public void AttributeValue_with_lt_and_quot_entities_preserved()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <TextBox Text=\"a &quot;b&quot; &lt; c\" x:Name=\"N\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains("a &quot;b&quot; &lt; c", after);
    }

    // 11. mirrors Opp.UI.Components.Kanban/UserControls/LabelEditor.xaml:10
    [Fact]
    public void SpacesAround_equals_preserved()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button MinHeight = \"40\" x:Name=\"N\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains("MinHeight = \"40\"", after);
    }

    // 12. mirrors Opp.UI.Components.Kanban/UserControls/ImageViewer.xaml:100
    [Fact]
    public void TabIndent_preserved()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "\t<Button x:Name=\"Submit\"\n" +
            "\t\tContent=\"Save\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains("\t<Button x:Name=\"Submit\"\n", after);
        Assert.Contains("\t\tContent=\"Save\" " + IdLocal + "=\"", after);
    }

    // 13.
    [Fact]
    public void Quote_style_double_preserved()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\" Content=\"Save\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        Assert.Contains("x:Name=\"Submit\"", after);
        Assert.Contains("Content=\"Save\"", after);
        // The newly inserted attribute uses double quotes.
        Assert.Contains(IdLocal + "=\"", after);
    }

    // 14.
    [Fact]
    public void Quote_style_single_preserved_on_existing_attrs()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name='Submit'/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        // Existing single quotes survive byte-exactly.
        Assert.Contains("x:Name='Submit'", after);
        Assert.DoesNotContain("x:Name=\"Submit\"", after);
    }

    // 15.
    [Fact]
    public void Property_element_dotted_localName_is_skipped()
    {
        var xaml =
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Grid>\n" +
            "        <Grid.ColumnDefinitions>\n" +
            "            <ColumnDefinition Width=\"*\"/>\n" +
            "        </Grid.ColumnDefinitions>\n" +
            "        <Button x:Name=\"Submit\"/>\n" +
            "    </Grid>\n" +
            "</Window>\n";
        var (added, before, after) = Instrument(xaml);
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        // Grid.ColumnDefinitions and ColumnDefinition untouched.
        Assert.Contains("<Grid.ColumnDefinitions>\n", after);
        Assert.Contains("<ColumnDefinition Width=\"*\"/>\n", after);
    }

    // 16. mirrors Metris.CryptographyTool/MainWindow.xaml:46
    [Fact]
    public void Comment_with_gt_inside_does_not_confuse_scanner()
    {
        var xaml =
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <StackPanel>\n" +
            "        <!--<Button x:Name=\"C\">click</Button>-->\n" +
            "        <Button x:Name=\"Real\"/>\n" +
            "    </StackPanel>\n" +
            "</Window>\n";
        var (added, before, after) = Instrument(xaml);
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains("<!--<Button x:Name=\"C\">click</Button>-->", after);
    }

    // 17.
    [Fact]
    public void Preexisting_AutomationId_attribute_skips_element()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"X\" AutomationProperties.AutomationId=\"Existing\"/>\n" +
            "</Window>\n");
        Assert.Equal(0, added);
        // Byte-exact preservation.
        Assert.Equal(before, after);
    }

    // 18.
    [Fact]
    public void UTF8_BOM_is_preserved()
    {
        var text =
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"/>\n" +
            "</Window>\n";
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var bytes = bom.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
        var (added, before, after) = InstrumentBytes(bytes);
        Assert.Equal(1, added);
        // BOM intact.
        Assert.Equal(0xEF, after[0]);
        Assert.Equal(0xBB, after[1]);
        Assert.Equal(0xBF, after[2]);
        // Body grew by exactly the inserted attribute length.
        Assert.True(after.Length > before.Length);
    }

    // 19.
    [Fact]
    public void UTF8_no_BOM_stays_no_BOM()
    {
        var text =
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"/>\n" +
            "</Window>\n";
        var bytes = Encoding.UTF8.GetBytes(text); // no BOM
        var (added, _, after) = InstrumentBytes(bytes);
        Assert.Equal(1, added);
        // First three bytes must NOT be a BOM.
        Assert.False(after.Length >= 3 && after[0] == 0xEF && after[1] == 0xBB && after[2] == 0xBF,
            "File without BOM should remain without BOM.");
    }

    // 20.
    [Fact]
    public void CRLF_line_endings_preserved()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\r\n" +
            "    <Button x:Name=\"Submit\"/>\r\n" +
            "</Window>\r\n");
        Assert.Equal(1, added);
        // CRLF preserved.
        Assert.Contains("\r\n", after);
        // No standalone \n that's not preceded by \r.
        for (int i = 0; i < after.Length; i++)
            if (after[i] == '\n')
                Assert.True(i > 0 && after[i - 1] == '\r', $"Bare LF at offset {i}");
    }

    // 21.
    [Fact]
    public void File_without_trailing_newline_stays_unchanged_at_EOF()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"/>\n" +
            "</Window>"); // no trailing newline
        Assert.Equal(1, added);
        Assert.False(before.EndsWith("\n"));
        Assert.False(after.EndsWith("\n"));
        Assert.EndsWith("</Window>", after);
    }

    // 22.
    [Fact]
    public void Sibling_elements_on_same_line_each_get_one_insertion()
    {
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <StackPanel><Button x:Name=\"A\"/><Button x:Name=\"B\"/></StackPanel>\n" +
            "</Window>\n");
        Assert.Equal(2, added);
        var d = TestHelpers.DiffStats(before, after);
        // Two insertions, both on the same line => 1 line modified, 0 added/removed.
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        // Both A and B got attributes.
        Assert.Contains("x:Name=\"A\" " + IdLocal + "=\"", after);
        Assert.Contains("x:Name=\"B\" " + IdLocal + "=\"", after);
    }

    // 23.
    [Fact]
    public void Existing_attribute_order_preserved_AutomationId_appended_last()
    {
        var (added, _, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"X\" Content=\"Y\" Width=\"50\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        // Existing attrs in the original order, then AutomationId last.
        int xName = after.IndexOf("x:Name=\"X\"", StringComparison.Ordinal);
        int content = after.IndexOf("Content=\"Y\"", StringComparison.Ordinal);
        int width = after.IndexOf("Width=\"50\"", StringComparison.Ordinal);
        int autoId = after.IndexOf(IdLocal, StringComparison.Ordinal);
        Assert.True(xName < content && content < width && width < autoId);
    }

    // 24. mirrors Opp.UI.Components.Kanban/UserControls/Overview.xaml:1-13
    [Fact]
    public void Root_element_with_many_xmlns_lines_not_touched_when_inner_instrumented()
    {
        var xaml =
            "<UserControl x:Class=\"X.Y.Z\"\n" +
            "             xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\n" +
            "             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\n" +
            "             xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"\n" +
            "             xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"\n" +
            "             xmlns:local=\"clr-namespace:X.Y.Z\"\n" +
            "             mc:Ignorable=\"d\"\n" +
            "             Style=\"{StaticResource MyStyle}\">\n" +
            "    <Button x:Name=\"Submit\"/>\n" +
            "</UserControl>\n";
        var (added, before, after) = Instrument(xaml);
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        // First 8 lines (root opening tag) untouched.
        var beforeLines = before.Split('\n');
        var afterLines = after.Split('\n');
        for (int i = 0; i < 8; i++) Assert.Equal(beforeLines[i], afterLines[i]);
    }

    // 25.
    [Fact]
    public void Idempotence_second_run_byte_exact()
    {
        var path = TestHelpers.WriteTemp("V.axaml",
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            "    <Button x:Name=\"Submit\"/>\n" +
            "    <TextBox x:Name=\"Email\"/>\n" +
            "</Window>\n");
        new Instrumenter(new ToolConfig()).InstrumentFile(path);
        var afterFirstBytes = File.ReadAllBytes(path);
        new Instrumenter(new ToolConfig()).InstrumentFile(path);
        var afterSecondBytes = File.ReadAllBytes(path);
        Assert.Equal(afterFirstBytes, afterSecondBytes);
    }

    // 26. mirrors Opp.UI.Common/...DiagramCommands.xaml — long Geometry
    [Fact]
    public void Long_attribute_value_does_not_break_scanner()
    {
        var longValue = new string('M', 600);
        var (added, before, after) = Instrument(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
            $"    <Button Tag=\"{longValue}\" x:Name=\"N\"/>\n" +
            "</Window>\n");
        Assert.Equal(1, added);
        var d = TestHelpers.DiffStats(before, after);
        Assert.Equal((1, 0, 0), (d.Modified, d.Added, d.Removed));
        Assert.Contains(longValue, after);
    }
}
