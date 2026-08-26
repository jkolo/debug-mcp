using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Memory;
using DebugMcp.Services;
using DebugMcp.Services.Inspection;
using DebugMcp.Services.SafeEval;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-021/FR-017: a client that reads only <c>content[0].text</c>, as every client does today,
/// must see the same field names and meanings after US3's typed-result migration. Verified by
/// serializing each tool's returned record the way the SDK does (camelCase, nulls omitted — see
/// data-model.md §1), then parsing and comparing fields, not by string equality — the contract's
/// own wire example (contracts/tool-result-contract.md) is compact JSON, while today's
/// pre-migration hand-rolled tools used <c>WriteIndented = true</c>; indentation was never part
/// of the contract, field presence and values are. One case per migrated tool, added as each tool
/// moves off its hand-rolled <c>JsonSerializer.Serialize(new {...})</c> (T044–T052); this file
/// starts with the pilot (<c>snapshot_delete</c>) as the worked example the rest follows.
/// </summary>
public class LegacyTextContractTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public async Task SnapshotDelete_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var storeMock = new Mock<ISnapshotStore>();
        serviceMock.Setup(s => s.DeleteSnapshot("snap-1")).Returns(true);
        storeMock.Setup(s => s.Count).Returns(3);
        var tool = new SnapshotDeleteTool(serviceMock.Object, storeMock.Object, Mock.Of<ILogger<SnapshotDeleteTool>>());

        var result = await tool.DeleteSnapshotAsync("snap-1");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("deleted").GetString().Should().Be("snap-1");
        parsed.GetProperty("remaining").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task SnapshotDelete_Failure_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var storeMock = new Mock<ISnapshotStore>();
        serviceMock.Setup(s => s.DeleteSnapshot("snap-missing")).Returns(false);
        var tool = new SnapshotDeleteTool(serviceMock.Object, storeMock.Object, Mock.Of<ILogger<SnapshotDeleteTool>>());

        var result = await tool.DeleteSnapshotAsync("snap-missing");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("SNAPSHOT_NOT_FOUND");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    private static DebugSession PausedSession() => new()
    {
        ProcessId = 1234,
        ProcessName = "TestApp",
        ExecutablePath = "/usr/bin/testapp",
        RuntimeVersion = ".NET 10.0",
        AttachedAt = DateTimeOffset.UtcNow,
        LaunchMode = LaunchMode.Launch,
        State = SessionState.Paused,
        ActiveThreadId = 1,
        PauseReason = PauseReason.Breakpoint
    };

    // ── variables_get ────────────────────────────────────────────────────

    [Fact]
    public async Task VariablesGet_Success_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns(PausedSession());
        sessionManager.Setup(m => m.GetVariables(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(new List<Variable>
            {
                new("count", "System.Int32", "42", VariableScope.Local, HasChildren: false),
                new("user", "MyApp.User", "{MyApp.User}", VariableScope.Local, HasChildren: true, ChildrenCount: 3, Path: "user")
            });
        var tool = new VariablesGetTool(sessionManager.Object, Mock.Of<ILogger<VariablesGetTool>>());

        var result = await tool.GetVariablesAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var variables = parsed.GetProperty("variables");
        var first = variables[0];
        first.GetProperty("name").GetString().Should().Be("count");
        first.GetProperty("type").GetString().Should().Be("System.Int32");
        first.GetProperty("value").GetString().Should().Be("42");
        first.GetProperty("scope").GetString().Should().Be("local");
        first.GetProperty("has_children").GetBoolean().Should().BeFalse();
        first.TryGetProperty("children_count", out _).Should().BeFalse("children_count omitted when absent");
        first.TryGetProperty("path", out _).Should().BeFalse("path omitted when absent");

        var second = variables[1];
        second.GetProperty("has_children").GetBoolean().Should().BeTrue();
        second.GetProperty("children_count").GetInt32().Should().Be(3);
        second.GetProperty("path").GetString().Should().Be("user");
    }

    [Fact]
    public async Task VariablesGet_Failure_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns((DebugSession?)null);
        var tool = new VariablesGetTool(sessionManager.Object, Mock.Of<ILogger<VariablesGetTool>>());

        var result = await tool.GetVariablesAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    // ── stacktrace_get ───────────────────────────────────────────────────

    [Fact]
    public async Task StacktraceGet_Success_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns(PausedSession());
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main()", "MyApp.dll", false,
                Location: new SourceLocation("/src/Program.cs", 42, 1, "Main"),
                Arguments: new List<Variable> { new("arg1", "System.Int32", "1", VariableScope.Argument, HasChildren: false) })
        };
        sessionManager.Setup(m => m.GetStackFrames(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((frames, frames.Count));
        var tool = new StacktraceGetTool(sessionManager.Object, Mock.Of<ILogger<StacktraceGetTool>>());

        var result = await tool.GetStackTraceAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.TryGetProperty("thread_id", out _).Should().BeTrue();
        parsed.GetProperty("total_frames").GetInt32().Should().Be(1);
        var frame = parsed.GetProperty("frames")[0];
        frame.GetProperty("index").GetInt32().Should().Be(0);
        frame.GetProperty("function").GetString().Should().Be("MyApp.Program.Main()");
        frame.GetProperty("module").GetString().Should().Be("MyApp.dll");
        frame.GetProperty("is_external").GetBoolean().Should().BeFalse();
        frame.GetProperty("frame_kind").GetString().Should().Be("sync");
        frame.GetProperty("is_awaiting").GetBoolean().Should().BeFalse();
        var location = frame.GetProperty("location");
        location.GetProperty("file").GetString().Should().Be("/src/Program.cs");
        location.GetProperty("line").GetInt32().Should().Be(42);
        var argument = frame.GetProperty("arguments")[0];
        argument.GetProperty("name").GetString().Should().Be("arg1");
        argument.GetProperty("has_children").GetBoolean().Should().BeFalse();
        parsed.TryGetProperty("raw_frames", out _).Should().BeFalse("raw_frames omitted unless include_raw is set");
    }

    [Fact]
    public async Task StacktraceGet_Failure_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns((DebugSession?)null);
        var tool = new StacktraceGetTool(sessionManager.Object, Mock.Of<ILogger<StacktraceGetTool>>());

        var result = await tool.GetStackTraceAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    // ── evaluate ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Evaluate_Success_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns(PausedSession());
        sessionManager.Setup(m => m.EvaluateAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(Success: true, Value: "42", Type: "System.Int32", HasChildren: false));
        var tool = new EvaluateTool(sessionManager.Object, Mock.Of<ILogger<EvaluateTool>>());

        var result = await tool.EvaluateAsync("1 + 41");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("value").GetString().Should().Be("42");
        parsed.GetProperty("type").GetString().Should().Be("System.Int32");
        parsed.GetProperty("has_children").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_Failure_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        var tool = new EvaluateTool(sessionManager.Object, Mock.Of<ILogger<EvaluateTool>>());

        var result = await tool.EvaluateAsync("   ");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("syntax_error");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().Be("Expression cannot be empty");
        parsed.TryGetProperty("has_children", out _).Should().BeFalse("has_children omitted on failure, matching the legacy {success, error}-only shape");
        parsed.TryGetProperty("value", out _).Should().BeFalse();
        parsed.TryGetProperty("type", out _).Should().BeFalse();
    }

    // ── evaluate_safe ────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateSafe_Success_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns(PausedSession());
        sessionManager.Setup(m => m.EvaluateAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(Success: true, Value: "3", Type: "System.Int32", HasChildren: false));
        var analyzer = new Mock<ISafeExpressionAnalyzer>();
        analyzer.Setup(a => a.Analyze(It.IsAny<string>())).Returns(SafeAnalysisResult.Allowed());
        var tool = new EvaluateSafeTool(sessionManager.Object, analyzer.Object, Mock.Of<ILogger<EvaluateSafeTool>>());

        var result = await tool.EvaluateSafeAsync("1 + 2");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("value").GetString().Should().Be("3");
        parsed.GetProperty("type").GetString().Should().Be("System.Int32");
        parsed.GetProperty("has_children").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateSafe_Failure_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        var rejection = new SafeEvalRejection(RejectionCategory.MethodCall, "repo.Save(x)", "Method call 'repo.Save' is not allowed");
        var analyzer = new Mock<ISafeExpressionAnalyzer>();
        analyzer.Setup(a => a.Analyze(It.IsAny<string>())).Returns(SafeAnalysisResult.Rejected(rejection));
        var tool = new EvaluateSafeTool(sessionManager.Object, analyzer.Object, Mock.Of<ILogger<EvaluateSafeTool>>());

        var result = await tool.EvaluateSafeAsync("repo.Save(x)");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("safe_eval_rejected");
        parsed.GetProperty("error").GetProperty("details").GetProperty("rejection_category").GetString().Should().Be("MethodCall");
        parsed.TryGetProperty("has_children", out _).Should().BeFalse("has_children omitted on failure, matching the legacy {success, error}-only shape");
    }

    // ── object_inspect ───────────────────────────────────────────────────

    [Fact]
    public async Task ObjectInspect_Success_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns(PausedSession());
        sessionManager.Setup(m => m.InspectObjectAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectInspection
            {
                Address = "0x1234",
                TypeName = "MyApp.Customer",
                Size = 32,
                IsNull = false,
                HasCircularRef = false,
                Truncated = false,
                Fields = new List<FieldDetail>
                {
                    new() { Name = "_id", TypeName = "System.Int32", Value = "1", Offset = 8, Size = 4, HasChildren = false }
                }
            });
        var tool = new ObjectInspectTool(sessionManager.Object, Mock.Of<ILogger<ObjectInspectTool>>());

        var result = await tool.InspectObject("customer");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var inspection = parsed.GetProperty("inspection");
        inspection.GetProperty("address").GetString().Should().Be("0x1234");
        inspection.GetProperty("typeName").GetString().Should().Be("MyApp.Customer");
        inspection.GetProperty("size").GetInt32().Should().Be(32);
        inspection.GetProperty("isNull").GetBoolean().Should().BeFalse();
        inspection.GetProperty("hasCircularRef").GetBoolean().Should().BeFalse();
        inspection.GetProperty("truncated").GetBoolean().Should().BeFalse();
        var field = inspection.GetProperty("fields")[0];
        field.GetProperty("name").GetString().Should().Be("_id");
        field.GetProperty("typeName").GetString().Should().Be("System.Int32");
        field.GetProperty("hasChildren").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ObjectInspect_Failure_PreservesLegacyFieldNames()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns((DebugSession?)null);
        var tool = new ObjectInspectTool(sessionManager.Object, Mock.Of<ILogger<ObjectInspectTool>>());

        var result = await tool.InspectObject("customer");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    // ── object_summarize ─────────────────────────────────────────────────

    [Fact]
    public async Task ObjectSummarize_Success_PreservesLegacyFieldNames()
    {
        var summarizer = new Mock<IObjectSummarizer>();
        summarizer.Setup(s => s.SummarizeAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectSummary(
                TypeName: "MyApp.Customer",
                Size: 32,
                IsNull: false,
                Fields: new List<FieldSummary> { new("Name", "System.String", "\"Alice\"") },
                NullFields: new List<string> { "MiddleName" },
                InterestingFields: new List<InterestingField> { new("Age", "System.Int32", "0", "default value") },
                InaccessibleFieldCount: 0,
                TotalFieldCount: 3));
        var tool = new ObjectSummarizeTool(summarizer.Object, Mock.Of<ILogger<ObjectSummarizeTool>>());

        var result = await tool.SummarizeObject("customer");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var summary = parsed.GetProperty("summary");
        summary.GetProperty("typeName").GetString().Should().Be("MyApp.Customer");
        summary.GetProperty("totalFieldCount").GetInt32().Should().Be(3);
        summary.GetProperty("inaccessibleFieldCount").GetInt32().Should().Be(0);
        summary.GetProperty("fields")[0].GetProperty("name").GetString().Should().Be("Name");
        summary.GetProperty("nullFields")[0].GetString().Should().Be("MiddleName");
        var interesting = summary.GetProperty("interestingFields")[0];
        interesting.GetProperty("name").GetString().Should().Be("Age");
        interesting.GetProperty("reason").GetString().Should().Be("default value");
    }

    [Fact]
    public async Task ObjectSummarize_Failure_PreservesLegacyFieldNames()
    {
        var summarizer = new Mock<IObjectSummarizer>();
        summarizer.Setup(s => s.SummarizeAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No active debug session"));
        var tool = new ObjectSummarizeTool(summarizer.Object, Mock.Of<ILogger<ObjectSummarizeTool>>());

        var result = await tool.SummarizeObject("customer");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }
}
