using AxamlIdGen.Models;

namespace AxamlIdGen.Validation;

public static class UniquenessChecker
{
    public static List<Diagnostic> Check(IEnumerable<AutomationIdOccurrence> occurrences)
    {
        var diagnostics = new List<Diagnostic>();

        // Instance-scope: globally unique
        var instanceGroups = occurrences
            .Where(o => o.Scope == Scope.Instance && o.FinalId != null)
            .GroupBy(o => o.FinalId!);

        foreach (var g in instanceGroups.Where(g => g.Count() > 1))
        {
            foreach (var occ in g)
            {
                var others = g.Where(x => !ReferenceEquals(x, occ))
                    .Select(x => $"{x.File}:{x.Line}")
                    .ToList();
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    "AID001",
                    $"Duplicate instance-scope AutomationId '{g.Key}' (also at: {string.Join(", ", others)})",
                    occ.File, occ.Line, occ.Column));
            }
        }

        // Template-scope: unique within a single template instance, identified
        // by (file, scope, scope-key — the template's source location).
        // Two sibling <ControlTemplate>s have distinct scope keys, so reusing
        // the same ID across them is not a duplicate.
        var templateGroups = occurrences
            .Where(o => o.Scope != Scope.Instance && o.FinalId != null)
            .GroupBy(o => (o.File, o.Scope, o.TemplateScopeKey, o.FinalId));

        foreach (var g in templateGroups.Where(g => g.Count() > 1))
        {
            foreach (var occ in g)
            {
                var others = g.Where(x => !ReferenceEquals(x, occ))
                    .Select(x => $"{x.File}:{x.Line}")
                    .ToList();
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    "AID002",
                    $"Duplicate AutomationId '{g.Key.FinalId}' within {g.Key.Scope} (also at: {string.Join(", ", others)})",
                    occ.File, occ.Line, occ.Column));
            }
        }

        return diagnostics;
    }
}
