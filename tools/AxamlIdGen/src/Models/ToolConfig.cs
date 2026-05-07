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
