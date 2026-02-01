using DebugMcp.Services.Breakpoints;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit;

public class DebuggerConditionEvaluatorTests
{
    private readonly DebuggerConditionEvaluator _evaluator;

    public DebuggerConditionEvaluatorTests()
    {
        var simpleEvaluator = new SimpleConditionEvaluator();
        var logger = new Mock<ILogger<DebuggerConditionEvaluator>>();
        _evaluator = new DebuggerConditionEvaluator(simpleEvaluator, logger.Object);
    }

    [Fact]
    public void Evaluate_SimpleCondition_DelegatesToSimpleEvaluator()
    {
        var ctx = new ConditionContext { HitCount = 5 };
        var result = _evaluator.Evaluate("hitCount == 5", ctx);

        result.Success.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_BooleanLiteral_DelegatesToSimpleEvaluator()
    {
        var result = _evaluator.Evaluate("true", new ConditionContext());
        result.Success.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_VariableComparison_ResolvesAndCompares()
    {
        var ctx = new ConditionContext
        {
            EvaluateExpression = expr => Task.FromResult<object?>(expr == "x" ? 10 : null)
        };

        var result = _evaluator.Evaluate("x > 5", ctx);

        result.Success.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_VariableComparisonFalse_ReturnsFalse()
    {
        var ctx = new ConditionContext
        {
            EvaluateExpression = expr => Task.FromResult<object?>(expr == "x" ? 3 : null)
        };

        var result = _evaluator.Evaluate("x > 5", ctx);

        result.Success.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_StringComparison_Works()
    {
        var ctx = new ConditionContext
        {
            EvaluateExpression = expr => Task.FromResult<object?>("test")
        };

        var result = _evaluator.Evaluate("name == \"test\"", ctx);

        result.Success.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NullComparison_Works()
    {
        var ctx = new ConditionContext
        {
            EvaluateExpression = _ => Task.FromResult<object?>(null)
        };

        var result = _evaluator.Evaluate("obj == null", ctx);

        result.Success.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NoEvaluateExpression_FailOpen()
    {
        var ctx = new ConditionContext(); // No EvaluateExpression

        var result = _evaluator.Evaluate("x > 5", ctx);

        result.Success.Should().BeTrue();
        result.Value.Should().BeTrue("should fail-open when no evaluator available");
    }

    [Fact]
    public void Evaluate_ExpressionThrows_FailOpen()
    {
        var ctx = new ConditionContext
        {
            EvaluateExpression = _ => throw new InvalidOperationException("Not at GC-safe point")
        };

        var result = _evaluator.Evaluate("x > 5", ctx);

        result.Success.Should().BeTrue();
        result.Value.Should().BeTrue("should fail-open on error");
    }

    [Fact]
    public void Evaluate_InvalidExpression_ReturnsError()
    {
        var result = _evaluator.Evaluate("not a valid expression!!!", new ConditionContext());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void ValidateCondition_ExpressionFormat_ReturnsValid()
    {
        var result = _evaluator.ValidateCondition("x > 5");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateCondition_PropertyPath_ReturnsValid()
    {
        var result = _evaluator.ValidateCondition("obj.Name == \"test\"");
        result.IsValid.Should().BeTrue();
    }
}
