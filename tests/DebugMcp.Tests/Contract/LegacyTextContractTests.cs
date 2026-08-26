using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Progress;
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
}
