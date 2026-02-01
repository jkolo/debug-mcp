using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Breakpoints;

/// <summary>
/// Evaluates condition expressions that require debugger access (variable resolution,
/// property access, method calls via ICorDebugEval). Chained after SimpleConditionEvaluator.
/// </summary>
public sealed class DebuggerConditionEvaluator : IConditionEvaluator
{
    private readonly IConditionEvaluator _simpleEvaluator;
    private readonly ILogger<DebuggerConditionEvaluator> _logger;
    private static readonly TimeSpan FuncEvalTimeout = TimeSpan.FromSeconds(5);

    public DebuggerConditionEvaluator(
        IConditionEvaluator simpleEvaluator,
        ILogger<DebuggerConditionEvaluator> logger)
    {
        _simpleEvaluator = simpleEvaluator;
        _logger = logger;
    }

    /// <inheritdoc />
    public ConditionResult Evaluate(string? condition, ConditionContext context)
    {
        // First, try the simple evaluator (boolean literals, hit count)
        var simpleResult = _simpleEvaluator.Evaluate(condition, context);
        if (simpleResult.Success)
            return simpleResult;

        // Simple evaluator returned error (unsupported) — try expression parsing
        if (string.IsNullOrWhiteSpace(condition))
            return ConditionResult.Ok(true);

        if (!ConditionExpressionParser.TryParse(condition, out var parsed) || parsed == null)
        {
            return ConditionResult.Error(
                $"Cannot parse condition expression: '{condition}'. " +
                "Expected format: '<variable> <op> <literal>' (e.g., 'x > 5', 'name == \"test\"')");
        }

        // Need EvaluateExpression to resolve variables
        if (context.EvaluateExpression == null)
        {
            _logger.LogDebug("No EvaluateExpression function available, fail-open");
            return ConditionResult.Ok(true); // fail-open
        }

        try
        {
            // Resolve LHS value with timeout
            using var cts = new CancellationTokenSource(FuncEvalTimeout);
            var lhsTask = context.EvaluateExpression(parsed.LeftHandSide);
            if (!lhsTask.Wait(FuncEvalTimeout))
            {
                _logger.LogWarning("FuncEval timeout evaluating '{Expression}', fail-open", parsed.LeftHandSide);
                return ConditionResult.Ok(true); // fail-open on timeout
            }

            var lhsValue = lhsTask.Result;
            return CompareValues(lhsValue, parsed.Operator, parsed.RightHandSide);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error evaluating condition '{Condition}', fail-open", condition);
            return ConditionResult.Ok(true); // fail-open on error
        }
    }

    /// <inheritdoc />
    public ConditionValidation ValidateCondition(string? condition)
    {
        // Delegate basic validation to simple evaluator first
        var simpleValidation = _simpleEvaluator.ValidateCondition(condition);
        if (simpleValidation.IsValid)
            return simpleValidation;

        // Try parsing as an expression
        if (!string.IsNullOrWhiteSpace(condition) &&
            ConditionExpressionParser.TryParse(condition!, out _))
        {
            return ConditionValidation.Valid();
        }

        return simpleValidation;
    }

    private static ConditionResult CompareValues(
        object? lhsValue,
        ComparisonOperator op,
        ConditionLiteral rhs)
    {
        // null comparison
        if (rhs.Type == LiteralType.Null)
        {
            return op switch
            {
                ComparisonOperator.Equal => ConditionResult.Ok(lhsValue == null),
                ComparisonOperator.NotEqual => ConditionResult.Ok(lhsValue != null),
                _ => ConditionResult.Error($"Cannot use operator '{op}' with null")
            };
        }

        if (lhsValue == null)
        {
            return op switch
            {
                ComparisonOperator.Equal => ConditionResult.Ok(rhs.Value == null),
                ComparisonOperator.NotEqual => ConditionResult.Ok(rhs.Value != null),
                _ => ConditionResult.Error("Cannot compare null with non-null using this operator")
            };
        }

        // Numeric comparison
        if (rhs.Type is LiteralType.Int or LiteralType.Double)
        {
            if (TryConvertToDouble(lhsValue, out var lhsNum) && TryConvertToDouble(rhs.Value, out var rhsNum))
            {
                var cmp = lhsNum.CompareTo(rhsNum);
                return ConditionResult.Ok(EvaluateComparison(cmp, op));
            }

            return ConditionResult.Error(
                $"Cannot compare '{lhsValue}' (type {lhsValue.GetType().Name}) with numeric literal");
        }

        // String comparison
        if (rhs.Type == LiteralType.String)
        {
            var lhsStr = lhsValue.ToString() ?? "";
            var rhsStr = rhs.Value?.ToString() ?? "";
            var cmp = string.Compare(lhsStr, rhsStr, StringComparison.Ordinal);
            return ConditionResult.Ok(EvaluateComparison(cmp, op));
        }

        // Bool comparison
        if (rhs.Type == LiteralType.Bool)
        {
            if (lhsValue is bool lhsBool && rhs.Value is bool rhsBool)
            {
                return op switch
                {
                    ComparisonOperator.Equal => ConditionResult.Ok(lhsBool == rhsBool),
                    ComparisonOperator.NotEqual => ConditionResult.Ok(lhsBool != rhsBool),
                    _ => ConditionResult.Error($"Cannot use operator '{op}' with boolean values")
                };
            }
        }

        return ConditionResult.Error($"Cannot compare values of incompatible types");
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;
        if (value == null) return false;

        return value switch
        {
            int i => (result = i) == i,
            long l => (result = l) == l,
            float f => (result = f) is not double.NaN,
            double d => (result = d) is not double.NaN,
            short s => (result = s) == s,
            byte b => (result = b) == b,
            _ => double.TryParse(value.ToString(), System.Globalization.CultureInfo.InvariantCulture, out result)
        };
    }

    private static bool EvaluateComparison(int cmp, ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => cmp == 0,
        ComparisonOperator.NotEqual => cmp != 0,
        ComparisonOperator.GreaterThan => cmp > 0,
        ComparisonOperator.GreaterThanOrEqual => cmp >= 0,
        ComparisonOperator.LessThan => cmp < 0,
        ComparisonOperator.LessThanOrEqual => cmp <= 0,
        _ => false
    };
}
