using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using DebugMcp.Services.Inspection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for retrieving stack traces from a paused debug session.
/// </summary>
[McpServerToolType]
public sealed class StacktraceGetTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ISuspicionRanker _ranker;
    private readonly ILogger<StacktraceGetTool> _logger;

    public StacktraceGetTool(IDebugSessionManager sessionManager, ISuspicionRanker ranker, ILogger<StacktraceGetTool> logger)
    {
        _sessionManager = sessionManager;
        _ranker = ranker;
        _logger = logger;
    }

    /// <summary>
    /// Get stack trace for a thread.
    /// </summary>
    /// <param name="thread_id">Thread ID (default: current thread).</param>
    /// <param name="start_frame">Start from frame N (for pagination, default: 0).</param>
    /// <param name="max_frames">Maximum frames to return (default: 20, min: 1, max: 1000).</param>
    /// <returns>Stack frames with source locations and arguments.</returns>
    [McpServerTool(Name = "stacktrace_get", Title = "Get Stack Trace",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get the call stack for a thread. The process must be paused. Returns ordered stack frames from top (index 0, most recent call) to bottom (entry point). Each frame includes: index, function name, module, is_external flag, source location (file/line/column), and arguments. Supports pagination via start_frame and max_frames. Use variables_get with a specific frame_index to inspect locals at any frame depth. Example response: {\"success\": true, \"thread_id\": 1, \"total_frames\": 5, \"frames\": [{\"index\": 0, \"function\": \"MyApp.Program.Main()\", \"module\": \"MyApp.dll\", \"is_external\": false, \"location\": {\"file\": \"Program.cs\", \"line\": 42}}]}")]
    public Task<StacktraceGetResult> GetStackTraceAsync(
        [Description("Thread ID (default: current thread)")] int? thread_id = null,
        [Description("Start from frame N (for pagination)")] int start_frame = 0,
        [Description("Maximum frames to return")] int max_frames = 20,
        [Description("Include raw physical frames alongside logical frames")] bool include_raw = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("stacktrace_get",
            $"{{\"thread_id\": {(thread_id?.ToString() ?? "null")}, \"start_frame\": {start_frame}, \"max_frames\": {max_frames}}}");

        try
        {
            // Validate parameters
            if (start_frame < 0)
            {
                return Task.FromResult(CreateErrorResult(ErrorCodes.InvalidParameter,
                    "start_frame must be >= 0",
                    new { parameter = "start_frame", value = start_frame }));
            }

            if (max_frames < 1 || max_frames > 1000)
            {
                return Task.FromResult(CreateErrorResult(ErrorCodes.InvalidParameter,
                    "max_frames must be between 1 and 1000",
                    new { parameter = "max_frames", value = max_frames }));
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("stacktrace_get", ErrorCodes.NoSession);
                return Task.FromResult(CreateErrorResult(ErrorCodes.NoSession, "No active debug session"));
            }

            // Check if paused
            if (session.State != SessionState.Paused)
            {
                _logger.ToolError("stacktrace_get", ErrorCodes.NotPaused);
                return Task.FromResult(CreateErrorResult(ErrorCodes.NotPaused,
                    $"Cannot get stack trace: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})",
                    new { currentState = session.State.ToString().ToLowerInvariant() }));
            }

            // Get stack frames
            var (frames, totalFrames) = _sessionManager.GetStackFrames(thread_id, start_frame, max_frames);

            // Use session's active thread ID if no thread specified
            var actualThreadId = thread_id ?? session.ActiveThreadId ?? 0;

            stopwatch.Stop();
            _logger.ToolCompleted("stacktrace_get", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Retrieved {FrameCount} stack frames (total: {TotalFrames}) for thread {ThreadId}",
                frames.Count, totalFrames, actualThreadId);

            var (boundedFrames, framesTruncation) = ResultTruncation.Bound(
                frames.Select(BuildFrameResult).ToList(), "stacktrace_get result exceeded the 256 KB size budget");

            // Raw frames show the physical stack without logical async reconstruction.
            // Currently identical to frames since continuation chain (US2) isn't implemented yet.
            // Capped to the same count as the (possibly truncated) logical frames rather than
            // budgeted independently, since the two lists describe the same underlying stack.
            var rawFrames = include_raw
                ? frames.Take(boundedFrames.Count).Select(BuildRawFrameResult).ToList()
                : null;

            // Same deterministic ranker as exception_get_context, run without exception context
            // (stacktrace_get isn't necessarily called at a fault) — only the exception-independent
            // heuristics can fire (FR-022-FR-026).
            var enrichment = _ranker.Rank(frames.Select(BuildAutopsyFrame).ToList(), exception: null);

            return Task.FromResult(new StacktraceGetResult(
                Success: true,
                ThreadId: actualThreadId,
                TotalFrames: totalFrames,
                Frames: boundedFrames,
                RawFrames: rawFrames,
                Truncation: framesTruncation,
                Ranking: enrichment.Ranking,
                RankingUnavailable: enrichment.Unavailable));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active debug session"))
        {
            _logger.ToolError("stacktrace_get", ErrorCodes.NoSession);
            return Task.FromResult(CreateErrorResult(ErrorCodes.NoSession, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not paused"))
        {
            _logger.ToolError("stacktrace_get", ErrorCodes.NotPaused);
            return Task.FromResult(CreateErrorResult(ErrorCodes.NotPaused, ex.Message));
        }
        catch (ArgumentException ex) when (ex.Message.Contains("thread"))
        {
            _logger.ToolError("stacktrace_get", ErrorCodes.InvalidThread);
            return Task.FromResult(CreateErrorResult(ErrorCodes.InvalidThread, ex.Message,
                new { thread_id }));
        }
        catch (Exception ex)
        {
            _logger.ToolError("stacktrace_get", ErrorCodes.StackTraceFailed);
            return Task.FromResult(CreateErrorResult(ErrorCodes.StackTraceFailed,
                $"Failed to retrieve stack trace: {ex.Message}"));
        }
    }

    private static StacktraceGetResult CreateErrorResult(string code, string message, object? details = null)
    {
        return new StacktraceGetResult(Success: false, Error: new ToolError(code, message, details));
    }

    private static StackFrameResult BuildFrameResult(Models.Inspection.StackFrame frame)
    {
        return new StackFrameResult(
            Index: frame.Index,
            Function: frame.Function,
            Module: frame.Module,
            IsExternal: frame.IsExternal,
            FrameKind: frame.FrameKind,
            IsAwaiting: frame.IsAwaiting,
            LogicalFunction: frame.LogicalFunction,
            Location: BuildLocationResult(frame.Location),
            Arguments: frame.Arguments?.Count > 0
                ? frame.Arguments.Select(BuildArgumentResult).ToList()
                : null);
    }

    /// <summary>
    /// Builds a raw frame response showing physical stack frame data without logical async transformations.
    /// </summary>
    private static RawStackFrameResult BuildRawFrameResult(Models.Inspection.StackFrame frame)
    {
        return new RawStackFrameResult(
            Index: frame.Index,
            Function: frame.Function,
            Module: frame.Module,
            IsExternal: frame.IsExternal,
            FrameKind: frame.FrameKind,
            Location: BuildLocationResult(frame.Location));
    }

    private static FrameLocationResult? BuildLocationResult(Models.SourceLocation? location)
    {
        return location == null
            ? null
            : new FrameLocationResult(location.File, location.Line, location.Column, location.FunctionName);
    }

    private static VariableResult BuildArgumentResult(Models.Inspection.Variable arg)
    {
        return new VariableResult(
            Name: arg.Name,
            Type: arg.Type,
            Value: arg.Value,
            Scope: arg.Scope.ToString().ToLowerInvariant(),
            HasChildren: arg.HasChildren,
            ChildrenCount: arg.ChildrenCount);
    }

    private static Models.Inspection.AutopsyFrame BuildAutopsyFrame(Models.Inspection.StackFrame frame)
    {
        return new Models.Inspection.AutopsyFrame(
            Index: frame.Index,
            Function: frame.Function,
            Module: frame.Module,
            IsExternal: frame.IsExternal,
            Location: frame.Location,
            Arguments: frame.Arguments);
    }
}
