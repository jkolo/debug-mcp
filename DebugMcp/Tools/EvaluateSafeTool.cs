using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Results;
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
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Evaluate a C# expression in safe mode — static analysis blocks method calls, object construction, and assignments before they reach the debugged process. Suitable for autonomous agents. Permitted: member reads, property access, arithmetic, comparisons (==,!=,<,>,<=,>=), logical (&&,||,!), ternary (?:), indexers, null-conditional (?.,?[]), and allowlisted methods. Blocked: non-allowlisted method calls, new T(), assignments. On rejection: {\"success\": false, \"error\": {\"code\": \"safe_eval_rejected\", \"details\": {\"rejection_category\": \"MethodCall\", \"offending_expression\": \"...\"}}}. On success: {\"success\": true, \"value\": \"42\", \"type\": \"System.Int32\", \"has_children\": false}")]
    public async Task<EvaluateSafeResult> EvaluateSafeAsync(
        [Description("C# expression to evaluate safely")] string expression,
        [Description("Thread context (default: current thread)")] int? thread_id = null,
        [Description("Stack frame context (0 = top)")] int frame_index = 0,
        [Description("Evaluation timeout in milliseconds (applied only if expression passes safety check) (default: 5000)")] int timeout_ms = 5000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        logger.ToolInvoked("evaluate_safe",
            $"{{\"expression\": \"{EscapeJsonString(expression)}\", \"thread_id\": {(thread_id?.ToString() ?? "null")}, \"frame_index\": {frame_index}}}");

        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                logger.ToolError("evaluate_safe", "syntax_error");
                return CreateErrorResult("syntax_error", "Expression cannot be empty", position: 0);
            }

            // Safety check FIRST — before session/pause check
            var analysis = analyzer.Analyze(expression);
            if (!analysis.IsAllowed)
            {
                logger.ToolError("evaluate_safe", "safe_eval_rejected");
                return CreateRejectionResult(analysis.Rejection!);
            }

            // Validate parameters
            if (timeout_ms < 100 || timeout_ms > 60000)
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    "timeout_ms must be between 100 and 60000",
                    new { parameter = "timeout_ms", value = timeout_ms });

            if (frame_index < 0)
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    "frame_index must be >= 0",
                    new { parameter = "frame_index", value = frame_index });

            // Check for active session
            var session = sessionManager.CurrentSession;
            if (session == null)
            {
                logger.ToolError("evaluate_safe", ErrorCodes.NoSession);
                return CreateErrorResult(ErrorCodes.NoSession, "No active debug session");
            }

            if (session.State != SessionState.Paused)
            {
                logger.ToolError("evaluate_safe", ErrorCodes.NotPaused);
                return CreateErrorResult(ErrorCodes.NotPaused,
                    $"Cannot evaluate expression: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})");
            }

            using var timeoutCts = new CancellationTokenSource(timeout_ms);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var result = await sessionManager.EvaluateAsync(expression, thread_id, frame_index, timeout_ms, linkedCts.Token);

            stopwatch.Stop();
            logger.ToolCompleted("evaluate_safe", stopwatch.ElapsedMilliseconds);

            if (result.Success)
            {
                return new EvaluateSafeResult(
                    Success: true,
                    Value: result.Value,
                    Type: result.Type,
                    HasChildren: result.HasChildren);
            }

            return CreateEvaluationErrorResult(result.Error!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.ToolError("evaluate_safe", "eval_timeout");
            return CreateErrorResult("eval_timeout", "Operation was cancelled");
        }
        catch (OperationCanceledException)
        {
            logger.ToolError("evaluate_safe", "eval_timeout");
            return CreateErrorResult("eval_timeout", $"Expression evaluation timed out after {timeout_ms}ms");
        }
        catch (Exception ex)
        {
            logger.ToolError("evaluate_safe", "eval_exception");
            return CreateErrorResult("eval_exception", ex.Message,
                new { exception_type = ex.GetType().FullName });
        }
    }

    private static EvaluateSafeResult CreateRejectionResult(SafeEvalRejection rejection)
    {
        var details = new
        {
            rejection_category = rejection.Category.ToString(),
            offending_expression = rejection.OffendingExpression,
            allowed_operations = "member reads, property access, arithmetic (+,-,*,/,%), comparisons (==,!=,<,>,<=,>=), logical (&&,||,!), ternary (?:), indexers, null-conditional (?.,?[]), and methods on the safe-eval allowlist"
        };
        return new EvaluateSafeResult(Success: false, Error: new ToolError("safe_eval_rejected", rejection.Message, details));
    }

    /// <summary>
    /// Builds a failure result. The pre-US3 wire shape put an optional <c>position</c> directly
    /// on the error object (alongside code/message); the shared <see cref="ToolError"/> type has
    /// no such field, so <paramref name="position"/> is folded into <c>Error.Details</c> instead
    /// (as <c>{ position }</c>) — same value, now one level deeper on the wire.
    /// </summary>
    private static EvaluateSafeResult CreateErrorResult(string code, string message, object? details = null, int? position = null)
    {
        var errorDetails = position.HasValue ? (object)new { position = position.Value } : details;
        return new EvaluateSafeResult(Success: false, Error: new ToolError(code, message, errorDetails));
    }

    /// <summary>
    /// Builds a failure result from an <see cref="EvaluationError"/>. The pre-US3 wire shape put
    /// optional <c>exception_type</c>/<c>position</c> directly on the error object; both are
    /// folded into <c>Error.Details</c> here for the same reason as <see cref="CreateErrorResult"/>.
    /// </summary>
    private static EvaluateSafeResult CreateEvaluationErrorResult(EvaluationError error)
    {
        var hasExceptionType = !string.IsNullOrEmpty(error.ExceptionType);
        object? details = (hasExceptionType, error.Position) switch
        {
            (true, { } position) => new { exception_type = error.ExceptionType, position },
            (true, null) => new { exception_type = error.ExceptionType },
            (false, { } position) => new { position },
            _ => null
        };

        return new EvaluateSafeResult(Success: false, Error: new ToolError(error.Code, error.Message, details));
    }

    private static string EscapeJsonString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"")
             .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
