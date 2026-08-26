using AwesomeAssertions;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Memory;
using DebugMcp.Models.Modules;
using DebugMcp.Models;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Inspection;
using DebugMcp.Services.Progress;
using DebugMcp.Services.SafeEval;
using DebugMcp.Services.Snapshots;
using DebugMcp.Services;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
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
/// moves off its hand-rolled <c>JsonSerializer.Serialize(new {...})</c> (T044-T052); this file
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

    // ---- debug_attach (T044) ----

    [Fact]
    public async Task DebugAttach_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var session = new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.Parse("2026-01-15T10:30:00Z"),
            State = SessionState.Running,
            LaunchMode = LaunchMode.Attach,
        };
        sessionManagerMock.Setup(s => s.AttachAsync(1234, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var tool = new DebugAttachTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugAttachTool>>());

        var result = await tool.AttachAsync(1234);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var sessionJson = parsed.GetProperty("session");
        sessionJson.GetProperty("processId").GetInt32().Should().Be(1234);
        sessionJson.GetProperty("processName").GetString().Should().Be("MyApp");
        sessionJson.GetProperty("executablePath").GetString().Should().Be("/path/to/MyApp");
        sessionJson.GetProperty("runtimeVersion").GetString().Should().Be(".NET 10.0.0");
        sessionJson.GetProperty("state").GetString().Should().Be("running");
        sessionJson.GetProperty("launchMode").GetString().Should().Be("attach");
        sessionJson.GetProperty("attachedAt").GetString().Should().NotBeNullOrEmpty();
        sessionJson.TryGetProperty("pauseReason", out _).Should().BeFalse();
        sessionJson.TryGetProperty("commandLineArgs", out _).Should().BeFalse();
        sessionJson.TryGetProperty("workingDirectory", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DebugAttach_Failure_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var tool = new DebugAttachTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugAttachTool>>());

        var result = await tool.AttachAsync(0);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("PROCESS_NOT_FOUND");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        parsed.TryGetProperty("session", out _).Should().BeFalse();
    }

    // ---- debug_launch (T045) ----

    [Fact]
    public async Task DebugLaunch_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var session = new DebugSession
        {
            ProcessId = 5678,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp.dll",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.Parse("2026-01-15T10:30:00Z"),
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Launch,
            PauseReason = PauseReason.Entry,
            CommandLineArgs = ["--flag", "value"],
            WorkingDirectory = "/work",
        };
        sessionManagerMock.Setup(s => s.LaunchAsync(
                "MyApp.dll", null, null, null, true,
                It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgressReporter?>()))
            .ReturnsAsync(session);
        var tool = new DebugLaunchTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugLaunchTool>>());

        var result = await tool.LaunchAsync("MyApp.dll");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var sessionJson = parsed.GetProperty("session");
        sessionJson.GetProperty("processId").GetInt32().Should().Be(5678);
        sessionJson.GetProperty("state").GetString().Should().Be("paused");
        sessionJson.GetProperty("launchMode").GetString().Should().Be("launch");
        sessionJson.GetProperty("pauseReason").GetString().Should().Be("entry");
        sessionJson.GetProperty("commandLineArgs").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("--flag", "value");
        sessionJson.GetProperty("workingDirectory").GetString().Should().Be("/work");
    }

    [Fact]
    public async Task DebugLaunch_Failure_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var tool = new DebugLaunchTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugLaunchTool>>());

        var result = await tool.LaunchAsync(string.Empty);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_PATH");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    // ---- debug_disconnect (T046) ----

    [Fact]
    public async Task DebugDisconnect_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var session = new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Running,
            LaunchMode = LaunchMode.Launch,
        };
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(session);
        sessionManagerMock.Setup(s => s.DisconnectAsync(false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var tool = new DebugDisconnectTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugDisconnectTool>>());

        var result = await tool.DisconnectAsync(false);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("state").GetString().Should().Be("disconnected");
        parsed.GetProperty("wasTerminated").GetBoolean().Should().BeFalse();
        var previousSession = parsed.GetProperty("previousSession");
        previousSession.GetProperty("processId").GetInt32().Should().Be(1234);
        previousSession.GetProperty("processName").GetString().Should().Be("MyApp");
        previousSession.GetProperty("launchMode").GetString().Should().Be("launch");
        parsed.TryGetProperty("message", out _).Should().BeFalse();
        parsed.TryGetProperty("timedOut", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DebugDisconnect_Failure_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var session = new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Running,
            LaunchMode = LaunchMode.Launch,
        };
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(session);
        sessionManagerMock.Setup(s => s.DisconnectAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var tool = new DebugDisconnectTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugDisconnectTool>>());

        var result = await tool.DisconnectAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("DISCONNECT_FAILED");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().Contain("boom");
        parsed.GetProperty("error").TryGetProperty("details", out _).Should().BeFalse();
    }

    // ---- debug_continue (T047) ----

    [Fact]
    public async Task DebugContinue_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var currentSession = new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "App",
            ExecutablePath = "/e",
            RuntimeVersion = "v",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Launch,
        };
        var updatedSession = new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "App",
            ExecutablePath = "/e",
            RuntimeVersion = "v",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Running,
            LaunchMode = LaunchMode.Launch,
        };
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(currentSession);
        sessionManagerMock.Setup(s => s.ContinueAsync(It.IsAny<CancellationToken>())).ReturnsAsync(updatedSession);
        var tool = new DebugContinueTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugContinueTool>>());

        var result = await tool.ContinueAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var sessionJson = parsed.GetProperty("session");
        sessionJson.GetProperty("processId").GetInt32().Should().Be(1234);
        sessionJson.GetProperty("processName").GetString().Should().Be("App");
        sessionJson.GetProperty("state").GetString().Should().Be("running");
        sessionJson.GetProperty("launchMode").GetString().Should().Be("launch");
        sessionJson.TryGetProperty("pauseReason", out _).Should().BeFalse();
        sessionJson.TryGetProperty("location", out _).Should().BeFalse();
        sessionJson.TryGetProperty("activeThreadId", out _).Should().BeFalse();
        // Distinct, smaller "session" shape than debug_attach/debug_launch's SessionSummary.
        sessionJson.TryGetProperty("executablePath", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DebugContinue_Failure_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);
        var tool = new DebugContinueTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugContinueTool>>());

        var result = await tool.ContinueAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    // ---- debug_step (T048) ----

    [Fact]
    public async Task DebugStep_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var currentSession = new DebugSession
        {
            ProcessId = 55,
            ProcessName = "App",
            ExecutablePath = "/e",
            RuntimeVersion = "v",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Launch,
        };
        var updatedSession = new DebugSession
        {
            ProcessId = 55,
            ProcessName = "App",
            ExecutablePath = "/e",
            RuntimeVersion = "v",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Launch,
            PauseReason = PauseReason.Step,
            CurrentLocation = new SourceLocation("Program.cs", 43, 5, "Main"),
            ActiveThreadId = 1,
        };
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(currentSession);
        sessionManagerMock.Setup(s => s.StepAsync(StepMode.Over, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedSession);
        var tool = new DebugStepTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugStepTool>>());

        var result = await tool.StepAsync("over");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("stepMode").GetString().Should().Be("over");
        var sessionJson = parsed.GetProperty("session");
        sessionJson.GetProperty("pauseReason").GetString().Should().Be("step");
        var location = sessionJson.GetProperty("location");
        location.GetProperty("file").GetString().Should().Be("Program.cs");
        location.GetProperty("line").GetInt32().Should().Be(43);
        location.GetProperty("column").GetInt32().Should().Be(5);
        location.GetProperty("functionName").GetString().Should().Be("Main");
        sessionJson.GetProperty("activeThreadId").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task DebugStep_Failure_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var tool = new DebugStepTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugStepTool>>());

        var result = await tool.StepAsync("sideways");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_PARAMETER");
    }

    // ---- debug_pause (T049) ----

    [Fact]
    public async Task DebugPause_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var session = new DebugSession
        {
            ProcessId = 99,
            ProcessName = "App",
            ExecutablePath = "/e",
            RuntimeVersion = "v",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Attach,
        };
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(session);
        var threads = new List<ThreadInfo>
        {
            new(1, "main", DebugMcp.Models.Inspection.ThreadState.Stopped, true, new SourceLocation("Program.cs", 20, null, "Main")),
            new(2, null, DebugMcp.Models.Inspection.ThreadState.Stopped, false, new SourceLocation(string.Empty, 0, null, null)),
        };
        sessionManagerMock.Setup(s => s.GetThreads()).Returns(threads);
        var tool = new DebugPauseTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugPauseTool>>());

        var result = await tool.PauseAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var threadsJson = parsed.GetProperty("threads");
        threadsJson.GetArrayLength().Should().Be(2);

        var t1 = threadsJson[0];
        t1.GetProperty("id").GetInt32().Should().Be(1);
        var loc1 = t1.GetProperty("location");
        loc1.GetProperty("function").GetString().Should().Be("Main");
        loc1.GetProperty("file").GetString().Should().Be("Program.cs");
        loc1.GetProperty("line").GetInt32().Should().Be(20);

        var t2 = threadsJson[1];
        t2.GetProperty("id").GetInt32().Should().Be(2);
        var loc2 = t2.GetProperty("location");
        loc2.GetProperty("function").GetString().Should().Be("Unknown");
        loc2.TryGetProperty("file", out _).Should().BeFalse();
        loc2.TryGetProperty("line", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DebugPause_Failure_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);
        var tool = new DebugPauseTool(sessionManagerMock.Object, Mock.Of<ILogger<DebugPauseTool>>());

        var result = await tool.PauseAsync();
    }

    private static DebugSession PausedSession() => new()
    {
        ProcessId = 1234,
        ProcessName = "MyApp",
        ExecutablePath = "/bin/MyApp",
        RuntimeVersion = ".NET 10.0",
        AttachedAt = DateTimeOffset.UtcNow,
        LaunchMode = LaunchMode.Launch,
        State = SessionState.Paused,
        ActiveThreadId = 1,
        PauseReason = PauseReason.Breakpoint,
    };

    private static DebugSession RunningSession() => new()
    {
        ProcessId = 1234,
        ProcessName = "MyApp",
        ExecutablePath = "/bin/MyApp",
        RuntimeVersion = ".NET 10.0",
        AttachedAt = DateTimeOffset.UtcNow,
        LaunchMode = LaunchMode.Launch,
        State = SessionState.Running,
    };

    [Fact]
    public async Task CollectionAnalyze_Success_PreservesLegacyFieldNames()
    {
        var analyzerMock = new Mock<ICollectionAnalyzer>();
        var summary = new CollectionSummary(
            Count: 3,
            ElementType: "System.Int32",
            CollectionType: "System.Collections.Generic.List`1",
            Kind: CollectionKind.List,
            NullCount: 0,
            NumericStats: new NumericStatistics("1", "3", "2"),
            TypeDistribution: null,
            FirstElements: new[] { new ElementPreview(0, "1", "System.Int32") },
            LastElements: new[] { new ElementPreview(2, "3", "System.Int32") },
            KeyValuePairs: null,
            IsSampled: false);
        analyzerMock.Setup(a => a.AnalyzeAsync("items", 5, null, 0, 5000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        var tool = new CollectionAnalyzeTool(analyzerMock.Object, Mock.Of<ILogger<CollectionAnalyzeTool>>());

        var result = await tool.AnalyzeCollection("items");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var summaryEl = parsed.GetProperty("summary");
        summaryEl.GetProperty("count").GetInt32().Should().Be(3);
        summaryEl.GetProperty("elementType").GetString().Should().Be("System.Int32");
        summaryEl.GetProperty("collectionType").GetString().Should().Be("System.Collections.Generic.List`1");
        summaryEl.GetProperty("kind").GetString().Should().Be("List");
        summaryEl.GetProperty("nullCount").GetInt32().Should().Be(0);
        summaryEl.GetProperty("numericStats").GetProperty("min").GetString().Should().Be("1");
        summaryEl.GetProperty("firstElements")[0].GetProperty("value").GetString().Should().Be("1");
        summaryEl.GetProperty("isSampled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CollectionAnalyze_NotCollection_PreservesLegacyFieldNames()
    {
        var analyzerMock = new Mock<ICollectionAnalyzer>();
        analyzerMock.Setup(a => a.AnalyzeAsync("x", 5, null, 0, 5000, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("'x' is not a recognized collection type"));
        var tool = new CollectionAnalyzeTool(analyzerMock.Object, Mock.Of<ILogger<CollectionAnalyzeTool>>());

        var result = await tool.AnalyzeCollection("x");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("not_collection");
    }

    [Fact]
    public async Task ExceptionGetContext_Success_PreservesLegacyFieldNames()
    {
        var autopsyMock = new Mock<IExceptionAutopsyService>();
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(PausedSession());
        var autopsyResult = new ExceptionAutopsyResult(
            ThreadId: 1,
            Exception: new ExceptionDetail("System.NullReferenceException", "Object reference not set", true, "at Foo()"),
            InnerExceptions: Array.Empty<InnerExceptionEntry>(),
            InnerExceptionsTruncated: false,
            Frames: new[] { new AutopsyFrame(0, "MyApp.Service.Process()", "MyApp.dll", false) },
            TotalFrames: 1,
            ThrowingFrameIndex: 0);
        autopsyMock.Setup(a => a.GetExceptionContextAsync(10, 1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(autopsyResult);
        var tool = new ExceptionGetContextTool(autopsyMock.Object, sessionManagerMock.Object, Mock.Of<ILogger<ExceptionGetContextTool>>());

        var result = await tool.GetExceptionContext();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("threadId").GetInt32().Should().Be(1);
        parsed.GetProperty("exception").GetProperty("type").GetString().Should().Be("System.NullReferenceException");
        parsed.GetProperty("exception").GetProperty("isFirstChance").GetBoolean().Should().BeTrue();
        parsed.GetProperty("frames")[0].GetProperty("function").GetString().Should().Be("MyApp.Service.Process()");
        parsed.GetProperty("totalFrames").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ExceptionGetContext_NoSession_PreservesLegacyFieldNames()
    {
        var autopsyMock = new Mock<IExceptionAutopsyService>();
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);
        var tool = new ExceptionGetContextTool(autopsyMock.Object, sessionManagerMock.Object, Mock.Of<ILogger<ExceptionGetContextTool>>());

        var result = await tool.GetExceptionContext();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    [Fact]
    public async Task BreakpointSet_Success_PreservesLegacyFieldNames()
    {
        var managerMock = new Mock<IBreakpointManager>();
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var breakpoint = new Breakpoint(
            Id: "bp-1",
            Location: new BreakpointLocation(File: "/app/Program.cs", Line: 42, Column: 5),
            State: BreakpointState.Bound,
            Enabled: true,
            Verified: true,
            HitCount: 0);
        managerMock.Setup(m => m.SetBreakpointAsync("/app/Program.cs", 42, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(breakpoint);
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugMcp.Models.DebugSession?)null);
        var tool = new BreakpointSetTool(
            managerMock.Object,
            sessionManagerMock.Object,
            Mock.Of<IProcessDebugger>(),
            Mock.Of<IPdbSymbolReader>(),
            Mock.Of<ILogger<BreakpointSetTool>>());

        var result = await tool.SetBreakpointAsync("/app/Program.cs", 42);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var bpJson = parsed.GetProperty("breakpoint");
        bpJson.GetProperty("id").GetString().Should().Be("bp-1");
        bpJson.GetProperty("location").GetProperty("file").GetString().Should().Be("/app/Program.cs");
        bpJson.GetProperty("location").GetProperty("line").GetInt32().Should().Be(42);
        bpJson.GetProperty("state").GetString().Should().Be("bound");
        bpJson.GetProperty("enabled").GetBoolean().Should().BeTrue();
        bpJson.GetProperty("verified").GetBoolean().Should().BeTrue();
        bpJson.GetProperty("hitCount").GetInt32().Should().Be(0);
        parsed.TryGetProperty("duplicate", out _).Should().BeFalse("duplicate is omitted when false, matching legacy conditional-null behavior");
    }

    [Fact]
    public async Task BreakpointSet_Failure_PreservesLegacyFieldNames()
    {
        var tool = new BreakpointSetTool(
            Mock.Of<IBreakpointManager>(),
            Mock.Of<IDebugSessionManager>(),
            Mock.Of<IProcessDebugger>(),
            Mock.Of<IPdbSymbolReader>(),
            Mock.Of<ILogger<BreakpointSetTool>>());

        var result = await tool.SetBreakpointAsync(file: "", line: 1);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_FILE");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BreakpointEnable_Success_PreservesLegacyFieldNames()
    {
        var managerMock = new Mock<IBreakpointManager>();
        var breakpoint = new Breakpoint(
            Id: "bp-1",
            Location: new BreakpointLocation(File: "/app/Program.cs", Line: 42),
            State: BreakpointState.Disabled,
            Enabled: false,
            Verified: true,
            HitCount: 3);
        managerMock.Setup(m => m.SetBreakpointEnabledAsync("bp-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(breakpoint);
        var tool = new BreakpointEnableTool(managerMock.Object, Mock.Of<ILogger<BreakpointEnableTool>>());

        var result = await tool.EnableBreakpointAsync("bp-1", false);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var bpJson = parsed.GetProperty("breakpoint");
        bpJson.GetProperty("id").GetString().Should().Be("bp-1");
        bpJson.GetProperty("state").GetString().Should().Be("disabled");
        bpJson.GetProperty("enabled").GetBoolean().Should().BeFalse();
        bpJson.GetProperty("hitCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task BreakpointEnable_Failure_PreservesLegacyFieldNames()
    {
        var managerMock = new Mock<IBreakpointManager>();
        managerMock.Setup(m => m.SetBreakpointEnabledAsync("bp-missing", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Breakpoint?)null);
        var tool = new BreakpointEnableTool(managerMock.Object, Mock.Of<ILogger<BreakpointEnableTool>>());

        var result = await tool.EnableBreakpointAsync("bp-missing", true);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("BREAKPOINT_NOT_FOUND");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BreakpointRemove_Success_PreservesLegacyFieldNames()
    {
        var managerMock = new Mock<IBreakpointManager>();
        managerMock.Setup(m => m.RemoveBreakpointAsync("bp-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var tool = new BreakpointRemoveTool(managerMock.Object, Mock.Of<ILogger<BreakpointRemoveTool>>());

        var result = await tool.RemoveBreakpointAsync("bp-1");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("message").GetString().Should().Be("Breakpoint bp-1 removed");
    }

    [Fact]
    public async Task BreakpointRemove_Failure_PreservesLegacyFieldNames()
    {
        var managerMock = new Mock<IBreakpointManager>();
        managerMock.Setup(m => m.RemoveBreakpointAsync("bp-missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var tool = new BreakpointRemoveTool(managerMock.Object, Mock.Of<ILogger<BreakpointRemoveTool>>());

        var result = await tool.RemoveBreakpointAsync("bp-missing");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("BREAKPOINT_NOT_FOUND");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BreakpointSetException_Success_PreservesLegacyFieldNames()
    {
        var managerMock = new Mock<IBreakpointManager>();
        var exceptionBreakpoint = new ExceptionBreakpoint(
            Id: "ebp-1",
            ExceptionType: "System.NullReferenceException",
            BreakOnFirstChance: true,
            BreakOnSecondChance: true,
            IncludeSubtypes: true,
            Enabled: true,
            Verified: false,
            HitCount: 0);
        managerMock.Setup(m => m.SetExceptionBreakpointAsync(
                "System.NullReferenceException", true, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exceptionBreakpoint);
        var tool = new BreakpointSetExceptionTool(managerMock.Object, Mock.Of<ILogger<BreakpointSetExceptionTool>>());

        var result = await tool.SetExceptionBreakpointAsync("System.NullReferenceException");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var bpJson = parsed.GetProperty("breakpoint");
        bpJson.GetProperty("id").GetString().Should().Be("ebp-1");
        bpJson.GetProperty("exceptionType").GetString().Should().Be("System.NullReferenceException");
        bpJson.GetProperty("breakOnFirstChance").GetBoolean().Should().BeTrue();
        bpJson.GetProperty("breakOnSecondChance").GetBoolean().Should().BeTrue();
        bpJson.GetProperty("includeSubtypes").GetBoolean().Should().BeTrue();
        bpJson.GetProperty("verified").GetBoolean().Should().BeFalse();
        bpJson.GetProperty("enabled").GetBoolean().Should().BeTrue();
        bpJson.GetProperty("hitCount").GetInt32().Should().Be(0);
        parsed.GetProperty("note").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BreakpointSetException_Failure_PreservesLegacyFieldNames()
    {
        var tool = new BreakpointSetExceptionTool(Mock.Of<IBreakpointManager>(), Mock.Of<ILogger<BreakpointSetExceptionTool>>());

        var result = await tool.SetExceptionBreakpointAsync(exception_type: "");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_CONDITION");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TracepointSet_Success_PreservesLegacyFieldNames()
    {
        var managerMock = new Mock<IBreakpointManager>();
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var tracepoint = new Breakpoint(
            Id: "tp-1",
            Location: new BreakpointLocation(File: "/app/Program.cs", Line: 10),
            State: BreakpointState.Bound,
            Enabled: true,
            Verified: true,
            HitCount: 0,
            Type: BreakpointType.Tracepoint,
            LogMessage: "Counter is {i}",
            HitCountMultiple: 2,
            MaxNotifications: 5);
        managerMock.Setup(m => m.SetTracepointAsync(
                "/app/Program.cs", 10, null, "Counter is {i}", 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracepoint);
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugMcp.Models.DebugSession?)null);
        var tool = new TracepointSetTool(managerMock.Object, sessionManagerMock.Object, Mock.Of<ILogger<TracepointSetTool>>());

        var result = await tool.SetTracepointAsync("/app/Program.cs", 10, log_message: "Counter is {i}", hit_count_multiple: 2, max_notifications: 5);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var tpJson = parsed.GetProperty("tracepoint");
        tpJson.GetProperty("id").GetString().Should().Be("tp-1");
        tpJson.GetProperty("type").GetString().Should().Be("tracepoint");
        tpJson.GetProperty("location").GetProperty("file").GetString().Should().Be("/app/Program.cs");
        tpJson.GetProperty("state").GetString().Should().Be("bound");
        tpJson.GetProperty("logMessage").GetString().Should().Be("Counter is {i}");
        tpJson.GetProperty("hitCountMultiple").GetInt32().Should().Be(2);
        tpJson.GetProperty("maxNotifications").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task TracepointSet_Failure_PreservesLegacyFieldNames()
    {
        var tool = new TracepointSetTool(
            Mock.Of<IBreakpointManager>(),
            Mock.Of<IDebugSessionManager>(),
            Mock.Of<ILogger<TracepointSetTool>>());

        var result = await tool.SetTracepointAsync(file: "", line: 1);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_FILE");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LayoutGet_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(PausedSession());
        var layout = new TypeLayout
        {
            TypeName = "MyApp.Foo",
            TotalSize = 24,
            HeaderSize = 8,
            DataSize = 16,
            Fields = new[]
            {
                new DebugMcp.Models.Memory.LayoutField
                {
                    Name = "_x", TypeName = "System.Int32", Offset = 0, Size = 4, Alignment = 4, IsReference = false,
                },
            },
            Padding = new[] { new PaddingRegion { Offset = 4, Size = 4, Reason = "alignment for Int64" } },
            IsValueType = false,
            BaseType = "System.Object",
        };
        sessionManagerMock.Setup(s => s.GetTypeLayoutAsync("MyApp.Foo", true, true, null, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(layout);
        var tool = new LayoutGetTool(sessionManagerMock.Object, Mock.Of<ILogger<LayoutGetTool>>());

        var result = await tool.GetLayout("MyApp.Foo");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var layoutEl = parsed.GetProperty("layout");
        layoutEl.GetProperty("typeName").GetString().Should().Be("MyApp.Foo");
        layoutEl.GetProperty("totalSize").GetInt32().Should().Be(24);
        layoutEl.GetProperty("fields")[0].GetProperty("name").GetString().Should().Be("_x");
        layoutEl.GetProperty("fields")[0].GetProperty("alignment").GetInt32().Should().Be(4);
        layoutEl.GetProperty("padding")[0].GetProperty("reason").GetString().Should().Be("alignment for Int64");
        layoutEl.GetProperty("baseType").GetString().Should().Be("System.Object");
    }

    [Fact]
    public async Task LayoutGet_NoSession_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);
        var tool = new LayoutGetTool(sessionManagerMock.Object, Mock.Of<ILogger<LayoutGetTool>>());

        var result = await tool.GetLayout("MyApp.Foo");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    [Fact]
    public async Task MembersGet_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(RunningSession());
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var membersResult = new TypeMembersResult(
            TypeName: "MyApp.Foo",
            Methods: new[]
            {
                new MethodMemberInfo("GetName", "string GetName()", "string", Array.Empty<ParameterInfo>(),
                    Visibility.Public, false, false, false, false, null, "MyApp.Foo"),
            },
            Properties: new[]
            {
                new PropertyMemberInfo("Id", "int", Visibility.Public, false, true, true,
                    Visibility.Public, Visibility.Public, false, null),
            },
            Fields: new[]
            {
                new FieldMemberInfo("_id", "int", Visibility.Private, false, true, false, null),
            },
            Events: Array.Empty<EventMemberInfo>(),
            IncludesInherited: false,
            MethodCount: 1,
            PropertyCount: 1,
            FieldCount: 1,
            EventCount: 0);
        processDebuggerMock.Setup(p => p.GetMembersAsync("MyApp.Foo", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membersResult);
        var tool = new MembersGetTool(sessionManagerMock.Object, processDebuggerMock.Object, Mock.Of<ILogger<MembersGetTool>>());

        var result = await tool.GetMembers("MyApp.Foo");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("typeName").GetString().Should().Be("MyApp.Foo");
        parsed.GetProperty("methods")[0].GetProperty("visibility").GetString().Should().Be("public");
        parsed.GetProperty("properties")[0].GetProperty("visibility").GetString().Should().Be("public");
        parsed.GetProperty("fields")[0].GetProperty("visibility").GetString().Should().Be("private");
        parsed.GetProperty("methodCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task MembersGet_NoSession_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var tool = new MembersGetTool(sessionManagerMock.Object, processDebuggerMock.Object, Mock.Of<ILogger<MembersGetTool>>());

        var result = await tool.GetMembers("MyApp.Foo");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    [Fact]
    public async Task ReferencesGet_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(PausedSession());
        var referencesResult = new ReferencesResult
        {
            TargetAddress = "0x1000",
            TargetType = "MyApp.Foo",
            Outbound = new[]
            {
                new ReferenceInfo
                {
                    SourceAddress = "0x1000", SourceType = "MyApp.Foo",
                    TargetAddress = "0x2000", TargetType = "MyApp.Bar",
                    Path = "_bar", ReferenceType = ReferenceType.Field,
                },
            },
            OutboundCount = 1,
            Truncated = false,
        };
        sessionManagerMock.Setup(s => s.GetOutboundReferencesAsync("this._bar", true, 50, null, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referencesResult);
        var tool = new ReferencesGetTool(sessionManagerMock.Object, Mock.Of<ILogger<ReferencesGetTool>>());

        var result = await tool.GetReferences("this._bar");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var refsEl = parsed.GetProperty("references");
        refsEl.GetProperty("targetAddress").GetString().Should().Be("0x1000");
        refsEl.GetProperty("outbound")[0].GetProperty("referenceType").GetString().Should().Be("Field");
        refsEl.GetProperty("outboundCount").GetInt32().Should().Be(1);
        refsEl.TryGetProperty("inbound", out _).Should().BeFalse("default direction is outbound; legacy omitted inbound keys entirely");
    }

    [Fact]
    public async Task ReferencesGet_NoSession_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);
        var tool = new ReferencesGetTool(sessionManagerMock.Object, Mock.Of<ILogger<ReferencesGetTool>>());

        var result = await tool.GetReferences("this._bar");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

    [Fact]
    public async Task TypesGet_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(RunningSession());
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var typesResult = new TypesResult(
            ModuleName: "MyApp",
            NamespaceFilter: null,
            Types: new[]
            {
                new DebugMcp.Models.Modules.TypeInfo("MyApp.Foo", "Foo", "MyApp", TypeKind.Class, Visibility.Public,
                    false, Array.Empty<string>(), false, null, "MyApp", "System.Object", Array.Empty<string>()),
            },
            Namespaces: new[] { new NamespaceNode("MyApp", "MyApp", 1, Array.Empty<string>(), 0) },
            TotalCount: 1,
            ReturnedCount: 1,
            Truncated: false,
            ContinuationToken: null);
        processDebuggerMock.Setup(p => p.GetTypesAsync("MyApp", null, null, null, 100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typesResult);
        var tool = new TypesGetTool(sessionManagerMock.Object, processDebuggerMock.Object, Mock.Of<ILogger<TypesGetTool>>());

        var result = await tool.GetTypes("MyApp");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("moduleName").GetString().Should().Be("MyApp");
        parsed.GetProperty("types")[0].GetProperty("kind").GetString().Should().Be("class");
        parsed.GetProperty("types")[0].GetProperty("visibility").GetString().Should().Be("public");
        parsed.GetProperty("namespaces")[0].GetProperty("name").GetString().Should().Be("MyApp");
        parsed.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TypesGet_NoSession_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var tool = new TypesGetTool(sessionManagerMock.Object, processDebuggerMock.Object, Mock.Of<ILogger<TypesGetTool>>());

        var result = await tool.GetTypes("MyApp");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
    }

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
