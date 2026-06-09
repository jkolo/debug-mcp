namespace DebugMcp.Services.Snapshots;

/// <summary>
/// Describes what changed in the snapshot store.
/// </summary>
public enum SnapshotChangedKind
{
    Added,
    Removed,
    Cleared
}

/// <summary>
/// Event args for ISnapshotStore.Changed notifications.
/// </summary>
public sealed class SnapshotChangedEventArgs : EventArgs
{
    /// <summary>What kind of change occurred.</summary>
    public required SnapshotChangedKind Kind { get; init; }

    /// <summary>Affected snapshot ID (null for Cleared).</summary>
    public string? SnapshotId { get; init; }
}
