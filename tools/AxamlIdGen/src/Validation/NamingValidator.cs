using System.Text.RegularExpressions;
using AxamlIdGen.Models;

namespace AxamlIdGen.Validation;

public sealed class NamingValidator
{
    private readonly Regex _pattern;

    // Suffix patterns produced by tiers 5/6/7. Used to recognise tool-synthesized
    // IDs even after a re-parse cycle (which loses Provenance metadata — the
    // round-tripped IDs come back tagged as Explicit).
    private static readonly Regex SyntheticSuffix = new(
        @"_(h_[0-9a-f]{8}|R\d+C\d+|R\d+|C\d+|Under[A-Za-z][A-Za-z0-9]*_\d+)(_N\d+)?$",
        RegexOptions.Compiled);

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
            // Synthesized IDs from tiers 5/6/7 use embedded markers (R0C1, UnderName_N,
            // h_<hex>) that don't match a pascal-style human pattern. Skip those —
            // the markers are tool-emitted and intentional.
            // Two paths recognise them:
            //  1. Provenance is explicitly set during synthesis (within one run).
            //  2. SyntheticSuffix regex detects them after a re-parse cycle that
            //     dropped Provenance metadata back to Explicit.
            if (occ.Provenance is ProvenanceSource.GridPosition
                or ProvenanceSource.SubtreeOrdinal
                or ProvenanceSource.Hash) continue;
            if (SyntheticSuffix.IsMatch(occ.FinalId)) continue;
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
