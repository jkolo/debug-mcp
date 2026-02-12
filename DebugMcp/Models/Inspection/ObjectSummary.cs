namespace DebugMcp.Models.Inspection;

/// <summary>
/// Result of summarizing an object via object_summarize.
/// </summary>
public sealed record ObjectSummary(
    string TypeName,
    int Size,
    bool IsNull,
    IReadOnlyList<FieldSummary> Fields,
    IReadOnlyList<string> NullFields,
    IReadOnlyList<InterestingField> InterestingFields,
    int InaccessibleFieldCount,
    int TotalFieldCount);

/// <summary>
/// Summary of a single object field.
/// </summary>
public sealed record FieldSummary(
    string Name,
    string Type,
    string Value,
    int? CollectionCount = null,
    string? CollectionElementType = null);

/// <summary>
/// A field flagged for an anomalous value.
/// </summary>
public sealed record InterestingField(
    string Name,
    string Type,
    string Value,
    string Reason);
