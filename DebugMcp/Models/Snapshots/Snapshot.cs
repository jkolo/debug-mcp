namespace DebugMcp.Models.Snapshots;

/// <summary>
/// A point-in-time capture of debug state for a single stack frame.
/// </summary>
/// <param name="Id">Unique identifier (snap-{guid}).</param>
/// <param name="Label">User-provided or auto-generated label.</param>
/// <param name="CreatedAt">UTC timestamp of capture.</param>
/// <param name="ThreadId">Thread where snapshot was taken.</param>
/// <param name="FrameIndex">Stack frame index (0 = top).</param>
/// <param name="FunctionName">Fully qualified method name at capture point.</param>
/// <param name="Depth">Expansion depth used (0 = top-level only).</param>
/// <param name="Variables">Captured variable values.</param>
public sealed record Snapshot(
    string Id,
    string Label,
    DateTimeOffset CreatedAt,
    int ThreadId,
    int FrameIndex,
    string FunctionName,
    int Depth,
    IReadOnlyList<SnapshotVariable> Variables);
