namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>object_inspect</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record ObjectInspectResult(
    bool Success,
    ObjectInspectionResult? Inspection = null,
    ToolError? Error = null);

/// <summary>
/// The <c>inspection</c> payload. When the inspected reference is null, only
/// <see cref="IsNull"/> and <see cref="TypeName"/> are populated — everything else stays null and
/// is omitted from the wire, matching the original code's two-shape (null vs. non-null) response.
/// </summary>
public sealed record ObjectInspectionResult(
    bool IsNull,
    string? TypeName = null,
    string? Address = null,
    int? Size = null,
    IReadOnlyList<InspectedFieldResult>? Fields = null,
    bool? HasCircularRef = null,
    bool? Truncated = null);

/// <summary>A single field of an inspected object.</summary>
public sealed record InspectedFieldResult(
    string Name,
    string TypeName,
    string Value,
    int Offset,
    int Size,
    bool HasChildren,
    int? ChildCount = null);
