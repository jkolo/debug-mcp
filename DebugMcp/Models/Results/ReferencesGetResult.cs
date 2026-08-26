using DebugMcp.Models.Memory;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>references_get</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record ReferencesGetResult(
    bool Success,
    ReferencesInfo? References = null,
    ToolError? Error = null);

/// <summary>
/// The <c>references</c> object nested under a successful <c>references_get</c> result.
/// <see cref="Outbound"/> reuses <see cref="DebugMcp.Models.Memory.ReferenceInfo"/> directly — it
/// already matches the legacy anonymous shape field-for-field, including
/// <c>referenceType</c> serializing as the un-lowered enum name via <c>JsonStringEnumConverter</c>,
/// exactly matching the legacy tool's <c>r.ReferenceType.ToString()</c>.
/// <see cref="Inbound"/>/<see cref="InboundCount"/>/<see cref="InboundNote"/> are only populated
/// when <c>direction</c> is <c>inbound</c> or <c>both</c> — the legacy tool omitted these keys
/// entirely for the default <c>outbound</c> direction.
/// </summary>
public sealed record ReferencesInfo(
    string TargetAddress,
    string TargetType,
    IReadOnlyList<ReferenceInfo> Outbound,
    int OutboundCount,
    bool Truncated,
    IReadOnlyList<ReferenceInfo>? Inbound = null,
    int? InboundCount = null,
    string? InboundNote = null);
