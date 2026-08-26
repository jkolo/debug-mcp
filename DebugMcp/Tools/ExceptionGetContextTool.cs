using System.ComponentModel;
using System.Diagnostics;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

[McpServerToolType]
public sealed class ExceptionGetContextTool
{
    private readonly IExceptionAutopsyService _autopsyService;
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<ExceptionGetContextTool> _logger;

    public ExceptionGetContextTool(
        IExceptionAutopsyService autopsyService,
        IDebugSessionManager sessionManager,
        ILogger<ExceptionGetContextTool> logger)
    {
        _autopsyService = autopsyService;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    [McpServerTool(Name = "exception_get_context", Title = "Get Exception Context",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get full exception context when paused at an exception. Returns exception details (type, message, isFirstChance), inner exception chain, stack frames with source locations, and local variables for the throwing frame — all in a single call. This is the recommended first tool to call when the debugger pauses due to an exception. Requires the process to be paused at an exception breakpoint. Use breakpoint_set_exception to configure which exceptions cause pauses. Example response: {\"success\": true, \"threadId\": 1, \"exception\": {\"type\": \"System.NullReferenceException\", \"message\": \"Object reference not set\", \"isFirstChance\": true}, \"frames\": [{\"index\": 0, \"function\": \"MyApp.Service.Process()\", \"module\": \"MyApp.dll\"}], \"totalFrames\": 3}")]
    public async Task<ExceptionGetContextResult> GetExceptionContext(
        [Description("Maximum stack frames to return (default: 10, min: 1, max: 100)")]
        int max_frames = 10,
        [Description("Number of top frames to include local variables for (default: 1, min: 0, max: 10). 0 = no variables, 1 = throwing frame only.")]
        int include_variables_for_frames = 1,
        [Description("Maximum inner exception chain depth to traverse (default: 5, min: 0, max: 20). 0 = skip inner exceptions.")]
        int max_inner_exceptions = 5,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.ToolInvoked("exception_get_context",
            $"{{\"max_frames\":{max_frames},\"include_variables_for_frames\":{include_variables_for_frames},\"max_inner_exceptions\":{max_inner_exceptions}}}");

        // Session check
        var session = _sessionManager.CurrentSession;
        if (session == null)
        {
            _logger.ToolError("exception_get_context", ErrorCodes.NoSession);
            return new ExceptionGetContextResult(Success: false, Error: new ToolError(ErrorCodes.NoSession, "No active debug session."));
        }

        if (session.State != SessionState.Paused)
        {
            _logger.ToolError("exception_get_context", ErrorCodes.NotPaused);
            return new ExceptionGetContextResult(Success: false, Error: new ToolError(
                ErrorCodes.NotPaused, "Process is not paused. Current state: " + session.State));
        }

        // Parameter validation
        if (max_frames < 1 || max_frames > 100)
            return new ExceptionGetContextResult(Success: false, Error: new ToolError(
                ErrorCodes.InvalidParameter, "max_frames must be between 1 and 100.",
                new { parameter = "max_frames", value = max_frames }));

        if (include_variables_for_frames < 0 || include_variables_for_frames > 10)
            return new ExceptionGetContextResult(Success: false, Error: new ToolError(
                ErrorCodes.InvalidParameter, "include_variables_for_frames must be between 0 and 10.",
                new { parameter = "include_variables_for_frames", value = include_variables_for_frames }));

        if (max_inner_exceptions < 0 || max_inner_exceptions > 20)
            return new ExceptionGetContextResult(Success: false, Error: new ToolError(
                ErrorCodes.InvalidParameter, "max_inner_exceptions must be between 0 and 20.",
                new { parameter = "max_inner_exceptions", value = max_inner_exceptions }));

        try
        {
            var result = await _autopsyService.GetExceptionContextAsync(
                max_frames, include_variables_for_frames, max_inner_exceptions, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("exception_get_context", stopwatch.ElapsedMilliseconds);

            return new ExceptionGetContextResult(
                Success: true,
                ThreadId: result.ThreadId,
                Exception: result.Exception,
                InnerExceptions: result.InnerExceptions,
                InnerExceptionsTruncated: result.InnerExceptionsTruncated,
                Frames: result.Frames.Select(BuildFrameResponse).ToList(),
                TotalFrames: result.TotalFrames,
                ThrowingFrameIndex: result.ThrowingFrameIndex);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not paused at an exception", StringComparison.OrdinalIgnoreCase))
        {
            _logger.ToolError("exception_get_context", ErrorCodes.NoException);
            return new ExceptionGetContextResult(Success: false, Error: new ToolError(
                ErrorCodes.NoException, "No exception context available. The debugger is not currently paused at an exception."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "exception_get_context failed unexpectedly");
            return new ExceptionGetContextResult(Success: false, Error: new ToolError(
                "AUTOPSY_FAILED", "Exception autopsy failed: " + ex.Message));
        }
    }

    private static ExceptionFrameInfo BuildFrameResponse(Models.Inspection.AutopsyFrame f)
    {
        return new ExceptionFrameInfo(
            Index: f.Index,
            Function: f.Function,
            Module: f.Module,
            IsExternal: f.IsExternal,
            Location: f.Location,
            Arguments: f.Arguments?.Select(a => new ExceptionFrameArgument(
                a.Name, a.Type, a.Value, a.Scope.ToString(), a.HasChildren)).ToList(),
            Variables: f.Variables != null ? new ExceptionFrameVariables(
                Locals: f.Variables.Locals.Select(v => new ExceptionFrameLocal(
                    v.Name, v.Type, v.Value, v.Scope.ToString(), v.HasChildren, v.ChildrenCount)).ToList(),
                Errors: f.Variables.Errors) : null);
    }
}
