using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for continuing execution of a paused debug session.
/// </summary>
[McpServerToolType]
public sealed class DebugContinueTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<DebugContinueTool> _logger;

    public DebugContinueTool(IDebugSessionManager sessionManager, ILogger<DebugContinueTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Continue execution of the paused process.
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 30000, min: 1000, max: 300000).</param>
    /// <returns>Updated session state after continuing.</returns>
    [McpServerTool(Name = "debug_continue", Title = "Continue Execution",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Continue execution of the paused process. The process must be in the 'paused' state (from a breakpoint hit, step completion, or debug_pause). After continuing, the process runs until it hits another breakpoint, throws an exception, or exits. Returns: updated session state (typically 'running'). Use breakpoint_wait to wait for the next pause event. Example response: {\"success\": true, \"session\": {\"processId\": 1234, \"processName\": \"MyApp\", \"state\": \"running\", \"launchMode\": \"launch\"}}")]
    public async Task<DebugContinueResult> ContinueAsync(
        [Description("Timeout in milliseconds (default: 30000)")] int timeout = 30000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("debug_continue", $"{{\"timeout\": {timeout}}}");

        try
        {
            // Validate timeout bounds
            if (timeout < 1000 || timeout > 300000)
            {
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    $"Timeout must be between 1000 and 300000 milliseconds (got {timeout})",
                    new { parameter = "timeout", value = timeout });
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("debug_continue", ErrorCodes.NoSession);
                return CreateErrorResult(ErrorCodes.NoSession, "No active debug session");
            }

            // Check if paused
            if (session.State != SessionState.Paused)
            {
                _logger.ToolError("debug_continue", ErrorCodes.NotPaused);
                return CreateErrorResult(ErrorCodes.NotPaused,
                    $"Cannot continue: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})",
                    new { currentState = session.State.ToString().ToLowerInvariant() });
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var updatedSession = await _sessionManager.ContinueAsync(linkedCts.Token);

            stopwatch.Stop();
            _logger.ToolCompleted("debug_continue", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Continued execution for process {ProcessId}", updatedSession.ProcessId);

            return new DebugContinueResult(
                Success: true,
                Session: BuildSessionResponse(updatedSession));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.ToolError("debug_continue", ErrorCodes.Timeout);
            return CreateErrorResult(ErrorCodes.Timeout, "Operation was cancelled");
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("debug_continue", ErrorCodes.Timeout);
            return CreateErrorResult(ErrorCodes.Timeout, "Continue operation timed out");
        }
        catch (InvalidOperationException ex)
        {
            _logger.ToolError("debug_continue", ErrorCodes.NotPaused);
            return CreateErrorResult(ErrorCodes.NotPaused, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.ToolError("debug_continue", "CONTINUE_FAILED");
            return CreateErrorResult("CONTINUE_FAILED", $"Failed to continue: {ex.Message}");
        }
    }

    private static DebugContinueResult CreateErrorResult(string code, string message, object? details = null) =>
        new(Success: false, Error: new ToolError(code, message, details));

    private static SessionStateInfo BuildSessionResponse(DebugSession session)
    {
        string? pauseReason = null;
        LocationInfo? location = null;
        int? activeThreadId = null;

        if (session.State == SessionState.Paused)
        {
            if (session.PauseReason.HasValue)
            {
                pauseReason = session.PauseReason.Value.ToString().ToLowerInvariant();
            }

            if (session.CurrentLocation != null)
            {
                location = new LocationInfo(
                    File: session.CurrentLocation.File,
                    Line: session.CurrentLocation.Line,
                    Column: session.CurrentLocation.Column,
                    FunctionName: session.CurrentLocation.FunctionName);
            }

            if (session.ActiveThreadId.HasValue)
            {
                activeThreadId = session.ActiveThreadId.Value;
            }
        }

        return new SessionStateInfo(
            ProcessId: session.ProcessId,
            ProcessName: session.ProcessName,
            State: session.State.ToString().ToLowerInvariant(),
            LaunchMode: session.LaunchMode.ToString().ToLowerInvariant(),
            PauseReason: pauseReason,
            Location: location,
            ActiveThreadId: activeThreadId);
    }
}
