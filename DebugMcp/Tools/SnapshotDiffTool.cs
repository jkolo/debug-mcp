using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
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
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Compare two snapshots and return structured differences (added, removed, modified variables with before/after values)")]
    public string DiffSnapshots(
        [Description("First snapshot ID (baseline)")]
        string snapshot_id_1,
        [Description("Second snapshot ID (comparison)")]
        string snapshot_id_2)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("snapshot_diff", JsonSerializer.Serialize(new { snapshot_id_1, snapshot_id_2 }));

        try
        {
            var diff = _snapshotService.DiffSnapshots(snapshot_id_1, snapshot_id_2);

            stopwatch.Stop();
            _logger.ToolCompleted("snapshot_diff", stopwatch.ElapsedMilliseconds);

            return JsonSerializer.Serialize(new
            {
                success = true,
                diff = new
                {
                    snapshotIdA = diff.SnapshotIdA,
                    snapshotIdB = diff.SnapshotIdB,
                    threadMismatch = diff.ThreadMismatch,
                    timeDelta = diff.TimeDelta.ToString(),
                    summary = new
                    {
                        added = diff.Added.Count,
                        removed = diff.Removed.Count,
                        modified = diff.Modified.Count,
                        unchanged = diff.Unchanged
                    },
                    added = diff.Added.Select(e => new { name = e.Name, path = e.Path, type = e.Type, value = e.NewValue }),
                    removed = diff.Removed.Select(e => new { name = e.Name, path = e.Path, type = e.Type, value = e.OldValue }),
                    modified = diff.Modified.Select(e => new { name = e.Name, path = e.Path, type = e.Type, oldValue = e.OldValue, newValue = e.NewValue })
                }
            }, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.ToolError("snapshot_diff", ErrorCodes.SnapshotNotFound);
            return CreateErrorResponse(ErrorCodes.SnapshotNotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.ToolError("snapshot_diff", ErrorCodes.VariablesFailed);
            return CreateErrorResponse(ErrorCodes.VariablesFailed,
                $"Failed to diff snapshots: {ex.Message}",
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
