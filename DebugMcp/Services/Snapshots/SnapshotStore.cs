using System.Collections.Concurrent;
using DebugMcp.Models.Snapshots;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Snapshots;

/// <summary>
/// Thread-safe in-memory storage for debug state snapshots.
/// </summary>
public sealed class SnapshotStore : ISnapshotStore
{
    private readonly ConcurrentDictionary<string, Snapshot> _snapshots = new();
    private readonly ILogger<SnapshotStore> _logger;

    public SnapshotStore(ILogger<SnapshotStore> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool Add(Snapshot snapshot)
    {
        if (_snapshots.TryAdd(snapshot.Id, snapshot))
        {
            _logger.LogDebug("Added snapshot {Id} with label \"{Label}\" ({VariableCount} variables)",
                snapshot.Id, snapshot.Label, snapshot.Variables.Count);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public Snapshot? Get(string id)
    {
        _snapshots.TryGetValue(id, out var snapshot);
        return snapshot;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Snapshot> GetAll()
    {
        return _snapshots.Values.ToList();
    }

    /// <inheritdoc/>
    public bool Remove(string id)
    {
        if (_snapshots.TryRemove(id, out _))
        {
            _logger.LogDebug("Removed snapshot {Id}", id);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        var count = _snapshots.Count;
        _snapshots.Clear();
        _logger.LogInformation("Cleared all snapshots ({Count} removed)", count);
    }

    /// <inheritdoc/>
    public int Count => _snapshots.Count;
}
