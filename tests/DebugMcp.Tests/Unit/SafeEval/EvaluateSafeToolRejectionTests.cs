using System.Text.Json;
using DebugMcp.Services;
using DebugMcp.Services.SafeEval;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FluentAssertions;

namespace DebugMcp.Tests.Unit.SafeEval;

public class EvaluateSafeToolRejectionTests
{
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

        var json = await tool.EvaluateSafeAsync("repo.Save(entity)");
        var doc = JsonDocument.Parse(json).RootElement;

        doc.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.GetProperty("error").GetProperty("code").GetString().Should().Be("safe_eval_rejected");
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

        var json = await tool.EvaluateSafeAsync("new List<int>()");
        var doc = JsonDocument.Parse(json).RootElement;

        doc.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.GetProperty("error").GetProperty("code").GetString().Should().Be("safe_eval_rejected");
        doc.GetProperty("error").GetProperty("details").GetProperty("rejection_category").GetString()
            .Should().Be("ObjectCreation");
    }

    // ── Assignment rejection shape ─────────────────────────────────────────

    [Fact]
    public async Task AssignmentRejection_ResponseHasCorrectShape()
    {
        var rejection = new SafeEvalRejection(RejectionCategory.Assignment, "x = 5", "Assignment not allowed");
        var tool = CreateTool(AnalyzerReturning(SafeAnalysisResult.Rejected(rejection)));

        var json = await tool.EvaluateSafeAsync("x = 5");
        var doc = JsonDocument.Parse(json).RootElement;

        doc.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.GetProperty("error").GetProperty("code").GetString().Should().Be("safe_eval_rejected");
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

        var json = await tool.EvaluateSafeAsync("db.Drop()");
        var doc = JsonDocument.Parse(json).RootElement;

        // Must be safe_eval_rejected, NOT no_session
        doc.GetProperty("error").GetProperty("code").GetString().Should().Be("safe_eval_rejected",
            "safety check must run before session check");
    }
}
