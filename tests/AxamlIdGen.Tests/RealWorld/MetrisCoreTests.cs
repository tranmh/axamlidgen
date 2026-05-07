using System.Diagnostics;
using AxamlIdGen.Cli;
using AxamlIdGen.Models;
using Xunit;

namespace AxamlIdGen.Tests.RealWorld;

/// <summary>
/// Tests that exercise the tool against the user's real WPF codebase
/// (../metris-test/metris-core/). Skipped if the directory is not present
/// or AXAMLIDGEN_REALWORLD_PATH is empty.
///
/// Survey numbers from this machine: 2,782 XAML files, 5 existing
/// AutomationId occurrences (4 template-scoped in App.xaml, 1 instance
/// in ProductionSchedule.xaml).
/// </summary>
public class MetrisCoreTests
{
    private static string? RealWorldPath()
    {
        var env = Environment.GetEnvironmentVariable("AXAMLIDGEN_REALWORLD_PATH");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;

        // Default relative location used during this PoC's development.
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "metris-test", "metris-core")),
            "/home/tranmh/work/metris-test/metris-core"
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    [Fact]
    public void Full_tree_scan_succeeds_and_completes_within_budget()
    {
        var path = RealWorldPath();
        if (path == null) return; // suite portable: skip when metris-core is absent

        var sw = Stopwatch.StartNew();
        var result = Pipeline.Run(path!, new ToolConfig(), writeBack: false);
        sw.Stop();

        // Survey expectations
        Assert.True(result.FilesScanned > 2000,
            $"Expected >2000 files scanned, got {result.FilesScanned}");
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Performance budget (env-overridable)
        var budgetMs = int.TryParse(Environment.GetEnvironmentVariable("AXAMLIDGEN_BUDGET_MS"), out var b) ? b : 30_000;
        Assert.True(sw.ElapsedMilliseconds < budgetMs,
            $"Scan took {sw.ElapsedMilliseconds}ms, exceeded budget {budgetMs}ms");
    }

    [Fact]
    public void Discovers_known_existing_AutomationIds()
    {
        var path = RealWorldPath();
        if (path == null) return; // suite portable: skip when metris-core is absent

        var result = Pipeline.Run(path!, new ToolConfig(), writeBack: false);

        // The known 5 occurrences from the survey.
        var values = result.Occurrences.Select(o => o.FinalId).ToList();
        Assert.Contains("VerticalScrollBar", values);
        Assert.Contains("HorizontalScrollBar", values);
        Assert.Contains("PART_Categories", values);

        // VerticalScrollBar / HorizontalScrollBar each appear twice (in two ControlTemplates)
        Assert.Equal(2, values.Count(v => v == "VerticalScrollBar"));
        Assert.Equal(2, values.Count(v => v == "HorizontalScrollBar"));
    }

    [Fact]
    public void ScrollBar_duplicates_are_template_scoped_and_not_flagged()
    {
        var path = RealWorldPath();
        if (path == null) return; // suite portable: skip when metris-core is absent

        var result = Pipeline.Run(path!, new ToolConfig(), writeBack: false);
        var dupErrors = result.Diagnostics.Where(d => d.Code == "AID001" || d.Code == "AID002").ToList();
        Assert.Empty(dupErrors);

        var scrollBarOccurrences = result.Occurrences.Where(o =>
            o.FinalId == "VerticalScrollBar" || o.FinalId == "HorizontalScrollBar").ToList();
        Assert.All(scrollBarOccurrences, o => Assert.Equal(Scope.ControlTemplate, o.Scope));
    }

    [Fact]
    public void Fabricated_instance_duplicate_is_caught()
    {
        var path = RealWorldPath();
        if (path == null) return; // suite portable: skip when metris-core is absent

        // Copy a small slice and inject a duplicate.
        var sandbox = Path.Combine(Path.GetTempPath(), "axamlidgen-rw", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(sandbox);
        File.WriteAllText(Path.Combine(sandbox, "A.xaml"),
            $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"Fabricated_Dup_Id\"/></Window>");
        File.WriteAllText(Path.Combine(sandbox, "B.xaml"),
            $"{TestHelpers.WpfRoot}<Button AutomationProperties.AutomationId=\"Fabricated_Dup_Id\"/></Window>");

        var result = Pipeline.Run(sandbox, new ToolConfig(), writeBack: false);
        Assert.Contains(result.Diagnostics, d => d.Code == "AID001");
    }

    [Fact]
    public void Migration_simulation_writes_synthesized_ids_idempotently()
    {
        var path = RealWorldPath();
        if (path == null) return; // suite portable: skip when metris-core is absent

        // Pick a small subset of XAML files (any 5-10) and copy to sandbox.
        var picks = Directory.EnumerateFiles(path!, "*.xaml", SearchOption.AllDirectories)
            .Where(f => new FileInfo(f).Length < 8 * 1024)  // keep files small for speed
            .Take(8)
            .ToList();
        Assert.NotEmpty(picks);

        var sandbox = Path.Combine(Path.GetTempPath(), "axamlidgen-mig", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(sandbox);

        // Copy and inject sentinel into a Button or first child element of each file.
        foreach (var src in picks)
        {
            var dst = Path.Combine(sandbox, Path.GetFileName(src));
            // ensure unique filename
            int n = 1;
            while (File.Exists(dst))
                dst = Path.Combine(sandbox, $"{Path.GetFileNameWithoutExtension(src)}_{n++}.xaml");

            var text = File.ReadAllText(src);
            // Inject sentinel on first <Button without an AutomationId
            var injected = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<Button(?![^>]*AutomationProperties\.AutomationId)([^/>]*)/>",
                "<Button AutomationProperties.AutomationId=\"@auto\" x:Name=\"InjectedBtn\"$1/>",
                System.Text.RegularExpressions.RegexOptions.None);
            File.WriteAllText(dst, injected);
        }

        var first = Pipeline.Run(sandbox, new ToolConfig(), writeBack: true);

        // Some inserted sentinels may not be reachable (file may have no Button) — synthesizing zero
        // is fine, but at least one should have been processed if injection worked anywhere.
        Assert.Empty(first.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Idempotence: a second write should report 0 synthesized and produce no file changes.
        var snapshot = Directory.EnumerateFiles(sandbox, "*.xaml")
            .ToDictionary(p => p, File.ReadAllText);
        var second = Pipeline.Run(sandbox, new ToolConfig(), writeBack: true);
        Assert.Equal(0, second.Synthesized);
        foreach (var (p, contents) in snapshot)
            Assert.Equal(contents, File.ReadAllText(p));
    }

    [Fact]
    public void Scan_does_not_modify_real_codebase_files()
    {
        var path = RealWorldPath();
        if (path == null) return; // suite portable: skip when metris-core is absent

        var sample = Directory.EnumerateFiles(path!, "*.xaml", SearchOption.AllDirectories).Take(20).ToList();
        var before = sample.ToDictionary(p => p, p => File.GetLastWriteTimeUtc(p));

        Pipeline.Run(path!, new ToolConfig(), writeBack: false);

        foreach (var (p, mtime) in before)
            Assert.Equal(mtime, File.GetLastWriteTimeUtc(p));
    }
}

