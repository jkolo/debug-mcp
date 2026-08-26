namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>snapshot_delete</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record SnapshotDeleteResult(
    bool Success,
    string? Deleted = null,
    int? Remaining = null,
    ToolError? Error = null);
