namespace AxamlIdGen.Models;

public enum DiagnosticSeverity { Warning, Error }

public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string File,
    int Line,
    int Column);
