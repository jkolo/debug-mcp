using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
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
        ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Delete a specific snapshot by ID, or clear all snapshots if no ID is provided")]
    public string DeleteSnapshot(
        [Description("Snapshot ID to delete. If omitted, deletes all snapshots.")]
        string? snapshot_id = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("snapshot_delete", JsonSerializer.Serialize(new { snapshot_id }));

        try
        {
            if (snapshot_id != null)
            {
                if (!_snapshotService.DeleteSnapshot(snapshot_id))
                {
                    _logger.ToolError("snapshot_delete", ErrorCodes.SnapshotNotFound);
                    return CreateErrorResponse(ErrorCodes.SnapshotNotFound,
                        $"Snapshot '{snapshot_id}' not found.");
                }

                stopwatch.Stop();
                _logger.ToolCompleted("snapshot_delete", stopwatch.ElapsedMilliseconds);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    deleted = snapshot_id,
                    remaining = _snapshotStore.Count
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                _snapshotService.ClearAll();

                stopwatch.Stop();
                _logger.ToolCompleted("snapshot_delete", stopwatch.ElapsedMilliseconds);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    deleted = "all",
                    remaining = 0
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.ToolError("snapshot_delete", ErrorCodes.VariablesFailed);
            return CreateErrorResponse(ErrorCodes.VariablesFailed,
                $"Failed to delete snapshot: {ex.Message}",
                new { exceptionType = ex.GetType().Name });
        }
    }

    private static string CreateErrorResponse(string code, string message, object? details = null)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new { code, message, details }
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
