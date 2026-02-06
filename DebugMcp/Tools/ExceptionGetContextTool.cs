using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

[McpServerToolType]
public sealed class ExceptionGetContextTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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

    [McpServerTool(Name = "exception_get_context")]
    [Description("Get full exception context when paused at an exception. Returns exception details, inner exception chain, stack frames with source locations, and local variables for the throwing frame — all in a single call.")]
    public async Task<string> GetExceptionContext(
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
            return CreateErrorResponse(ErrorCodes.NoSession, "No active debug session.");
        }

        if (session.State != SessionState.Paused)
        {
            _logger.ToolError("exception_get_context", ErrorCodes.NotPaused);
            return CreateErrorResponse(ErrorCodes.NotPaused,
                "Process is not paused. Current state: " + session.State);
        }

        // Parameter validation
        if (max_frames < 1 || max_frames > 100)
            return CreateErrorResponse(ErrorCodes.InvalidParameter,
                "max_frames must be between 1 and 100.",
                new { parameter = "max_frames", value = max_frames });

        if (include_variables_for_frames < 0 || include_variables_for_frames > 10)
            return CreateErrorResponse(ErrorCodes.InvalidParameter,
                "include_variables_for_frames must be between 0 and 10.",
                new { parameter = "include_variables_for_frames", value = include_variables_for_frames });

        if (max_inner_exceptions < 0 || max_inner_exceptions > 20)
            return CreateErrorResponse(ErrorCodes.InvalidParameter,
                "max_inner_exceptions must be between 0 and 20.",
                new { parameter = "max_inner_exceptions", value = max_inner_exceptions });

        try
        {
            var result = await _autopsyService.GetExceptionContextAsync(
                max_frames, include_variables_for_frames, max_inner_exceptions, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("exception_get_context", stopwatch.ElapsedMilliseconds);

            return JsonSerializer.Serialize(new
            {
                threadId = result.ThreadId,
                exception = new
                {
                    type = result.Exception.Type,
                    message = result.Exception.Message,
                    isFirstChance = result.Exception.IsFirstChance,
                    stackTraceString = result.Exception.StackTraceString
                },
                innerExceptions = result.InnerExceptions.Select(ie => new
                {
                    type = ie.Type,
                    message = ie.Message,
                    depth = ie.Depth
                }),
                innerExceptionsTruncated = result.InnerExceptionsTruncated,
                frames = result.Frames.Select(BuildFrameResponse),
                totalFrames = result.TotalFrames,
                throwingFrameIndex = result.ThrowingFrameIndex
            }, JsonOptions);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not paused at an exception", StringComparison.OrdinalIgnoreCase))
        {
            _logger.ToolError("exception_get_context", ErrorCodes.NoException);
            return CreateErrorResponse(ErrorCodes.NoException,
                "No exception context available. The debugger is not currently paused at an exception.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "exception_get_context failed unexpectedly");
            return CreateErrorResponse("AUTOPSY_FAILED",
                "Exception autopsy failed: " + ex.Message);
        }
    }

    private static Dictionary<string, object?> BuildFrameResponse(Models.Inspection.AutopsyFrame f)
    {
        var frame = new Dictionary<string, object?>
        {
            ["index"] = f.Index,
            ["function"] = f.Function,
            ["module"] = f.Module,
            ["isExternal"] = f.IsExternal,
            ["location"] = f.Location != null ? new Dictionary<string, object?>
            {
                ["file"] = f.Location.File,
                ["line"] = f.Location.Line,
                ["column"] = f.Location.Column,
                ["functionName"] = f.Location.FunctionName,
                ["moduleName"] = f.Location.ModuleName
            } : null,
            ["arguments"] = f.Arguments?.Select(a => new Dictionary<string, object?>
            {
                ["name"] = a.Name,
                ["type"] = a.Type,
                ["value"] = a.Value,
                ["scope"] = a.Scope.ToString(),
                ["hasChildren"] = a.HasChildren
            }),
            ["variables"] = f.Variables != null ? new Dictionary<string, object?>
            {
                ["locals"] = f.Variables.Locals.Select(v => new Dictionary<string, object?>
                {
                    ["name"] = v.Name,
                    ["type"] = v.Type,
                    ["value"] = v.Value,
                    ["scope"] = v.Scope.ToString(),
                    ["hasChildren"] = v.HasChildren,
                    ["childrenCount"] = v.ChildrenCount
                }),
                ["errors"] = f.Variables.Errors?.Select(e => new Dictionary<string, object?>
                {
                    ["name"] = e.Name,
                    ["error"] = e.Error
                })
            } : null
        };
        return frame;
    }

    private static string CreateErrorResponse(string code, string message, object? details = null)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new { code, message, details }
        }, JsonOptions);
    }
}
