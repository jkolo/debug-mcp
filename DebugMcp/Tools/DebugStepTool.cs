using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for stepping through code during debugging.
/// </summary>
[McpServerToolType]
public sealed class DebugStepTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<DebugStepTool> _logger;

    public DebugStepTool(IDebugSessionManager sessionManager, ILogger<DebugStepTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Step through code in the specified mode.
    /// </summary>
    /// <param name="mode">Step mode: "in" (step into), "over" (step over), or "out" (step out).</param>
    /// <param name="timeout">Timeout in milliseconds (default: 30000, min: 1000, max: 300000).</param>
    /// <returns>Updated session state after stepping.</returns>
    [McpServerTool(Name = "debug_step", Title = "Step Through Code",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Step through code during debugging. The process must be paused. Modes: 'in' (step into function calls), 'over' (step over, staying in current scope), 'out' (step out to caller). Returns: updated session state with new source location after the step completes. The step blocks until the debuggee re-pauses at the next source line. Example response: {\"success\": true, \"stepMode\": \"over\", \"session\": {\"processId\": 1234, \"state\": \"paused\", \"pauseReason\": \"step\", \"location\": {\"file\": \"Program.cs\", \"line\": 43, \"functionName\": \"Main\"}}}")]
    public async Task<DebugStepResult> StepAsync(
        [Description("Step mode: 'in', 'over', or 'out'")] string mode,
        [Description("Timeout in milliseconds")] int timeout = 30000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("debug_step", $"{{\"mode\": \"{mode}\", \"timeout\": {timeout}}}");

        try
        {
            // Validate timeout bounds
            if (timeout < 1000 || timeout > 300000)
            {
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    $"Timeout must be between 1000 and 300000 milliseconds (got {timeout})",
                    new { parameter = "timeout", value = timeout });
            }

            // Parse and validate step mode
            if (!TryParseStepMode(mode, out var stepMode))
            {
                _logger.ToolError("debug_step", ErrorCodes.InvalidParameter);
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    $"Invalid step mode: '{mode}'. Valid modes: in, over, out",
                    new { parameter = "mode", value = mode, validModes = new[] { "in", "over", "out" } });
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("debug_step", ErrorCodes.NoSession);
                return CreateErrorResult(ErrorCodes.NoSession, "No active debug session");
            }

            // Check if paused
            if (session.State != SessionState.Paused)
            {
                _logger.ToolError("debug_step", ErrorCodes.NotPaused);
                return CreateErrorResult(ErrorCodes.NotPaused,
                    $"Cannot step: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})",
                    new { currentState = session.State.ToString().ToLowerInvariant() });
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var updatedSession = await _sessionManager.StepAsync(stepMode, linkedCts.Token);

            stopwatch.Stop();
            _logger.ToolCompleted("debug_step", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Stepped {Mode} for process {ProcessId}", mode, updatedSession.ProcessId);

            return new DebugStepResult(
                Success: true,
                StepMode: mode,
                Session: BuildSessionResponse(updatedSession));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.ToolError("debug_step", ErrorCodes.Timeout);
            return CreateErrorResult(ErrorCodes.Timeout, "Operation was cancelled");
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("debug_step", ErrorCodes.Timeout);
            return CreateErrorResult(ErrorCodes.Timeout, "Step operation timed out");
        }
        catch (InvalidOperationException ex)
        {
            var errorCode = ex.Message.Contains("not paused") ? ErrorCodes.NotPaused : ErrorCodes.StepFailed;
            _logger.ToolError("debug_step", errorCode);
            return CreateErrorResult(errorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.ToolError("debug_step", ErrorCodes.StepFailed);
            return CreateErrorResult(ErrorCodes.StepFailed, $"Failed to step: {ex.Message}");
        }
    }

    private static bool TryParseStepMode(string mode, out StepMode stepMode)
    {
        stepMode = default;

        switch (mode?.ToLowerInvariant())
        {
            case "in":
                stepMode = StepMode.In;
                return true;
            case "over":
                stepMode = StepMode.Over;
                return true;
            case "out":
                stepMode = StepMode.Out;
                return true;
            default:
                return false;
        }
    }

    private static DebugStepResult CreateErrorResult(string code, string message, object? details = null) =>
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
