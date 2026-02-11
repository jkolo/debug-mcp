namespace DebugMcp.Models.Snapshots;

/// <summary>
/// The result of comparing two snapshots.
/// </summary>
/// <param name="SnapshotIdA">First snapshot ID (baseline).</param>
/// <param name="SnapshotIdB">Second snapshot ID (comparison).</param>
/// <param name="Added">Variables in B not in A.</param>
/// <param name="Removed">Variables in A not in B.</param>
/// <param name="Modified">Variables in both with different values.</param>
/// <param name="ThreadMismatch">True if snapshots are from different threads.</param>
/// <param name="TimeDelta">Time elapsed between snapshots.</param>
/// <param name="Unchanged">Count of variables that did not change.</param>
public sealed record SnapshotDiff(
    string SnapshotIdA,
    string SnapshotIdB,
    IReadOnlyList<DiffEntry> Added,
    IReadOnlyList<DiffEntry> Removed,
    IReadOnlyList<DiffEntry> Modified,
    bool ThreadMismatch,
    TimeSpan TimeDelta,
    int Unchanged);
