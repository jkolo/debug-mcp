namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>snapshot_diff</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record SnapshotDiffResult(
    bool Success,
    SnapshotDiffInfo? Diff = null,
    ToolError? Error = null);

/// <summary>The nested <c>diff</c> object emitted on success.</summary>
public sealed record SnapshotDiffInfo(
    string SnapshotIdA,
    string SnapshotIdB,
    bool ThreadMismatch,
    string TimeDelta,
    SnapshotDiffSummary Summary,
    IReadOnlyList<SnapshotDiffValueEntry> Added,
    IReadOnlyList<SnapshotDiffValueEntry> Removed,
    IReadOnlyList<SnapshotDiffModifiedEntry> Modified);

/// <summary>The nested <c>diff.summary</c> object.</summary>
public sealed record SnapshotDiffSummary(int Added, int Removed, int Modified, int Unchanged);

/// <summary>An entry in <c>diff.added</c> or <c>diff.removed</c> — single-sided value.</summary>
public sealed record SnapshotDiffValueEntry(string Name, string Path, string Type, string? Value);

/// <summary>An entry in <c>diff.modified</c> — before/after values.</summary>
public sealed record SnapshotDiffModifiedEntry(string Name, string Path, string Type, string? OldValue, string? NewValue);
