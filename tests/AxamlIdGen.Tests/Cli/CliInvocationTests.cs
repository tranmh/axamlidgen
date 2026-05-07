using System.Diagnostics;
using Xunit;

namespace AxamlIdGen.Tests.Cli;

public class CliInvocationTests
{
    private static string ToolDll =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "AxamlIdGen", "bin", "Debug", "net10.0", "AxamlIdGen.dll"));

    private static (int ExitCode, string StdOut, string StdErr) Run(params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add(ToolDll);
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout, stderr);
    }

    [Fact]
    public void No_args_prints_usage_and_exits_64()
    {
        var (code, stdout, _) = Run();
        Assert.Equal(64, code);
        Assert.Contains("Usage:", stdout);
    }

    [Fact]
    public void Unknown_command_exits_64()
    {
        var (code, _, _) = Run("frobnicate", "/tmp");
        Assert.Equal(64, code);
    }

    [Fact]
    public void Missing_root_exits_64()
    {
        var (code, _, _) = Run("scan");
        Assert.Equal(64, code);
    }

    [Fact]
    public void Nonexistent_root_exits_2()
    {
        var (code, _, _) = Run("scan", "/no/such/path");
        Assert.Equal(2, code);
    }

    [Fact]
    public void Scan_clean_project_exits_0()
    {
        var dir = MakeProject(("V.axaml",
            $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"View_Submit_Button\"/></Window>"));
        var (code, _, _) = Run("scan", dir);
        Assert.Equal(0, code);
    }

    [Fact]
    public void Scan_with_duplicate_exits_1()
    {
        var dir = MakeProject(
            ("A.axaml", $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"View_Foo_Bar\"/></Window>"),
            ("B.axaml", $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"View_Foo_Bar\"/></Window>"));
        var (code, _, stderr) = Run("scan", dir);
        Assert.Equal(1, code);
        Assert.Contains("AID001", stderr);
    }

    [Fact]
    public void Check_clean_then_stale_manifest_exits_3()
    {
        var dir = MakeProject(("V.axaml",
            $"{TestHelpers.AvaRoot}<Button AutomationProperties.AutomationId=\"View_Submit_Button\"/></Window>"));

        // Use config that points outputs into the same dir.
        var configPath = Path.Combine(dir, "axamlidgen.config.json");
        File.WriteAllText(configPath,
            "{\"outputs\":{\"json\":\"manifest.json\",\"cSharp\":\"Ids.g.cs\",\"typeScript\":\"ids.ts\",\"cSharpNamespace\":\"X\"}}");

        // First emit: should succeed.
        var (codeEmit, _, _) = Run("emit", dir, "--config", configPath);
        Assert.Equal(0, codeEmit);

        // Now check should be clean (0).
        var (codeOk, _, _) = Run("check", dir, "--config", configPath);
        Assert.Equal(0, codeOk);

        // Modify the manifest on disk; check should report stale (3).
        File.WriteAllText(Path.Combine(dir, "manifest.json"), "stale");
        var (codeStale, _, _) = Run("check", dir, "--config", configPath);
        Assert.Equal(3, codeStale);
    }

    [Fact]
    public void Write_replaces_sentinel_in_xaml_file()
    {
        var dir = MakeProject(("Login.axaml",
            $"{TestHelpers.AvaRoot}<Button x:Name=\"Submit\" AutomationProperties.AutomationId=\"@auto\"/></Window>"));

        var (code, _, _) = Run("write", dir);
        Assert.Equal(0, code);
        var content = File.ReadAllText(Path.Combine(dir, "Login.axaml"));
        Assert.Contains("Login_Button_Submit", content);
        Assert.DoesNotContain("@auto", content);
    }

    private static string MakeProject(params (string filename, string contents)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "axamlidgen-cli", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        foreach (var (name, body) in files)
            File.WriteAllText(Path.Combine(dir, name), body);
        return dir;
    }
}
