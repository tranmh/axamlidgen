namespace AxamlIdGen.Models;

public enum ProvenanceSource
{
    Explicit,
    XName,           // Tier 1
    Content,         // Tier 2
    PropertyName,    // Tier 3
    Binding,         // Tier 4 — extracted from {Binding PropName}
    GridPosition,    // Tier 5 — Grid.Row/Column under named ancestor
    SubtreeOrdinal,  // Tier 6 — ordinal within nearest named ancestor's subtree
    Hash,            // Tier 7 — SHA1 of element's own attributes
    Ordinal          // legacy file-global ordinal — only emitted when cutoff allows
}
