using DebugMcp.Models.Snapshots;

namespace DebugMcp.Services.Snapshots;

/// <summary>
/// Orchestrates snapshot capture, comparison, and lifecycle management.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Captures the current debug state as a snapshot.
    /// </summary>
    /// <param name="label">Optional label (auto-generated if null).</param>
    /// <param name="threadId">Thread to capture from (null = active thread).</param>
    /// <param name="frameIndex">Stack frame index (0 = top).</param>
    /// <param name="depth">Expansion depth for nested objects (0 = top-level only).</param>
    /// <returns>The created snapshot.</returns>
    Snapshot CreateSnapshot(string? label = null, int? threadId = null, int frameIndex = 0, int depth = 0);

    /// <summary>
    /// Compares two snapshots and returns structured differences.
    /// </summary>
    /// <param name="snapshotId1">First snapshot ID (baseline).</param>
    /// <param name="snapshotId2">Second snapshot ID (comparison).</param>
    /// <returns>The diff result.</returns>
    SnapshotDiff DiffSnapshots(string snapshotId1, string snapshotId2);

    /// <summary>
    /// Lists all snapshots in the current session.
    /// </summary>
    IReadOnlyList<Snapshot> ListSnapshots();

    /// <summary>
    /// Deletes a specific snapshot by ID.
    /// </summary>
    /// <returns>True if deleted, false if not found.</returns>
    bool DeleteSnapshot(string id);

    /// <summary>
    /// Clears all snapshots.
    /// </summary>
    void ClearAll();
}
