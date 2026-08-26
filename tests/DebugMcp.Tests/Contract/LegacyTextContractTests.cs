using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
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
