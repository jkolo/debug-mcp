using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services.Breakpoints;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for listing all breakpoints in the current debug session.
/// </summary>
[McpServerToolType]
public sealed class BreakpointListTool
{
    private readonly IBreakpointManager _breakpointManager;
    private readonly ILogger<BreakpointListTool> _logger;

    public BreakpointListTool(
        IBreakpointManager breakpointManager,
        ILogger<BreakpointListTool> logger)
    {
        _breakpointManager = breakpointManager;
        _logger = logger;
    }

    /// <summary>
    /// List all breakpoints in the current debug session.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all breakpoints with their details.</returns>
    [McpServerTool(Name = "breakpoint_list", Title = "List Breakpoints",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all breakpoints in the current debug session")]
    public async Task<string> ListBreakpointsAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("breakpoint_list", "{}");

        try
        {
            // Get all breakpoints (source + tracepoints)
            var breakpoints = await _breakpointManager.GetBreakpointsAsync(cancellationToken);

            // Get all exception breakpoints (BUG-4 fix)
            var exceptionBreakpoints = await _breakpointManager.GetExceptionBreakpointsAsync(cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("breakpoint_list", stopwatch.ElapsedMilliseconds);
            _logger.LogDebug("Listed {Count} breakpoints + {ExCount} exception breakpoints",
                breakpoints.Count, exceptionBreakpoints.Count);

            // Serialize source/tracepoint breakpoints
            var serializedBreakpoints = breakpoints.Select(bp => new
            {
                id = bp.Id,
                type = bp.Type == BreakpointType.Tracepoint ? "tracepoint" : "blocking",
                location = new
                {
                    file = bp.Location.File,
                    line = bp.Location.Line,
                    column = bp.Location.Column,
                    endLine = bp.Location.EndLine,
                    endColumn = bp.Location.EndColumn,
                    functionName = bp.Location.FunctionName,
                    moduleName = bp.Location.ModuleName
                },
                state = bp.State.ToString().ToLowerInvariant(),
                enabled = bp.Enabled,
                verified = bp.Verified,
                condition = bp.Condition,
                hitCount = bp.HitCount,
                message = bp.Message,
                logMessage = bp.Type == BreakpointType.Tracepoint ? bp.LogMessage : null,
                hitCountMultiple = bp.Type == BreakpointType.Tracepoint && bp.HitCountMultiple > 0 ? bp.HitCountMultiple : (int?)null,
                maxNotifications = bp.Type == BreakpointType.Tracepoint && bp.MaxNotifications > 0 ? bp.MaxNotifications : (int?)null,
                notificationsSent = bp.Type == BreakpointType.Tracepoint ? bp.NotificationsSent : (int?)null
            }).ToList();

            // Serialize exception breakpoints
            var serializedExceptionBreakpoints = exceptionBreakpoints.Select(eb => new
            {
                id = eb.Id,
                type = "exception",
                exceptionType = eb.ExceptionType,
                breakOnFirstChance = eb.BreakOnFirstChance,
                breakOnSecondChance = eb.BreakOnSecondChance,
                includeSubtypes = eb.IncludeSubtypes,
                enabled = eb.Enabled,
                verified = eb.Verified,
                hitCount = eb.HitCount
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                breakpoints = serializedBreakpoints,
                exceptionBreakpoints = serializedExceptionBreakpoints,
                count = breakpoints.Count + exceptionBreakpoints.Count
            }, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("breakpoint_list", ErrorCodes.Timeout);
            return CreateErrorResponse(ErrorCodes.Timeout, "Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.ToolError("breakpoint_list", ErrorCodes.NoSession);
            return CreateErrorResponse(
                ErrorCodes.NoSession,
                $"Failed to list breakpoints: {ex.Message}",
                new { exceptionType = ex.GetType().Name });
        }
    }

    private static string CreateErrorResponse(string code, string message, object? details = null)
    {
        var response = new
        {
            success = false,
            error = new
            {
                code,
                message,
                details
            }
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
