using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Services.Snapshots;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for listing all snapshots in the current debug session.
/// </summary>
[McpServerToolType]
public sealed class SnapshotListTool
{
    private readonly ISnapshotService _snapshotService;
    private readonly ILogger<SnapshotListTool> _logger;

    public SnapshotListTool(
        ISnapshotService snapshotService,
        ILogger<SnapshotListTool> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }

    /// <summary>
    /// List all snapshots in the current debug session with their metadata.
    /// </summary>
    [McpServerTool(Name = "snapshot_list", Title = "List Snapshots",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all snapshots in the current debug session with their metadata")]
    public string ListSnapshots()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("snapshot_list", "{}");

        var snapshots = _snapshotService.ListSnapshots();

        stopwatch.Stop();
        _logger.ToolCompleted("snapshot_list", stopwatch.ElapsedMilliseconds);

        return JsonSerializer.Serialize(new
        {
            success = true,
            snapshots = snapshots.Select(s => new
            {
                id = s.Id,
                label = s.Label,
                timestamp = s.CreatedAt,
                threadId = s.ThreadId,
                functionName = s.FunctionName,
                variableCount = s.Variables.Count
            }),
            count = snapshots.Count
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
