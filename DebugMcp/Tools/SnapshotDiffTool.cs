using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services.Snapshots;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for comparing two snapshots and returning structured differences.
/// </summary>
[McpServerToolType]
public sealed class SnapshotDiffTool
{
    private readonly ISnapshotService _snapshotService;
    private readonly ILogger<SnapshotDiffTool> _logger;

    public SnapshotDiffTool(
        ISnapshotService snapshotService,
        ILogger<SnapshotDiffTool> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }

    /// <summary>
    /// Compare two snapshots and return structured differences (added, removed, modified variables).
    /// </summary>
    [McpServerTool(Name = "snapshot_diff", Title = "Compare Two Snapshots",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Compare two snapshots and return structured differences (added, removed, modified variables with before/after values)")]
    public Task<SnapshotDiffResult> DiffSnapshotsAsync(
        [Description("First snapshot ID (baseline)")]
        string snapshot_id_1,
        [Description("Second snapshot ID (comparison)")]
        string snapshot_id_2,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("snapshot_diff", JsonSerializer.Serialize(new { snapshot_id_1, snapshot_id_2 }));

        try
        {
            var diff = _snapshotService.DiffSnapshots(snapshot_id_1, snapshot_id_2);

            stopwatch.Stop();
            _logger.ToolCompleted("snapshot_diff", stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new SnapshotDiffResult(
                Success: true,
                Diff: new SnapshotDiffInfo(
                    SnapshotIdA: diff.SnapshotIdA,
                    SnapshotIdB: diff.SnapshotIdB,
                    ThreadMismatch: diff.ThreadMismatch,
                    TimeDelta: diff.TimeDelta.ToString(),
                    Summary: new SnapshotDiffSummary(
                        Added: diff.Added.Count,
                        Removed: diff.Removed.Count,
                        Modified: diff.Modified.Count,
                        Unchanged: diff.Unchanged),
                    Added: diff.Added.Select(e => new SnapshotDiffValueEntry(e.Name, e.Path, e.Type, e.NewValue)).ToList(),
                    Removed: diff.Removed.Select(e => new SnapshotDiffValueEntry(e.Name, e.Path, e.Type, e.OldValue)).ToList(),
                    Modified: diff.Modified.Select(e => new SnapshotDiffModifiedEntry(e.Name, e.Path, e.Type, e.OldValue, e.NewValue)).ToList())));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.ToolError("snapshot_diff", ErrorCodes.SnapshotNotFound);
            return Task.FromResult(new SnapshotDiffResult(
                Success: false,
                Error: new ToolError(ErrorCodes.SnapshotNotFound, ex.Message)));
        }
        catch (Exception ex)
        {
            _logger.ToolError("snapshot_diff", ErrorCodes.VariablesFailed);
            return Task.FromResult(new SnapshotDiffResult(
                Success: false,
                Error: new ToolError(
                    ErrorCodes.VariablesFailed,
                    $"Failed to diff snapshots: {ex.Message}",
                    new { exceptionType = ex.GetType().Name })));
        }
    }
}
