using System.Text.Json;
using System.Text.Json.Serialization;
using DebugMcp.Services;
using DebugMcp.Services.SafeEval;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AwesomeAssertions;

namespace DebugMcp.Tests.Unit.SafeEval;

public class EvaluateSafeToolRejectionTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static EvaluateSafeTool CreateTool(ISafeExpressionAnalyzer analyzer)
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        return new EvaluateSafeTool(sessionManager.Object, analyzer, NullLogger<EvaluateSafeTool>.Instance);
    }

    private static ISafeExpressionAnalyzer AnalyzerReturning(SafeAnalysisResult result)
    {
        var mock = new Mock<ISafeExpressionAnalyzer>();
        mock.Setup(a => a.Analyze(It.IsAny<string>())).Returns(result);
        return mock.Object;
    }

    // ── MethodCall rejection shape ─────────────────────────────────────────

    [Fact]
    public async Task MethodCallRejection_ResponseHasCorrectShape()
    {
        var rejection = new SafeEvalRejection(RejectionCategory.MethodCall, "repo.Save(entity)", "Method call 'repo.Save' is not allowed");
        var tool = CreateTool(AnalyzerReturning(SafeAnalysisResult.Rejected(rejection)));

        var result = await tool.EvaluateSafeAsync("repo.Save(entity)");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("safe_eval_rejected");

        var doc = JsonSerializer.SerializeToElement(result, WireOptions);
        doc.GetProperty("error").GetProperty("details").GetProperty("rejection_category").GetString()
            .Should().Be("MethodCall");
        doc.GetProperty("error").GetProperty("details").GetProperty("offending_expression").GetString()
            .Should().Contain("Save");
        doc.GetProperty("error").GetProperty("details").GetProperty("allowed_operations").GetString()
            .Should().NotBeNullOrWhiteSpace();
    }

    // ── ObjectCreation rejection shape ────────────────────────────────────

    [Fact]
    public async Task ObjectCreationRejection_ResponseHasCorrectShape()
    {
        var rejection = new SafeEvalRejection(RejectionCategory.ObjectCreation, "new List<int>()", "Object construction not allowed");
        var tool = CreateTool(AnalyzerReturning(SafeAnalysisResult.Rejected(rejection)));

        var result = await tool.EvaluateSafeAsync("new List<int>()");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("safe_eval_rejected");

        var doc = JsonSerializer.SerializeToElement(result, WireOptions);
        doc.GetProperty("error").GetProperty("details").GetProperty("rejection_category").GetString()
            .Should().Be("ObjectCreation");
    }

    // ── Assignment rejection shape ─────────────────────────────────────────

    [Fact]
    public async Task AssignmentRejection_ResponseHasCorrectShape()
    {
        var rejection = new SafeEvalRejection(RejectionCategory.Assignment, "x = 5", "Assignment not allowed");
        var tool = CreateTool(AnalyzerReturning(SafeAnalysisResult.Rejected(rejection)));

        var result = await tool.EvaluateSafeAsync("x = 5");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("safe_eval_rejected");

        var doc = JsonSerializer.SerializeToElement(result, WireOptions);
        doc.GetProperty("error").GetProperty("details").GetProperty("rejection_category").GetString()
            .Should().Be("Assignment");
    }

    // ── Safety check runs before session check ─────────────────────────────

    [Fact]
    public async Task BlockedExpression_RejectsWithoutSession_SafetyFirst()
    {
        // Analyzer returns a rejection; no session is set up
        var rejection = new SafeEvalRejection(RejectionCategory.MethodCall, "db.Drop()", "Not allowed");
        var tool = CreateTool(AnalyzerReturning(SafeAnalysisResult.Rejected(rejection)));

        var result = await tool.EvaluateSafeAsync("db.Drop()");

        // Must be safe_eval_rejected, NOT no_session
        result.Error!.Code.Should().Be("safe_eval_rejected",
            "safety check must run before session check");
    }
}
