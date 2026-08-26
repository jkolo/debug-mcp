using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Results;
using DebugMcp.Services.Breakpoints;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for enabling and disabling breakpoints.
/// </summary>
[McpServerToolType]
public sealed class BreakpointEnableTool
{
    private readonly IBreakpointManager _breakpointManager;
    private readonly ILogger<BreakpointEnableTool> _logger;

    public BreakpointEnableTool(
        IBreakpointManager breakpointManager,
        ILogger<BreakpointEnableTool> logger)
    {
        _breakpointManager = breakpointManager;
        _logger = logger;
    }

    /// <summary>
    /// Enable or disable a breakpoint by ID.
    /// </summary>
    /// <param name="id">Breakpoint ID to enable or disable.</param>
    /// <param name="enabled">True to enable, false to disable. Default: true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated breakpoint information or error response.</returns>
    [McpServerTool(Name = "breakpoint_enable", Title = "Enable/Disable Breakpoint",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Enable or disable a breakpoint by ID")]
    public async Task<BreakpointEnableResult> EnableBreakpointAsync(
        [Description("Breakpoint ID to enable or disable")] string id,
        [Description("True to enable, false to disable")] bool enabled = true,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("breakpoint_enable", JsonSerializer.Serialize(new { id, enabled }));

        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.ToolError("breakpoint_enable", ErrorCodes.BreakpointNotFound);
                return new BreakpointEnableResult(
                    Success: false,
                    Error: new ToolError(ErrorCodes.BreakpointNotFound, "Breakpoint ID cannot be empty"));
            }

            // Enable/disable the breakpoint
            var updatedBreakpoint = await _breakpointManager.SetBreakpointEnabledAsync(
                id, enabled, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("breakpoint_enable", stopwatch.ElapsedMilliseconds);

            if (updatedBreakpoint == null)
            {
                _logger.ToolError("breakpoint_enable", ErrorCodes.BreakpointNotFound);
                return new BreakpointEnableResult(
                    Success: false,
                    Error: new ToolError(ErrorCodes.BreakpointNotFound, $"No breakpoint with ID '{id}'"));
            }

            _logger.LogInformation("Breakpoint {BreakpointId} {Action}",
                id, enabled ? "enabled" : "disabled");

            return new BreakpointEnableResult(
                Success: true,
                Breakpoint: ToBreakpointInfo(updatedBreakpoint));
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("breakpoint_enable", ErrorCodes.Timeout);
            return new BreakpointEnableResult(
                Success: false,
                Error: new ToolError(ErrorCodes.Timeout, "Operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.ToolError("breakpoint_enable", ErrorCodes.BreakpointNotFound);
            return new BreakpointEnableResult(
                Success: false,
                Error: new ToolError(
                    ErrorCodes.BreakpointNotFound,
                    $"Failed to enable/disable breakpoint: {ex.Message}",
                    new { id, exceptionType = ex.GetType().Name }));
        }
    }

    private static BreakpointInfo ToBreakpointInfo(Breakpoint bp) =>
        new(
            Id: bp.Id,
            Location: bp.Location,
            State: bp.State.ToString().ToLowerInvariant(),
            Enabled: bp.Enabled,
            Verified: bp.Verified,
            HitCount: bp.HitCount,
            Condition: bp.Condition,
            Message: bp.Message);
}
