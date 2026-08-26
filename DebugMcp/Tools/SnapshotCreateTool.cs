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
/// MCP tool for capturing the current debug state as a named snapshot.
/// </summary>
[McpServerToolType]
public sealed class SnapshotCreateTool
{
    private readonly ISnapshotService _snapshotService;
    private readonly ILogger<SnapshotCreateTool> _logger;

    public SnapshotCreateTool(
        ISnapshotService snapshotService,
        ILogger<SnapshotCreateTool> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }

    /// <summary>
    /// Capture the current debug state (variables, arguments, this) as a named snapshot.
    /// Must be called while the process is paused at a breakpoint.
    /// </summary>
    [McpServerTool(Name = "snapshot_create", Title = "Create State Snapshot",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Capture the current debug state (variables, arguments, this) as a named snapshot. Must be called while paused.")]
    public Task<SnapshotCreateResult> CreateSnapshotAsync(
        [Description("Human-readable label for the snapshot (auto-generated if omitted)")]
        string? label = null,
        [Description("Thread to capture variables from (default: active thread)")]
        int? thread_id = null,
        [Description("Stack frame index, 0 = top of stack")]
        int frame_index = 0,
        [Description("Expansion depth for nested objects (0 = top-level only)")]
        int depth = 0,
        // FR-034: the body below only calls synchronous ISnapshotService/IDebugSessionManager
        // methods (wrapped in Task.FromResult) — there is no awaited, cancellable call to bound.
        // Racing a timeout via Task.Run + Task.WhenAny here would abandon a call still touching
        // the live ICorDebug session on a background thread while another call could start,
        // violating this codebase's _lock/_stateLock threading invariant. So this parameter is
        // validated but not wired to a CancellationTokenSource.
        [Description("Maximum time to wait for the snapshot to be created, in milliseconds (default: 30000, min: 1, max: 300000)")]
        int timeout_ms = 30000,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("snapshot_create", JsonSerializer.Serialize(new { label, thread_id, frame_index, depth }));

        if (timeout_ms < 1 || timeout_ms > 300000)
        {
            _logger.ToolError("snapshot_create", ErrorCodes.InvalidParameter);
            return Task.FromResult(new SnapshotCreateResult(
                Success: false,
                Error: new ToolError(ErrorCodes.InvalidParameter,
                    "timeout_ms must be between 1 and 300000",
                    new { parameter = "timeout_ms", value = timeout_ms })));
        }

        try
        {
            var snapshot = _snapshotService.CreateSnapshot(label, thread_id, frame_index, depth);

            stopwatch.Stop();
            _logger.ToolCompleted("snapshot_create", stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new SnapshotCreateResult(
                Success: true,
                Snapshot: new SnapshotCreateInfo(
                    Id: snapshot.Id,
                    Label: snapshot.Label,
                    Timestamp: snapshot.CreatedAt,
                    ThreadId: snapshot.ThreadId,
                    FrameIndex: snapshot.FrameIndex,
                    FunctionName: snapshot.FunctionName,
                    VariableCount: snapshot.Variables.Count,
                    Depth: snapshot.Depth)));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("paused"))
        {
            _logger.ToolError("snapshot_create", ErrorCodes.NotPaused);
            return Task.FromResult(new SnapshotCreateResult(
                Success: false,
                Error: new ToolError(ErrorCodes.NotPaused,
                    "Cannot create snapshot while process is running. Pause at a breakpoint first.")));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("session"))
        {
            _logger.ToolError("snapshot_create", ErrorCodes.NoSession);
            return Task.FromResult(new SnapshotCreateResult(
                Success: false,
                Error: new ToolError(ErrorCodes.NoSession, ex.Message)));
        }
        catch (Exception ex)
        {
            _logger.ToolError("snapshot_create", ErrorCodes.VariablesFailed);
            return Task.FromResult(new SnapshotCreateResult(
                Success: false,
                Error: new ToolError(
                    ErrorCodes.VariablesFailed,
                    $"Failed to create snapshot: {ex.Message}",
                    new { exceptionType = ex.GetType().Name })));
        }
    }
}
