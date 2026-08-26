namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>snapshot_create</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record SnapshotCreateResult(
    bool Success,
    SnapshotCreateInfo? Snapshot = null,
    ToolError? Error = null);

/// <summary>The nested <c>snapshot</c> object emitted on success.</summary>
public sealed record SnapshotCreateInfo(
    string Id,
    string Label,
    DateTimeOffset Timestamp,
    int ThreadId,
    int FrameIndex,
    string FunctionName,
    int VariableCount,
    int Depth);
