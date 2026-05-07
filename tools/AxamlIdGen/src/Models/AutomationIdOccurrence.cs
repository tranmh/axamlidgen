using System.Xml.Linq;

namespace AxamlIdGen.Models;

public sealed class AutomationIdOccurrence
{
    public required string File { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required string ControlElementName { get; init; }
    public string? XName { get; init; }
    public string? StaticContent { get; init; }
    public string? OwningPropertyName { get; init; }
    public required Scope Scope { get; init; }
    public string? TemplateTargetType { get; init; }
    public int TemplateScopeKey { get; init; }
    public required OccurrenceState State { get; init; }
    public string? OriginalValue { get; init; }
    public string? FinalId { get; set; }
    public ProvenanceSource Provenance { get; set; }
    public bool CollisionResolved { get; set; }
    public required XElement Element { get; init; }
    public required XAttribute? IdAttribute { get; init; }
    public string ViewName { get; init; } = "";
}
