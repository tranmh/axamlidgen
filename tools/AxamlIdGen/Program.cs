using AxamlIdGen.Cli;
using AxamlIdGen.Models;

if (args.Length == 0)
{
    PrintUsage();
    return 64;
}

string command = args[0];
string? root = null;
string? configPath = null;

for (int i = 1; i < args.Length; i++)
{
    var a = args[i];
    if (a == "--config" && i + 1 < args.Length) configPath = args[++i];
    else if (a == "--help" || a == "-h") { PrintUsage(); return 0; }
    else if (root == null) root = a;
    else { Console.Error.WriteLine($"Unexpected argument: {a}"); return 64; }
}

if (root == null)
{
    Console.Error.WriteLine("Missing <root> argument.");
    PrintUsage();
    return 64;
}

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Root directory does not exist: {root}");
    return 2;
}

if (configPath == null)
{
    var defaultPath = Path.Combine(root, "axamlidgen.config.json");
    if (File.Exists(defaultPath)) configPath = defaultPath;
    else
    {
        var rootCandidate = Path.Combine(Directory.GetCurrentDirectory(), "axamlidgen.config.json");
        if (File.Exists(rootCandidate)) configPath = rootCandidate;
    }
}

ToolConfig config;
try
{
    config = ToolConfig.LoadOrDefault(configPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load config: {ex.Message}");
    return 2;
}

var repoRoot = configPath != null
    ? Path.GetDirectoryName(Path.GetFullPath(configPath))!
    : Directory.GetCurrentDirectory();

try
{
    return command switch
    {
        "scan"  => RunScan(root, config),
        "check" => RunCheck(root, repoRoot, config),
        "write" => RunWrite(root, repoRoot, config),
        "emit"  => RunEmit(root, repoRoot, config),
        _ => UnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unhandled error: {ex.Message}");
    return 2;
}

static int RunScan(string root, ToolConfig config)
{
    var result = Pipeline.Run(root, config, writeBack: false);
    PrintSummary(result, "scan");
    PrintDiagnostics(result);
    return ExitCodeForDiagnostics(result);
}

static int RunCheck(string root, string repoRoot, ToolConfig config)
{
    var result = Pipeline.Run(root, config, writeBack: false);
    PrintSummary(result, "check");
    PrintDiagnostics(result);
    var errCode = ExitCodeForDiagnostics(result);
    if (errCode != 0) return errCode;

    var (jsonChanged, csChanged, tsChanged) = CompareManifests(result, repoRoot, config);
    if (jsonChanged || csChanged || tsChanged)
    {
        Console.Error.WriteLine("ERROR: Generated manifests are out of date. Run `AxamlIdGen write` or `emit`.");
        if (jsonChanged) Console.Error.WriteLine($"  - {config.Outputs.Json}");
        if (csChanged)   Console.Error.WriteLine($"  - {config.Outputs.CSharp}");
        if (tsChanged)   Console.Error.WriteLine($"  - {config.Outputs.TypeScript}");
        return 3;
    }
    return 0;
}

static int RunWrite(string root, string repoRoot, ToolConfig config)
{
    var result = Pipeline.Run(root, config, writeBack: true);
    PrintSummary(result, "write");
    PrintDiagnostics(result);
    var errCode = ExitCodeForDiagnostics(result);
    if (errCode != 0) return errCode;

    var changed = Pipeline.FlushManifests(result, repoRoot, config.Outputs);
    Console.WriteLine($"Manifests written: json={changed.Json} cs={changed.CSharp} ts={changed.Ts}");
    return 0;
}

static int RunEmit(string root, string repoRoot, ToolConfig config)
{
    var result = Pipeline.Run(root, config, writeBack: false);
    var errCode = ExitCodeForDiagnostics(result);
    if (errCode != 0)
    {
        PrintDiagnostics(result);
        return errCode;
    }
    var changed = Pipeline.FlushManifests(result, repoRoot, config.Outputs);
    Console.WriteLine($"Manifests written: json={changed.Json} cs={changed.CSharp} ts={changed.Ts}");
    return 0;
}

static int ExitCodeForDiagnostics(PipelineResult result)
{
    if (result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) return 1;
    return 0;
}

static (bool, bool, bool) CompareManifests(PipelineResult result, string root, ToolConfig config)
{
    string Resolve(string p) => Path.IsPathRooted(p) ? p : Path.Combine(root, p);
    bool Diff(string path, string expected) =>
        !File.Exists(path) || File.ReadAllText(path) != expected;
    return (
        Diff(Resolve(config.Outputs.Json), result.JsonManifest),
        Diff(Resolve(config.Outputs.CSharp), result.CSharpManifest),
        Diff(Resolve(config.Outputs.TypeScript), result.TypeScriptManifest)
    );
}

static void PrintSummary(PipelineResult result, string command)
{
    Console.WriteLine($"AxamlIdGen {command}: {result.FilesScanned} files, {result.Occurrences.Count} ids, {result.Synthesized} synthesized, {result.Duration.TotalMilliseconds:F0}ms");
}

static void PrintDiagnostics(PipelineResult result)
{
    foreach (var d in result.Diagnostics.OrderBy(d => d.File).ThenBy(d => d.Line))
    {
        var severity = d.Severity == DiagnosticSeverity.Error ? "error" : "warning";
        Console.Error.WriteLine($"{d.File}:{d.Line}:{d.Column}: {severity} {d.Code}: {d.Message}");
    }
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintUsage();
    return 64;
}

static void PrintUsage()
{
    Console.WriteLine("AxamlIdGen — XAML AutomationId codegen tool");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  AxamlIdGen scan   <root> [--config <path>]   Read-only scan; prints diagnostics.");
    Console.WriteLine("  AxamlIdGen check  <root> [--config <path>]   Like scan + assert manifests up to date.");
    Console.WriteLine("  AxamlIdGen write  <root> [--config <path>]   Replace sentinels in XAML, regenerate manifests.");
    Console.WriteLine("  AxamlIdGen emit   <root> [--config <path>]   Regenerate manifests only.");
    Console.WriteLine();
    Console.WriteLine("Exit codes: 0 clean | 1 validation errors | 2 IO/parse errors | 3 manifests stale.");
}
