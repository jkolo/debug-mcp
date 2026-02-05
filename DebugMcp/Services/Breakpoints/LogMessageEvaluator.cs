using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Breakpoints;

/// <summary>
/// Service for evaluating tracepoint log message templates with {expression} placeholders.
/// </summary>
public sealed partial class LogMessageEvaluator
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<LogMessageEvaluator> _logger;

    // Regex pattern to match {expression} placeholders
    // Matches: {variableName}, {obj.Property}, {arr[0]}, etc.
    // Does not match: {{ (escaped braces)
    [GeneratedRegex(@"\{(?!{)([^}]+)\}")]
    private static partial Regex ExpressionPattern();

    public LogMessageEvaluator(IDebugSessionManager sessionManager, ILogger<LogMessageEvaluator> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates a log message template by replacing {expression} placeholders with their values.
    /// </summary>
    /// <param name="logMessage">The log message template with {expression} placeholders.</param>
    /// <param name="threadId">Thread ID for evaluation context.</param>
    /// <param name="frameIndex">Frame index for evaluation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluated log message with placeholders replaced by values.</returns>
    public async Task<string> EvaluateLogMessageAsync(
        string? logMessage,
        int threadId,
        int frameIndex = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(logMessage))
        {
            return string.Empty;
        }

        // Handle escaped braces first: {{ -> PLACEHOLDER_OPEN, }} -> PLACEHOLDER_CLOSE
        var result = logMessage
            .Replace("{{", "\x00BRACE_OPEN\x00")
            .Replace("}}", "\x00BRACE_CLOSE\x00");

        // Find all {expression} placeholders
        var matches = ExpressionPattern().Matches(result);
        if (matches.Count == 0)
        {
            // No expressions to evaluate, just restore escaped braces
            return result
                .Replace("\x00BRACE_OPEN\x00", "{")
                .Replace("\x00BRACE_CLOSE\x00", "}");
        }

        _logger.LogDebug("Evaluating {Count} expressions in log message template", matches.Count);

        // Build list of expressions to evaluate
        var expressionResults = new Dictionary<string, string>();

        foreach (Match match in matches)
        {
            var expression = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(expression))
            {
                continue;
            }

            // Skip if we already evaluated this expression
            if (expressionResults.ContainsKey(expression))
            {
                continue;
            }

            try
            {
                var evalResult = await _sessionManager.EvaluateAsync(
                    expression,
                    threadId,
                    frameIndex,
                    timeoutMs: 1000, // Short timeout for log message evaluation
                    cancellationToken);

                if (evalResult.Success)
                {
                    expressionResults[expression] = evalResult.Value ?? "null";
                    _logger.LogDebug("Expression '{Expression}' evaluated to '{Value}'", expression, evalResult.Value);
                }
                else
                {
                    var errorMessage = evalResult.Error?.ExceptionType ?? evalResult.Error?.Code ?? "error";
                    expressionResults[expression] = $"<error: {errorMessage}>";
                    _logger.LogDebug("Expression '{Expression}' evaluation failed: {Error}", expression, errorMessage);
                }
            }
            catch (OperationCanceledException)
            {
                expressionResults[expression] = "<error: Timeout>";
                _logger.LogDebug("Expression '{Expression}' evaluation timed out", expression);
            }
            catch (Exception ex)
            {
                expressionResults[expression] = $"<error: {ex.GetType().Name}>";
                _logger.LogDebug(ex, "Expression '{Expression}' evaluation threw exception", expression);
            }
        }

        // Replace expressions with their evaluated values
        result = ExpressionPattern().Replace(result, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            return expressionResults.TryGetValue(expression, out var value) ? value : match.Value;
        });

        // Restore escaped braces
        result = result
            .Replace("\x00BRACE_OPEN\x00", "{")
            .Replace("\x00BRACE_CLOSE\x00", "}");

        return result;
    }
}
