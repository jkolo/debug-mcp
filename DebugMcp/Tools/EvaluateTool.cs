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
/// MCP tool for evaluating expressions in the debuggee context.
/// </summary>
[McpServerToolType]
public sealed class EvaluateTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<EvaluateTool> _logger;

    public EvaluateTool(IDebugSessionManager sessionManager, ILogger<EvaluateTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate a C# expression in the debuggee context.
    /// </summary>
    /// <param name="expression">C# expression to evaluate.</param>
    /// <param name="thread_id">Thread context (default: current thread).</param>
    /// <param name="frame_index">Stack frame context (0 = top).</param>
    /// <param name="timeout_ms">Evaluation timeout in milliseconds (default: 5000).</param>
    /// <returns>Evaluation result with value or error.</returns>
    [McpServerTool(Name = "evaluate", Title = "Evaluate Expression",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Evaluate a C# expression in the debuggee context. The process must be paused. Supports: literals; locals/arguments/this and bare instance fields; member access and property getters; indexers (arrays, List<T>, string, Dictionary with value-type keys); instance method calls; arithmetic, comparison, logical, bitwise and conditional (?:) operators; casts; and string interpolation. Examples: 'myList.Count', 'customer.Name.ToUpper()', 'x + y * 2', 'tags[0]', 'a > 0 ? a : -a'. NOT supported (returns a 'not_supported' error): lambdas / LINQ query & method syntax, and reference-typed (string/object) method or indexer arguments. Returns: value (string representation), type (CLR type name), has_children flag. On failure: error with code and message. Note: method calls and property getters run real code in the debuggee and may have side effects. Example response: {\"success\": true, \"value\": \"42\", \"type\": \"System.Int32\", \"has_children\": false}")]
    public async Task<EvaluateResult> EvaluateAsync(
        [Description("C# expression to evaluate")] string expression,
        [Description("Thread context (default: current thread)")] int? thread_id = null,
        [Description("Stack frame context (0 = top)")] int frame_index = 0,
        [Description("Evaluation timeout in milliseconds")] int timeout_ms = 5000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("evaluate",
            $"{{\"expression\": \"{EscapeJsonString(expression)}\", \"thread_id\": {(thread_id?.ToString() ?? "null")}, \"frame_index\": {frame_index}, \"timeout_ms\": {timeout_ms}}}");

        try
        {
            // Validate expression parameter
            if (string.IsNullOrWhiteSpace(expression))
            {
                return CreateErrorResult("syntax_error", "Expression cannot be empty", position: 0);
            }

            // Validate timeout range (100-60000)
            if (timeout_ms < 100 || timeout_ms > 60000)
            {
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    "timeout_ms must be between 100 and 60000",
                    new { parameter = "timeout_ms", value = timeout_ms });
            }

            // Validate frame_index
            if (frame_index < 0)
            {
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    "frame_index must be >= 0",
                    new { parameter = "frame_index", value = frame_index });
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("evaluate", ErrorCodes.NoSession);
                return CreateErrorResult(ErrorCodes.NoSession, "No active debug session");
            }

            // Check if paused
            if (session.State != SessionState.Paused)
            {
                _logger.ToolError("evaluate", ErrorCodes.NotPaused);
                return CreateErrorResult(ErrorCodes.NotPaused,
                    $"Cannot evaluate expression: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})");
            }

            // Evaluate expression
            using var timeoutCts = new CancellationTokenSource(timeout_ms);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var result = await _sessionManager.EvaluateAsync(expression, thread_id, frame_index, timeout_ms, linkedCts.Token);

            stopwatch.Stop();
            _logger.ToolCompleted("evaluate", stopwatch.ElapsedMilliseconds);

            if (result.Success)
            {
                _logger.LogInformation("Evaluated expression '{Expression}' = {Value} ({Type})",
                    expression, result.Value ?? "null", result.Type ?? "void");

                return new EvaluateResult(
                    Success: true,
                    Value: result.Value,
                    Type: result.Type,
                    HasChildren: result.HasChildren);
            }
            else
            {
                _logger.LogWarning("Expression evaluation failed: {Code} - {Message}",
                    result.Error?.Code, result.Error?.Message);

                return CreateEvaluationErrorResult(result.Error!);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active debug session"))
        {
            _logger.ToolError("evaluate", ErrorCodes.NoSession);
            return CreateErrorResult(ErrorCodes.NoSession, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not paused"))
        {
            _logger.ToolError("evaluate", ErrorCodes.NotPaused);
            return CreateErrorResult(ErrorCodes.NotPaused, ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.ToolError("evaluate", "eval_timeout");
            return CreateErrorResult("eval_timeout", "Operation was cancelled");
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("evaluate", "eval_timeout");
            return CreateErrorResult("eval_timeout",
                $"Expression evaluation timed out after {timeout_ms}ms");
        }
        catch (Exception ex)
        {
            _logger.ToolError("evaluate", "eval_exception");
            return CreateErrorResult("eval_exception", ex.Message,
                new { exception_type = ex.GetType().FullName });
        }
    }

    /// <summary>
    /// Builds a failure result. The pre-US3 wire shape put an optional <c>position</c> directly
    /// on the error object (alongside code/message); the shared <see cref="ToolError"/> type has
    /// no such field, so <paramref name="position"/> is folded into <c>Error.Details</c> instead
    /// (as <c>{ position }</c>) — same value, now one level deeper on the wire.
    /// </summary>
    private static EvaluateResult CreateErrorResult(string code, string message, object? details = null, int? position = null)
    {
        var errorDetails = position.HasValue ? (object)new { position = position.Value } : details;
        return new EvaluateResult(Success: false, Error: new ToolError(code, message, errorDetails));
    }

    /// <summary>
    /// Builds a failure result from an <see cref="EvaluationError"/>. The pre-US3 wire shape put
    /// optional <c>exception_type</c>/<c>position</c> directly on the error object; both are
    /// folded into <c>Error.Details</c> here for the same reason as <see cref="CreateErrorResult"/>.
    /// </summary>
    private static EvaluateResult CreateEvaluationErrorResult(EvaluationError error)
    {
        var hasExceptionType = !string.IsNullOrEmpty(error.ExceptionType);
        object? details = (hasExceptionType, error.Position) switch
        {
            (true, { } position) => new { exception_type = error.ExceptionType, position },
            (true, null) => new { exception_type = error.ExceptionType },
            (false, { } position) => new { position },
            _ => null
        };

        return new EvaluateResult(Success: false, Error: new ToolError(error.Code, error.Message, details));
    }

    private static string EscapeJsonString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
