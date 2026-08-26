namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>object_summarize</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record ObjectSummarizeResult(
    bool Success,
    ObjectSummaryResult? Summary = null,
    ToolError? Error = null);

/// <summary>The <c>summary</c> payload.</summary>
public sealed record ObjectSummaryResult(
    string TypeName,
    int Size,
    bool IsNull,
    int TotalFieldCount,
    int InaccessibleFieldCount,
    IReadOnlyList<FieldSummaryResult> Fields,
    IReadOnlyList<string> NullFields,
    IReadOnlyList<InterestingFieldResult> InterestingFields);

/// <summary>Summary of a single non-default, non-null object field.</summary>
public sealed record FieldSummaryResult(
    string Name,
    string Type,
    string Value,
    int? CollectionCount = null,
    string? CollectionElementType = null);

/// <summary>A field flagged for an anomalous value.</summary>
public sealed record InterestingFieldResult(
    string Name,
    string Type,
    string Value,
    string Reason);
