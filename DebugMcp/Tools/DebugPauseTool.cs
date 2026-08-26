using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for pausing execution of a running debug session.
/// </summary>
[McpServerToolType]
public sealed class DebugPauseTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<DebugPauseTool> _logger;

    public DebugPauseTool(IDebugSessionManager sessionManager, ILogger<DebugPauseTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Pause execution of the running debuggee process.
    /// </summary>
    /// <returns>Pause result with thread locations.</returns>
    [McpServerTool(Name = "debug_pause", Title = "Pause Execution",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Pause execution of the running debuggee process")]
    public async Task<DebugPauseResult> PauseAsync(
        [Description("Maximum time to wait for the process to pause, in milliseconds (default: 30000)")]
        int timeout = 30000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("debug_pause", "{}");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("debug_pause", ErrorCodes.NoSession);
                return CreateErrorResult(ErrorCodes.NoSession, "No active debug session");
            }

            // Check if already paused
            if (session.State == SessionState.Paused)
            {
                stopwatch.Stop();
                _logger.ToolCompleted("debug_pause", stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("Process already paused");

                var currentThreads = _sessionManager.GetThreads();
                return new DebugPauseResult(
                    Success: true,
                    Session: BuildSessionResponse(session),
                    Threads: currentThreads.Select(BuildThreadResponse).ToList());
            }

            // Pause the process
            var threads = await _sessionManager.PauseAsync(linkedCts.Token);

            stopwatch.Stop();
            _logger.ToolCompleted("debug_pause", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Paused process with {ThreadCount} threads", threads.Count);

            // Re-read the session so the response carries the same `session` envelope as
            // debug_continue / debug_step (BUG-004); `threads` stays as supplementary detail.
            var pausedSession = _sessionManager.CurrentSession ?? session;
            return new DebugPauseResult(
                Success: true,
                Session: BuildSessionResponse(pausedSession),
                Threads: threads.Select(BuildThreadResponse).ToList());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active debug session"))
        {
            _logger.ToolError("debug_pause", ErrorCodes.NoSession);
            return CreateErrorResult(ErrorCodes.NoSession, ex.Message);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.ToolError("debug_pause", ErrorCodes.Timeout);
            return CreateErrorResult(ErrorCodes.Timeout, $"debug_pause timed out after {timeout}ms", new { timeout });
        }
        catch (Exception ex)
        {
            _logger.ToolError("debug_pause", "PAUSE_FAILED");
            return CreateErrorResult("PAUSE_FAILED",
                $"Failed to pause process: {ex.Message}");
        }
    }

    private static DebugPauseResult CreateErrorResult(string code, string message, object? details = null) =>
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

    private static PauseThreadInfo BuildThreadResponse(ThreadInfo thread)
    {
        PauseThreadLocation? location = null;

        if (thread.Location != null)
        {
            location = new PauseThreadLocation(
                Function: thread.Location.FunctionName ?? "Unknown",
                File: string.IsNullOrEmpty(thread.Location.File) ? null : thread.Location.File,
                Line: thread.Location.Line > 0 ? thread.Location.Line : null);
        }

        return new PauseThreadInfo(thread.Id, location);
    }
}
