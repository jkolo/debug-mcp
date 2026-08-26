using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for setting tracepoints (non-blocking observation points) at source locations.
/// </summary>
[McpServerToolType]
public sealed class TracepointSetTool
{
    private readonly IBreakpointManager _breakpointManager;
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<TracepointSetTool> _logger;

    public TracepointSetTool(
        IBreakpointManager breakpointManager,
        IDebugSessionManager sessionManager,
        ILogger<TracepointSetTool> logger)
    {
        _breakpointManager = breakpointManager;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Set a tracepoint (non-blocking observation point) at a source location.
    /// Unlike breakpoints, tracepoints do not pause execution but send notifications when code passes through.
    /// </summary>
    /// <param name="file">Source file path (absolute or relative to project).</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">Optional 1-based column for targeting specific sequence point (lambda/inline).</param>
    /// <param name="log_message">Optional log message template with {expression} placeholders for variable interpolation.</param>
    /// <param name="hit_count_multiple">Notify only every Nth hit (0 = every hit).</param>
    /// <param name="max_notifications">Auto-disable tracepoint after N notifications (0 = unlimited).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tracepoint information or error response.</returns>
    [McpServerTool(Name = "tracepoint_set", Title = "Set Tracepoint",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Set a tracepoint (non-blocking observation point) at a source location. Unlike breakpoints, tracepoints do not pause execution but send notifications when code passes through.")]
    public async Task<TracepointSetResult> SetTracepointAsync(
        [Description("Source file path (absolute or relative to project)")] string file,
        [Description("1-based line number")] int line,
        [Description("1-based column for targeting lambdas/inline statements (optional)")] int? column = null,
        [Description("Log message template with {expression} placeholders for variable interpolation, e.g., \"Counter is {i}, sum is {sum}\"")] string? log_message = null,
        [Description("Send notification every Nth hit (0 = every hit)")] int hit_count_multiple = 0,
        [Description("Auto-disable tracepoint after N notifications (0 = unlimited)")] int max_notifications = 0,
        [Description("Maximum time to wait for the tracepoint to be set/verified, in milliseconds (default: 30000)")] int timeout_ms = 30000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("tracepoint_set", JsonSerializer.Serialize(new { file, line, column, log_message, hit_count_multiple, max_notifications }));

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout_ms));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Validate input parameters
            if (string.IsNullOrWhiteSpace(file))
            {
                _logger.ToolError("tracepoint_set", ErrorCodes.InvalidFile);
                return new TracepointSetResult(
                    Success: false,
                    Error: new ToolError(ErrorCodes.InvalidFile, "File path cannot be empty"));
            }

            if (line < 1)
            {
                _logger.ToolError("tracepoint_set", ErrorCodes.InvalidLine);
                return new TracepointSetResult(
                    Success: false,
                    Error: new ToolError(ErrorCodes.InvalidLine, $"Line must be >= 1, got: {line}"));
            }

            if (column.HasValue && column.Value < 1)
            {
                _logger.ToolError("tracepoint_set", ErrorCodes.InvalidColumn);
                return new TracepointSetResult(
                    Success: false,
                    Error: new ToolError(ErrorCodes.InvalidColumn, $"Column must be >= 1, got: {column}"));
            }

            if (hit_count_multiple < 0)
            {
                _logger.ToolError("tracepoint_set", ErrorCodes.InvalidParameter);
                return new TracepointSetResult(
                    Success: false,
                    Error: new ToolError(ErrorCodes.InvalidParameter, $"hit_count_multiple must be >= 0, got: {hit_count_multiple}"));
            }

            if (max_notifications < 0)
            {
                _logger.ToolError("tracepoint_set", ErrorCodes.InvalidParameter);
                return new TracepointSetResult(
                    Success: false,
                    Error: new ToolError(ErrorCodes.InvalidParameter, $"max_notifications must be >= 0, got: {max_notifications}"));
            }

            // Check for active session (optional - tracepoint can be pending)
            var hasSession = _sessionManager.CurrentSession != null;
            if (!hasSession)
            {
                _logger.LogDebug("No active debug session, tracepoint will be pending");
            }

            // Set the tracepoint
            var tracepoint = await _breakpointManager.SetTracepointAsync(
                file, line, column, log_message, hit_count_multiple, max_notifications, linkedCts.Token);

            stopwatch.Stop();
            _logger.ToolCompleted("tracepoint_set", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Set tracepoint {TracepointId} at {File}:{Line} (state: {State})",
                tracepoint.Id, file, line, tracepoint.State);

            return new TracepointSetResult(
                Success: true,
                Tracepoint: ToTracepointInfo(tracepoint));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.ToolError("tracepoint_set", ErrorCodes.Timeout);
            return new TracepointSetResult(
                Success: false,
                Error: new ToolError(ErrorCodes.Timeout, $"tracepoint_set timed out after {timeout_ms}ms", new { timeout = timeout_ms }));
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("tracepoint_set", ErrorCodes.Timeout);
            return new TracepointSetResult(
                Success: false,
                Error: new ToolError(ErrorCodes.Timeout, "Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.ToolError("tracepoint_set", ErrorCodes.InvalidLine);
            return new TracepointSetResult(
                Success: false,
                Error: new ToolError(
                    ErrorCodes.InvalidLine,
                    $"Failed to set tracepoint: {ex.Message}",
                    new { file, line, exceptionType = ex.GetType().Name }));
        }
    }

    private static TracepointInfo ToTracepointInfo(Breakpoint tp) =>
        new(
            Id: tp.Id,
            Type: "tracepoint",
            Location: new TracepointLocation(
                File: tp.Location.File,
                Line: tp.Location.Line,
                Column: tp.Location.Column,
                FunctionName: tp.Location.FunctionName,
                ModuleName: tp.Location.ModuleName),
            State: tp.State.ToString().ToLowerInvariant(),
            Enabled: tp.Enabled,
            LogMessage: tp.LogMessage,
            HitCountMultiple: tp.HitCountMultiple > 0 ? tp.HitCountMultiple : null,
            MaxNotifications: tp.MaxNotifications > 0 ? tp.MaxNotifications : null);
}
