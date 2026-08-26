using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services.Snapshots;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for deleting snapshots.
/// </summary>
[McpServerToolType]
public sealed class SnapshotDeleteTool
{
    private readonly ISnapshotService _snapshotService;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ILogger<SnapshotDeleteTool> _logger;

    public SnapshotDeleteTool(
        ISnapshotService snapshotService,
        ISnapshotStore snapshotStore,
        ILogger<SnapshotDeleteTool> logger)
    {
        _snapshotService = snapshotService;
        _snapshotStore = snapshotStore;
        _logger = logger;
    }

    /// <summary>
    /// Delete a specific snapshot by ID, or clear all snapshots if no ID is provided.
    /// </summary>
    [McpServerTool(Name = "snapshot_delete", Title = "Delete Snapshot(s)",
        ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Delete a specific snapshot by ID, or clear all snapshots if no ID is provided")]
    public Task<SnapshotDeleteResult> DeleteSnapshotAsync(
        [Description("Snapshot ID to delete. If omitted, deletes all snapshots.")]
        string? snapshot_id = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("snapshot_delete", JsonSerializer.Serialize(new { snapshot_id }));

        try
        {
            if (snapshot_id != null)
            {
                if (!_snapshotService.DeleteSnapshot(snapshot_id))
                {
                    _logger.ToolError("snapshot_delete", ErrorCodes.SnapshotNotFound);
                    return Task.FromResult(new SnapshotDeleteResult(
                        Success: false,
                        Error: new ToolError(ErrorCodes.SnapshotNotFound, $"Snapshot '{snapshot_id}' not found.")));
                }

                stopwatch.Stop();
                _logger.ToolCompleted("snapshot_delete", stopwatch.ElapsedMilliseconds);

                return Task.FromResult(new SnapshotDeleteResult(
                    Success: true,
                    Deleted: snapshot_id,
                    Remaining: _snapshotStore.Count));
            }
            else
            {
                _snapshotService.ClearAll();

                stopwatch.Stop();
                _logger.ToolCompleted("snapshot_delete", stopwatch.ElapsedMilliseconds);

                return Task.FromResult(new SnapshotDeleteResult(
                    Success: true,
                    Deleted: "all",
                    Remaining: 0));
            }
        }
        catch (Exception ex)
        {
            _logger.ToolError("snapshot_delete", ErrorCodes.VariablesFailed);
            return Task.FromResult(new SnapshotDeleteResult(
                Success: false,
                Error: new ToolError(
                    ErrorCodes.VariablesFailed,
                    $"Failed to delete snapshot: {ex.Message}",
                    new { exceptionType = ex.GetType().Name })));
        }
    }
}
