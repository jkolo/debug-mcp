using DebugMcp.Services.Breakpoints;
using FluentAssertions;

namespace DebugMcp.Tests.Unit;

public class ConditionExpressionParserTests
{
    [Theory]
    [InlineData("x > 5", "x", ComparisonOperator.GreaterThan, 5, LiteralType.Int)]
    [InlineData("count == 10", "count", ComparisonOperator.Equal, 10, LiteralType.Int)]
    [InlineData("value != 0", "value", ComparisonOperator.NotEqual, 0, LiteralType.Int)]
    [InlineData("x >= 100", "x", ComparisonOperator.GreaterThanOrEqual, 100, LiteralType.Int)]
    [InlineData("x < 3", "x", ComparisonOperator.LessThan, 3, LiteralType.Int)]
    [InlineData("x <= 99", "x", ComparisonOperator.LessThanOrEqual, 99, LiteralType.Int)]
    public void TryParse_IntegerComparison_ParsesCorrectly(
        string expr, string expectedLhs, ComparisonOperator expectedOp, int expectedRhs, LiteralType expectedType)
    {
        var success = ConditionExpressionParser.TryParse(expr, out var result);

        success.Should().BeTrue();
        result!.LeftHandSide.Should().Be(expectedLhs);
        result.Operator.Should().Be(expectedOp);
        result.RightHandSide.Type.Should().Be(expectedType);
        result.RightHandSide.Value.Should().Be(expectedRhs);
    }

    [Fact]
    public void TryParse_StringComparison_ParsesCorrectly()
    {
        var success = ConditionExpressionParser.TryParse("obj.Name == \"test\"", out var result);

        success.Should().BeTrue();
        result!.LeftHandSide.Should().Be("obj.Name");
        result.Operator.Should().Be(ComparisonOperator.Equal);
        result.RightHandSide.Type.Should().Be(LiteralType.String);
        result.RightHandSide.Value.Should().Be("test");
    }

    [Fact]
    public void TryParse_NullComparison_ParsesCorrectly()
    {
        var success = ConditionExpressionParser.TryParse("obj == null", out var result);

        success.Should().BeTrue();
        result!.LeftHandSide.Should().Be("obj");
        result.Operator.Should().Be(ComparisonOperator.Equal);
        result.RightHandSide.Type.Should().Be(LiteralType.Null);
    }

    [Fact]
    public void TryParse_BoolComparison_ParsesCorrectly()
    {
        var success = ConditionExpressionParser.TryParse("flag == true", out var result);

        success.Should().BeTrue();
        result!.LeftHandSide.Should().Be("flag");
        result.Operator.Should().Be(ComparisonOperator.Equal);
        result.RightHandSide.Type.Should().Be(LiteralType.Bool);
        result.RightHandSide.Value.Should().Be(true);
    }

    [Fact]
    public void TryParse_MethodCall_ParsesCorrectly()
    {
        var success = ConditionExpressionParser.TryParse("obj.ToString() == \"hello\"", out var result);

        success.Should().BeTrue();
        result!.LeftHandSide.Should().Be("obj.ToString()");
        result.Operator.Should().Be(ComparisonOperator.Equal);
        result.RightHandSide.Type.Should().Be(LiteralType.String);
        result.RightHandSide.Value.Should().Be("hello");
    }

    [Fact]
    public void TryParse_PropertyPath_ParsesCorrectly()
    {
        var success = ConditionExpressionParser.TryParse("obj.Inner.Value > 10", out var result);

        success.Should().BeTrue();
        result!.LeftHandSide.Should().Be("obj.Inner.Value");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hitCount == 5")] // This is a hitCount expression, not variable
    [InlineData("just_a_variable")]
    [InlineData("5 > x")] // LHS must be identifier
    public void TryParse_InvalidExpressions_ReturnsFalse(string expr)
    {
        var success = ConditionExpressionParser.TryParse(expr, out var result);
        success.Should().BeFalse();
        result.Should().BeNull();
    }
}
