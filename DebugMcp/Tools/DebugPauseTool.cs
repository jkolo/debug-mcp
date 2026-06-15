using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
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
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Pause execution of the running debuggee process")]
    public async Task<string> PauseAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("debug_pause", "{}");

        try
        {
            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("debug_pause", ErrorCodes.NoSession);
                return CreateErrorResponse(ErrorCodes.NoSession, "No active debug session");
            }

            // Check if already paused
            if (session.State == SessionState.Paused)
            {
                stopwatch.Stop();
                _logger.ToolCompleted("debug_pause", stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("Process already paused");

                var currentThreads = _sessionManager.GetThreads();
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    session = BuildSessionResponse(session),
                    threads = currentThreads.Select(t => BuildThreadResponse(t))
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Pause the process
            var threads = await _sessionManager.PauseAsync();

            stopwatch.Stop();
            _logger.ToolCompleted("debug_pause", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Paused process with {ThreadCount} threads", threads.Count);

            // Re-read the session so the response carries the same `session` envelope as
            // debug_continue / debug_step (BUG-004); `threads` stays as supplementary detail.
            var pausedSession = _sessionManager.CurrentSession ?? session;
            return JsonSerializer.Serialize(new
            {
                success = true,
                session = BuildSessionResponse(pausedSession),
                threads = threads.Select(t => BuildThreadResponse(t))
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active debug session"))
        {
            _logger.ToolError("debug_pause", ErrorCodes.NoSession);
            return CreateErrorResponse(ErrorCodes.NoSession, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.ToolError("debug_pause", "PAUSE_FAILED");
            return CreateErrorResponse("PAUSE_FAILED",
                $"Failed to pause process: {ex.Message}");
        }
    }

    private static string CreateErrorResponse(string code, string message, object? details = null)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new
            {
                code,
                message,
                details
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object BuildSessionResponse(DebugSession session)
    {
        var response = new Dictionary<string, object?>
        {
            ["processId"] = session.ProcessId,
            ["processName"] = session.ProcessName,
            ["state"] = session.State.ToString().ToLowerInvariant(),
            ["launchMode"] = session.LaunchMode.ToString().ToLowerInvariant()
        };

        if (session.State == SessionState.Paused)
        {
            if (session.PauseReason.HasValue)
            {
                response["pauseReason"] = session.PauseReason.Value.ToString().ToLowerInvariant();
            }

            if (session.CurrentLocation != null)
            {
                response["location"] = new
                {
                    file = session.CurrentLocation.File,
                    line = session.CurrentLocation.Line,
                    column = session.CurrentLocation.Column,
                    functionName = session.CurrentLocation.FunctionName
                };
            }

            if (session.ActiveThreadId.HasValue)
            {
                response["activeThreadId"] = session.ActiveThreadId.Value;
            }
        }

        return response;
    }

    private static object BuildThreadResponse(ThreadInfo thread)
    {
        var response = new Dictionary<string, object?>
        {
            ["id"] = thread.Id
        };

        if (thread.Location != null)
        {
            var location = new Dictionary<string, object>
            {
                ["function"] = thread.Location.FunctionName ?? "Unknown"
            };

            if (!string.IsNullOrEmpty(thread.Location.File))
            {
                location["file"] = thread.Location.File;
            }

            if (thread.Location.Line > 0)
            {
                location["line"] = thread.Location.Line;
            }

            response["location"] = location;
        }

        return response;
    }
}
