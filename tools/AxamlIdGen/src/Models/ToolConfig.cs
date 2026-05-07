using System.Text.Json;
using System.Text.Json.Serialization;

namespace AxamlIdGen.Models;

public enum MissingPolicy
{
    Strict,
    Synthesize
}

public sealed class ToolConfig
{
    public string SentinelValue { get; init; } = "@auto";
    public string NamingPattern { get; init; } = "^[A-Z][A-Za-z0-9]+(_[A-Za-z][A-Za-z0-9]*)+$";
    public MissingPolicy MissingPolicy { get; init; } = MissingPolicy.Strict;
    public List<string> IncludeGlobs { get; init; } = new() { "**/*.axaml", "**/*.xaml" };
    public List<string> ExcludeGlobs { get; init; } = new() { "**/bin/**", "**/obj/**", "**/Generated/**" };
    public OutputPaths Outputs { get; init; } = new();
    public int MaxIdLength { get; init; } = 64;

    /// <summary>
    /// Element local-names eligible for `instrument` mode auto-injection.
    /// Element types not in this list are skipped (Grid, Border, Rectangle, etc. — UI plumbing QA doesn't query).
    /// Template-scoped occurrences of these types are also skipped.
    /// </summary>
    public List<string> InstrumentControlTypes { get; init; } = new()
    {
        // Buttons / toggles
        "Button", "RepeatButton", "ToggleButton", "CheckBox", "RadioButton", "ToggleSwitch", "Hyperlink",
        // Text inputs
        "TextBox", "RichTextBox", "PasswordBox", "AutoCompleteBox",
        // Selectors
        "ComboBox", "ListBox", "ListView", "TreeView", "TreeViewItem",
        "TabItem", "MenuItem",
        // Sliders / pickers
        "Slider", "DatePicker", "TimePicker", "CalendarDatePicker",
        // Display
        "TextBlock", "ProgressBar",
        // DataGrids
        "DataGrid",
        // Common Telerik counterparts (extend per project)
        "RadButton", "RadComboBox", "RadGridView", "RadDatePicker", "RadDateTimePicker",
        "RadNumericUpDown", "RadToggleButton", "RadSlider", "RadTabItem", "RadMenuItem",
    };

    public static ToolConfig LoadOrDefault(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return new ToolConfig();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ToolConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid config: {path}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

public sealed class OutputPaths
{
    public string Json { get; init; } = "manifest/automation-ids.json";
    public string CSharp { get; init; } = "SampleApp/Generated/AutomationIds.g.cs";
    public string CSharpNamespace { get; init; } = "SampleApp.Generated";
    public string TypeScript { get; init; } = "e2e/automation-ids.generated.ts";
}
