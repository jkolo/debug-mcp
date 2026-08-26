using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for disconnecting from the current debug session.
/// </summary>
[McpServerToolType]
public sealed class DebugDisconnectTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<DebugDisconnectTool> _logger;

    public DebugDisconnectTool(IDebugSessionManager sessionManager, ILogger<DebugDisconnectTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Disconnect from the current debug session.
    /// </summary>
    /// <param name="terminateProcess">Terminate the process instead of detaching (only for launched processes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Disconnect result.</returns>
    [McpServerTool(Name = "debug_disconnect", Title = "Disconnect Debug Session",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Disconnect from the current debug session. For launched processes, optionally terminates the debuggee (terminateProcess=true). For attached processes, detaches and lets the process continue running. Safe to call when no session is active (returns success). Has a 10-second internal timeout — if the process doesn't respond, it is force-killed. Returns: disconnect status with previousSession info (processId, processName, launchMode). Example response: {\"success\": true, \"state\": \"disconnected\", \"wasTerminated\": true, \"previousSession\": {\"processId\": 1234, \"processName\": \"MyApp\", \"launchMode\": \"launch\"}}")]
    public async Task<DebugDisconnectResult> DisconnectAsync(
        [Description("Terminate the process instead of detaching (only for launched processes)")] bool terminateProcess = false,
        [Description("Maximum time to wait for disconnect before force-killing the process, in milliseconds (default: 10000)")] int timeout = 10000,
        CancellationToken cancellationToken = default)
    {
        var disconnectTimeout = TimeSpan.FromMilliseconds(timeout);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("debug_disconnect", JsonSerializer.Serialize(new { terminateProcess }));

        try
        {
            var currentSession = _sessionManager.CurrentSession;

            // Handle case where no session is active
            if (currentSession == null)
            {
                stopwatch.Stop();
                _logger.ToolCompleted("debug_disconnect", stopwatch.ElapsedMilliseconds);

                return new DebugDisconnectResult(
                    Success: true,
                    State: "disconnected",
                    Message: "No active debug session",
                    PreviousSession: null);
            }

            // Capture session info before disconnect
            var previousSessionInfo = new PreviousSessionInfo(
                ProcessId: currentSession.ProcessId,
                ProcessName: currentSession.ProcessName,
                LaunchMode: currentSession.LaunchMode.ToString().ToLowerInvariant());

            // Determine if process will be terminated
            var willTerminate = terminateProcess && currentSession.LaunchMode == LaunchMode.Launch;

            // Perform disconnect with timeout to prevent hanging
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(disconnectTimeout);

            try
            {
                await _sessionManager.DisconnectAsync(terminateProcess, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout hit, not user cancellation — force-kill the process as last resort
                _logger.LogWarning(
                    "debug_disconnect timed out after {TimeoutSeconds}s, force-killing process {Pid}",
                    disconnectTimeout.TotalSeconds, currentSession.ProcessId);

                ForceKillProcess(currentSession.ProcessId);

                stopwatch.Stop();
                return new DebugDisconnectResult(
                    Success: true,
                    State: "disconnected",
                    WasTerminated: true,
                    TimedOut: true,
                    Message: $"Disconnect timed out after {disconnectTimeout.TotalSeconds}s. Process was force-killed.",
                    PreviousSession: previousSessionInfo);
            }

            stopwatch.Stop();
            _logger.ToolCompleted("debug_disconnect", stopwatch.ElapsedMilliseconds);

            return new DebugDisconnectResult(
                Success: true,
                State: "disconnected",
                WasTerminated: willTerminate,
                PreviousSession: previousSessionInfo);
        }
        catch (Exception ex)
        {
            _logger.ToolError("debug_disconnect", "DISCONNECT_FAILED");

            return new DebugDisconnectResult(
                Success: false,
                Error: new ToolError("DISCONNECT_FAILED", $"Failed to disconnect: {ex.Message}"));
        }
    }

    private void ForceKillProcess(int pid)
    {
        try
        {
            System.Diagnostics.Process.GetProcessById(pid)?.Kill(entireProcessTree: true);
            _logger.LogWarning("Force-killed process {Pid}", pid);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Force-kill of process {Pid} failed (may have already exited)", pid);
        }
    }
}
