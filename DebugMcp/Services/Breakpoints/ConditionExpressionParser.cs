using System.Text.RegularExpressions;

namespace DebugMcp.Services.Breakpoints;

/// <summary>
/// Parses condition expressions of the form: &lt;expr&gt; &lt;op&gt; &lt;literal&gt;
/// where expr is a variable/property path, op is a comparison operator,
/// and literal is int/string/bool/null.
/// </summary>
public static partial class ConditionExpressionParser
{
    /// <summary>
    /// Tries to parse a condition expression into its components.
    /// </summary>
    /// <param name="expression">The condition expression string.</param>
    /// <param name="result">The parsed expression if successful.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParse(string expression, out ParsedConditionExpression? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        expression = expression.Trim();

        // Skip hitCount expressions — handled by SimpleConditionEvaluator
        if (expression.StartsWith("hitCount", StringComparison.OrdinalIgnoreCase))
            return false;

        // Try method call: expr.Method() <op> literal
        var methodMatch = MethodCallExpressionRegex().Match(expression);
        if (methodMatch.Success)
        {
            var lhs = methodMatch.Groups["lhs"].Value;
            var op = ParseOperator(methodMatch.Groups["op"].Value);
            var rhs = ParseLiteral(methodMatch.Groups["rhs"].Value);
            if (op != null && rhs != null)
            {
                result = new ParsedConditionExpression(lhs, op.Value, rhs);
                return true;
            }
        }

        // Try comparison: expr <op> literal
        var comparisonMatch = ComparisonExpressionRegex().Match(expression);
        if (comparisonMatch.Success)
        {
            var lhs = comparisonMatch.Groups["lhs"].Value.Trim();
            var op = ParseOperator(comparisonMatch.Groups["op"].Value);
            var rhs = ParseLiteral(comparisonMatch.Groups["rhs"].Value.Trim());
            if (op != null && rhs != null)
            {
                result = new ParsedConditionExpression(lhs, op.Value, rhs);
                return true;
            }
        }

        return false;
    }

    private static ComparisonOperator? ParseOperator(string op) => op switch
    {
        "==" => ComparisonOperator.Equal,
        "!=" => ComparisonOperator.NotEqual,
        ">" => ComparisonOperator.GreaterThan,
        ">=" => ComparisonOperator.GreaterThanOrEqual,
        "<" => ComparisonOperator.LessThan,
        "<=" => ComparisonOperator.LessThanOrEqual,
        _ => null
    };

    private static ConditionLiteral? ParseLiteral(string value)
    {
        value = value.Trim();

        // null
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
            return new ConditionLiteral(null, LiteralType.Null);

        // bool
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return new ConditionLiteral(true, LiteralType.Bool);
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return new ConditionLiteral(false, LiteralType.Bool);

        // string (quoted)
        if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
            return new ConditionLiteral(value[1..^1], LiteralType.String);

        // int
        if (int.TryParse(value, out var intVal))
            return new ConditionLiteral(intVal, LiteralType.Int);

        // double
        if (double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
            return new ConditionLiteral(doubleVal, LiteralType.Double);

        return null;
    }

    // Matches: expr.Method() <op> literal  OR  expr <op> literal
    // LHS can be: variable, property path (a.b.c), method call (a.Method())
    [GeneratedRegex(@"^(?<lhs>[a-zA-Z_][\w.]*\(\))\s*(?<op>==|!=|>=|<=|>|<)\s*(?<rhs>.+)$")]
    private static partial Regex MethodCallExpressionRegex();

    [GeneratedRegex(@"^(?<lhs>[a-zA-Z_][\w.]*)\s*(?<op>==|!=|>=|<=|>|<)\s*(?<rhs>.+)$")]
    private static partial Regex ComparisonExpressionRegex();
}

/// <summary>
/// A parsed condition expression: LHS op RHS.
/// </summary>
public record ParsedConditionExpression(
    string LeftHandSide,
    ComparisonOperator Operator,
    ConditionLiteral RightHandSide);

/// <summary>
/// A literal value in a condition expression.
/// </summary>
public record ConditionLiteral(object? Value, LiteralType Type);

/// <summary>
/// Type of literal in a condition.
/// </summary>
public enum LiteralType { Null, Bool, Int, Double, String }

/// <summary>
/// Comparison operators for condition expressions.
/// </summary>
public enum ComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}
