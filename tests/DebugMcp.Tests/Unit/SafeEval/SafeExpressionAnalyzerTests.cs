using DebugMcp.Services.SafeEval;
using FluentAssertions;

namespace DebugMcp.Tests.Unit.SafeEval;

public class SafeExpressionAnalyzerTests
{
    // Analyzer with no allowlist (US1 phase — blocks ALL invocations).
    // Replaced in US2 with an allowlist-aware instance.
    private static ISafeExpressionAnalyzer CreateAnalyzer() =>
        new SafeExpressionAnalyzer(new SafeEvalAllowlist());

    // ── Allowed expressions ────────────────────────────────────────────────

    [Theory]
    [InlineData("user.Name")]
    [InlineData("list.Count")]
    [InlineData("a + b * 2")]
    [InlineData("x > 0 ? y : z")]
    [InlineData("arr[0]")]
    [InlineData("user?.Name")]
    [InlineData("42")]
    [InlineData("\"hello\"")]
    [InlineData("x > 0 && y < 10")]
    [InlineData("!flag")]
    public void Allowed_PureExpression_ReturnsAllowed(string expression)
    {
        var result = CreateAnalyzer().Analyze(expression);
        result.IsAllowed.Should().BeTrue($"'{expression}' should be allowed");
        result.Rejection.Should().BeNull();
    }

    // ── Blocked: method calls ──────────────────────────────────────────────

    [Theory]
    [InlineData("File.Delete(\"x\")")]
    [InlineData("db.Drop()")]
    [InlineData("list.Add(x)")]
    [InlineData("obj.GetList().Count")]
    public void Blocked_MethodCall_RejectsWithMethodCallCategory(string expression)
    {
        var result = CreateAnalyzer().Analyze(expression);
        result.IsAllowed.Should().BeFalse($"'{expression}' should be blocked");
        result.Rejection.Should().NotBeNull();
        result.Rejection!.Category.Should().Be(RejectionCategory.MethodCall);
    }

    // ── Blocked: object creation ───────────────────────────────────────────

    [Theory]
    [InlineData("new List<int>()")]
    [InlineData("new StringBuilder()")]
    public void Blocked_ObjectCreation_RejectsWithObjectCreationCategory(string expression)
    {
        var result = CreateAnalyzer().Analyze(expression);
        result.IsAllowed.Should().BeFalse();
        result.Rejection!.Category.Should().Be(RejectionCategory.ObjectCreation);
    }

    // ── Blocked: assignments ───────────────────────────────────────────────

    [Theory]
    [InlineData("x = 5")]
    [InlineData("x += 1")]
    public void Blocked_Assignment_RejectsWithAssignmentCategory(string expression)
    {
        var result = CreateAnalyzer().Analyze(expression);
        result.IsAllowed.Should().BeFalse();
        result.Rejection!.Category.Should().Be(RejectionCategory.Assignment);
    }

    // ── Parse error ────────────────────────────────────────────────────────

    [Fact]
    public void Blocked_SyntaxError_RejectsWithParseErrorCategory()
    {
        var result = CreateAnalyzer().Analyze("{ broken");
        result.IsAllowed.Should().BeFalse();
        result.Rejection!.Category.Should().Be(RejectionCategory.ParseError);
    }

    // ── Allowlisted methods (US2) ──────────────────────────────────────────

    [Theory]
    [InlineData("Math.Abs(delta)")]
    [InlineData("String.Format(\"{0}\", x)")]
    [InlineData("Enumerable.Count(list)")]
    public void Allowed_AllowlistedMethod_ReturnsAllowed(string expression)
    {
        var result = CreateAnalyzer().Analyze(expression);
        result.IsAllowed.Should().BeTrue($"'{expression}' should be allowed (in default allowlist)");
    }

    [Fact]
    public void Blocked_NonAllowlisted_IsRejected()
    {
        var result = CreateAnalyzer().Analyze("Console.WriteLine(\"x\")");
        result.IsAllowed.Should().BeFalse();
        result.Rejection!.Category.Should().Be(RejectionCategory.MethodCall);
    }

    [Fact]
    public void Blocked_AllowlistedWithUnsafeArgument_IsRejected()
    {
        // Math.Abs is allowlisted, but its argument list.Add(x) is not
        var result = CreateAnalyzer().Analyze("Math.Abs(list.Add(x))");
        result.IsAllowed.Should().BeFalse();
        result.Rejection!.Category.Should().Be(RejectionCategory.MethodCall);
    }

    // ── Performance (SC-003) ───────────────────────────────────────────────

    [Fact]
    public void SC003_RejectedExpression_AnalyzedUnder50ms()
    {
        var analyzer = CreateAnalyzer();
        // warm up
        analyzer.Analyze("File.Delete(\"x\")");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
            analyzer.Analyze("File.Delete(\"x\")");
        sw.Stop();

        var avgMs = sw.ElapsedMilliseconds / 10.0;
        avgMs.Should().BeLessThan(50, "SC-003: static analysis must complete in <50ms");
    }
}
