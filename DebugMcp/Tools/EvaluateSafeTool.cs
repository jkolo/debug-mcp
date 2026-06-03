using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Services.SafeEval;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

[McpServerToolType]
public sealed class EvaluateSafeTool(
    IDebugSessionManager sessionManager,
    ISafeExpressionAnalyzer analyzer,
    ILogger<EvaluateSafeTool> logger)
{
    [McpServerTool(Name = "evaluate_safe", Title = "Evaluate Expression (Safe Mode)",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Evaluate a C# expression in safe mode — static analysis blocks method calls, object construction, and assignments before they reach the debugged process. Suitable for autonomous agents. Permitted: member reads, property access, arithmetic, comparisons (==,!=,<,>,<=,>=), logical (&&,||,!), ternary (?:), indexers, null-conditional (?.,?[]), and allowlisted methods. Blocked: non-allowlisted method calls, new T(), assignments. On rejection: {\"success\": false, \"error\": {\"code\": \"safe_eval_rejected\", \"details\": {\"rejection_category\": \"MethodCall\", \"offending_expression\": \"...\"}}}. On success: {\"success\": true, \"value\": \"42\", \"type\": \"System.Int32\", \"has_children\": false}")]
    public async Task<string> EvaluateSafeAsync(
        [Description("C# expression to evaluate safely")] string expression,
        [Description("Thread context (default: current thread)")] int? thread_id = null,
        [Description("Stack frame context (0 = top)")] int frame_index = 0,
        [Description("Evaluation timeout in milliseconds (applied only if expression passes safety check)")] int timeout_ms = 5000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        logger.ToolInvoked("evaluate_safe",
            $"{{\"expression\": \"{EscapeJsonString(expression)}\", \"thread_id\": {(thread_id?.ToString() ?? "null")}, \"frame_index\": {frame_index}}}");

        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                logger.ToolError("evaluate_safe", "syntax_error");
                return CreateErrorResponse("syntax_error", "Expression cannot be empty", position: 0);
            }

            // Safety check FIRST — before session/pause check
            var analysis = analyzer.Analyze(expression);
            if (!analysis.IsAllowed)
            {
                logger.ToolError("evaluate_safe", "safe_eval_rejected");
                return CreateRejectionResponse(analysis.Rejection!);
            }

            // Validate parameters
            if (timeout_ms < 100 || timeout_ms > 60000)
                return CreateErrorResponse(ErrorCodes.InvalidParameter,
                    "timeout_ms must be between 100 and 60000",
                    new { parameter = "timeout_ms", value = timeout_ms });

            if (frame_index < 0)
                return CreateErrorResponse(ErrorCodes.InvalidParameter,
                    "frame_index must be >= 0",
                    new { parameter = "frame_index", value = frame_index });

            // Check for active session
            var session = sessionManager.CurrentSession;
            if (session == null)
            {
                logger.ToolError("evaluate_safe", ErrorCodes.NoSession);
                return CreateErrorResponse(ErrorCodes.NoSession, "No active debug session");
            }

            if (session.State != SessionState.Paused)
            {
                logger.ToolError("evaluate_safe", ErrorCodes.NotPaused);
                return CreateErrorResponse(ErrorCodes.NotPaused,
                    $"Cannot evaluate expression: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})");
            }

            using var cts = new CancellationTokenSource(timeout_ms);
            var result = await sessionManager.EvaluateAsync(expression, thread_id, frame_index, timeout_ms, cts.Token);

            stopwatch.Stop();
            logger.ToolCompleted("evaluate_safe", stopwatch.ElapsedMilliseconds);

            if (result.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    value = result.Value,
                    type = result.Type,
                    has_children = result.HasChildren
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            return CreateEvaluationErrorResponse(result.Error!);
        }
        catch (OperationCanceledException)
        {
            logger.ToolError("evaluate_safe", "eval_timeout");
            return CreateErrorResponse("eval_timeout", $"Expression evaluation timed out after {timeout_ms}ms");
        }
        catch (Exception ex)
        {
            logger.ToolError("evaluate_safe", "eval_exception");
            return CreateErrorResponse("eval_exception", ex.Message,
                new { exception_type = ex.GetType().FullName });
        }
    }

    private static string CreateRejectionResponse(SafeEvalRejection rejection)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new
            {
                code = "safe_eval_rejected",
                message = rejection.Message,
                details = new
                {
                    rejection_category = rejection.Category.ToString(),
                    offending_expression = rejection.OffendingExpression,
                    allowed_operations = "member reads, property access, arithmetic (+,-,*,/,%), comparisons (==,!=,<,>,<=,>=), logical (&&,||,!), ternary (?:), indexers, null-conditional (?.,?[]), and methods on the safe-eval allowlist"
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateErrorResponse(string code, string message, object? details = null, int? position = null)
    {
        var errorObj = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["message"] = message
        };
        if (details != null) errorObj["details"] = details;
        if (position.HasValue) errorObj["position"] = position.Value;

        return JsonSerializer.Serialize(new
        {
            success = false,
            error = errorObj
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateEvaluationErrorResponse(EvaluationError error)
    {
        var errorObj = new Dictionary<string, object?>
        {
            ["code"] = error.Code,
            ["message"] = error.Message
        };
        if (!string.IsNullOrEmpty(error.ExceptionType))
            errorObj["exception_type"] = error.ExceptionType;
        if (error.Position.HasValue)
            errorObj["position"] = error.Position.Value;

        return JsonSerializer.Serialize(new
        {
            success = false,
            error = errorObj
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string EscapeJsonString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"")
             .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
