using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Snapshots;

/// <summary>
/// Orchestrates snapshot capture, comparison, and lifecycle management.
/// </summary>
public sealed class SnapshotService : ISnapshotService
{
    private readonly ISnapshotStore _store;
    private readonly IDebugSessionManager _sessionManager;
    private readonly IProcessDebugger _processDebugger;
    private readonly ILogger<SnapshotService> _logger;
    private int _autoLabelCounter;

    public SnapshotService(
        ISnapshotStore store,
        IDebugSessionManager sessionManager,
        IProcessDebugger processDebugger,
        ILogger<SnapshotService> logger)
    {
        _store = store;
        _sessionManager = sessionManager;
        _processDebugger = processDebugger;
        _logger = logger;

        // Subscribe to state changes for session cleanup
        _processDebugger.StateChanged += OnStateChanged;
    }

    /// <inheritdoc/>
    public Snapshot CreateSnapshot(string? label = null, int? threadId = null, int frameIndex = 0, int depth = 0)
    {
        var session = _sessionManager.CurrentSession
            ?? throw new InvalidOperationException("No active debug session.");

        if (session.State != SessionState.Paused)
            throw new InvalidOperationException("Cannot create snapshot while process is not paused. Pause at a breakpoint first.");

        // Capture variables from the specified frame
        var variables = _sessionManager.GetVariables(threadId, frameIndex, "all");

        // Get the function name from the stack frame
        var (frames, _) = _sessionManager.GetStackFrames(threadId, frameIndex, 1);
        var functionName = frames.Count > 0 ? frames[0].Function : "<unknown>";

        // Map variables to snapshot variables, expanding children if depth > 0
        var snapshotVariables = variables.Select(v =>
            MapVariable(v, threadId, frameIndex, depth)).ToList();

        // Generate label if not provided
        var resolvedLabel = label ?? $"snapshot-{Interlocked.Increment(ref _autoLabelCounter)}";

        var resolvedThreadId = threadId ?? session.ActiveThreadId ?? 0;

        var snapshot = new Snapshot(
            Id: $"snap-{Guid.NewGuid()}",
            Label: resolvedLabel,
            CreatedAt: DateTimeOffset.UtcNow,
            ThreadId: resolvedThreadId,
            FrameIndex: frameIndex,
            FunctionName: functionName,
            Depth: depth,
            Variables: snapshotVariables);

        _store.Add(snapshot);

        if (_store.Count >= 100)
            _logger.LogWarning("Snapshot count ({Count}) has reached the soft limit of 100. Consider deleting old snapshots", _store.Count);

        _logger.LogInformation("Created snapshot {Id} \"{Label}\" with {Count} variables at {Function}",
            snapshot.Id, snapshot.Label, snapshot.Variables.Count, snapshot.FunctionName);

        return snapshot;
    }

    /// <inheritdoc/>
    public SnapshotDiff DiffSnapshots(string snapshotId1, string snapshotId2)
    {
        var snapshotA = _store.Get(snapshotId1)
            ?? throw new KeyNotFoundException($"Snapshot '{snapshotId1}' not found. It may have been deleted or the session may have been disconnected.");

        var snapshotB = _store.Get(snapshotId2)
            ?? throw new KeyNotFoundException($"Snapshot '{snapshotId2}' not found. It may have been deleted or the session may have been disconnected.");

        // Build dictionaries keyed by path for O(n) comparison
        var dictA = FlattenVariables(snapshotA.Variables);
        var dictB = FlattenVariables(snapshotB.Variables);

        var added = new List<DiffEntry>();
        var removed = new List<DiffEntry>();
        var modified = new List<DiffEntry>();
        var unchanged = 0;

        // Find removed and modified
        foreach (var (path, varA) in dictA)
        {
            if (dictB.TryGetValue(path, out var varB))
            {
                if (varA.Value != varB.Value)
                {
                    modified.Add(new DiffEntry(varA.Name, path, varA.Type, varA.Value, varB.Value, DiffChangeType.Modified));
                }
                else
                {
                    unchanged++;
                }
            }
            else
            {
                removed.Add(new DiffEntry(varA.Name, path, varA.Type, varA.Value, null, DiffChangeType.Removed));
            }
        }

        // Find added
        foreach (var (path, varB) in dictB)
        {
            if (!dictA.ContainsKey(path))
            {
                added.Add(new DiffEntry(varB.Name, path, varB.Type, null, varB.Value, DiffChangeType.Added));
            }
        }

        var threadMismatch = snapshotA.ThreadId != snapshotB.ThreadId;
        var timeDelta = snapshotB.CreatedAt - snapshotA.CreatedAt;

        _logger.LogInformation("Diffed snapshots {IdA} vs {IdB}: {Added} added, {Removed} removed, {Modified} modified, {Unchanged} unchanged",
            snapshotId1, snapshotId2, added.Count, removed.Count, modified.Count, unchanged);

        return new SnapshotDiff(snapshotId1, snapshotId2, added, removed, modified, threadMismatch, timeDelta, unchanged);
    }

    private static Dictionary<string, SnapshotVariable> FlattenVariables(IReadOnlyList<SnapshotVariable> variables)
    {
        var result = new Dictionary<string, SnapshotVariable>();
        FlattenRecursive(variables, result);
        return result;
    }

    private static void FlattenRecursive(IReadOnlyList<SnapshotVariable>? variables, Dictionary<string, SnapshotVariable> result)
    {
        if (variables == null) return;

        foreach (var v in variables)
        {
            result[v.Path] = v;
            FlattenRecursive(v.Children, result);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Snapshot> ListSnapshots() => _store.GetAll();

    /// <inheritdoc/>
    public bool DeleteSnapshot(string id) => _store.Remove(id);

    /// <inheritdoc/>
    public void ClearAll() => _store.Clear();

    private SnapshotVariable MapVariable(Variable v, int? threadId, int frameIndex, int remainingDepth)
    {
        var path = v.Path ?? v.Name;
        IReadOnlyList<SnapshotVariable>? children = null;

        if (remainingDepth > 0 && v.HasChildren)
        {
            var childVars = _sessionManager.GetVariables(threadId, frameIndex, "all", path);
            children = childVars.Select(c =>
                MapVariable(c, threadId, frameIndex, remainingDepth - 1)).ToList();
        }

        return new SnapshotVariable(
            Name: v.Name,
            Path: path,
            Type: v.Type,
            Value: v.Value,
            Scope: v.Scope,
            Children: children);
    }

    private void OnStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        if (e.NewState == SessionState.Disconnected)
        {
            _logger.LogInformation("Session disconnected — clearing all snapshots");
            _store.Clear();
        }
    }
}
