using System.Text.RegularExpressions;
using AxamlIdGen.Models;

namespace AxamlIdGen.Validation;

public sealed class NamingValidator
{
    private readonly Regex _pattern;

    public NamingValidator(string pattern)
    {
        _pattern = new Regex(pattern, RegexOptions.Compiled);
    }

    public List<Diagnostic> Check(IEnumerable<AutomationIdOccurrence> occurrences)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var occ in occurrences)
        {
            if (occ.FinalId == null) continue;
            // Naming convention applies to instance-scope IDs by default.
            // Template-scoped IDs are often historical/short (e.g. "VerticalScrollBar")
            // and shouldn't be force-renamed by this tool.
            if (occ.Scope != Scope.Instance) continue;
            if (!_pattern.IsMatch(occ.FinalId))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    "AID003",
                    $"AutomationId '{occ.FinalId}' does not match naming pattern '{_pattern}'",
                    occ.File, occ.Line, occ.Column));
            }
        }
        return diagnostics;
    }
}
