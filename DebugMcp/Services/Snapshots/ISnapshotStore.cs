using DebugMcp.Models.Snapshots;

namespace DebugMcp.Services.Snapshots;

/// <summary>
/// Thread-safe storage for debug state snapshots.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// Adds a snapshot to the store.
    /// </summary>
    /// <returns>True if added, false if ID already exists.</returns>
    bool Add(Snapshot snapshot);

    /// <summary>
    /// Gets a snapshot by ID.
    /// </summary>
    /// <returns>The snapshot, or null if not found.</returns>
    Snapshot? Get(string id);

    /// <summary>
    /// Gets all snapshots in the store.
    /// </summary>
    IReadOnlyList<Snapshot> GetAll();

    /// <summary>
    /// Removes a snapshot by ID.
    /// </summary>
    /// <returns>True if removed, false if not found.</returns>
    bool Remove(string id);

    /// <summary>
    /// Removes all snapshots from the store.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the number of snapshots in the store.
    /// </summary>
    int Count { get; }
}
