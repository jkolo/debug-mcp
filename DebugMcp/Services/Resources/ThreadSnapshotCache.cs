using DebugMcp.Models;
using DebugMcp.Models.Inspection;

namespace DebugMcp.Services.Resources;

/// <summary>
/// Caches the last-known thread list for serving stale data when process is running.
/// Updated on each pause event with fresh thread information.
/// </summary>
public sealed class ThreadSnapshotCache
{
    /// <summary>Gets the cached thread list, or null if no snapshot has been taken.</summary>
    public IReadOnlyList<ThreadInfo>? Threads { get; private set; }

    /// <summary>Gets when the snapshot was captured.</summary>
    public DateTimeOffset? CapturedAt { get; private set; }

    /// <summary>Gets whether a snapshot exists.</summary>
    public bool HasSnapshot => Threads != null;

    /// <summary>
    /// Returns whether the cached data is stale given the current session state.
    /// Stale when process is running and a snapshot exists.
    /// </summary>
    public bool IsStale(SessionState currentState)
    {
        if (!HasSnapshot) return false;
        return currentState != SessionState.Paused;
    }

    /// <summary>
    /// Updates the cache with a fresh thread snapshot.
    /// </summary>
    public void Update(IReadOnlyList<ThreadInfo> threads)
    {
        Threads = threads;
        CapturedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Clears the cached snapshot.
    /// </summary>
    public void Clear()
    {
        Threads = null;
        CapturedAt = null;
    }
}
