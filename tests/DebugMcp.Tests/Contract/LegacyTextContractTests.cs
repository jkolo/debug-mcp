using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models;
using DebugMcp.Models.Batch;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Memory;
using DebugMcp.Models.Modules;
using DebugMcp.Models.Timeline;
using DebugMcp.Services;
using DebugMcp.Services.Batch;
using DebugMcp.Services.Progress;
using DebugMcp.Services.Snapshots;
using DebugMcp.Services.Timeline;
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

    // ─── memory_read (T052) ───

    [Fact]
    public async Task MemoryRead_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1,
            ProcessName = "test",
            ExecutablePath = "/bin/test",
            RuntimeVersion = ".NET 10.0",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Launch,
        });
        sessionManagerMock
            .Setup(s => s.ReadMemoryAsync("0x1000", 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryRegion
            {
                Address = "0x1000",
                RequestedSize = 4,
                ActualSize = 4,
                Bytes = "DE AD BE EF",
                Ascii = "....",
                Error = null,
            });
        var tool = new MemoryReadTool(sessionManagerMock.Object, Mock.Of<ILogger<MemoryReadTool>>());

        var result = await tool.ReadMemory("0x1000", 4, "hex_ascii");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var memory = parsed.GetProperty("memory");
        memory.GetProperty("address").GetString().Should().Be("0x1000");
        memory.GetProperty("requestedSize").GetInt32().Should().Be(4);
        memory.GetProperty("actualSize").GetInt32().Should().Be(4);
        memory.GetProperty("bytes").GetString().Should().Be("DE AD BE EF");
        memory.GetProperty("ascii").GetString().Should().Be("....");
        memory.TryGetProperty("error", out _).Should().BeFalse("legacy omitted the nested error key when null");
    }

    [Fact]
    public async Task MemoryRead_NoSession_PreservesLegacyErrorShape()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);
        var tool = new MemoryReadTool(sessionManagerMock.Object, Mock.Of<ILogger<MemoryReadTool>>());

        var result = await tool.ReadMemory("0x1000");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be(ErrorCodes.NoSession);
        parsed.TryGetProperty("memory", out _).Should().BeFalse();
    }

    // ─── modules_search (T052) ───

    [Fact]
    public async Task ModulesSearch_Success_PreservesLegacyFieldNames()
    {
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        sessionManagerMock.Setup(s => s.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1,
            ProcessName = "test",
            ExecutablePath = "/bin/test",
            RuntimeVersion = ".NET 10.0",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Running,
            LaunchMode = LaunchMode.Launch,
        });
        var debuggerMock = new Mock<IProcessDebugger>();
        var typeInfo = new TypeInfo("MyApp.Customer", "Customer", "MyApp", TypeKind.Class,
            Visibility.Public, false, null, false, null, "MyApp", "System.Object", null);
        var searchResult = new SearchResult("*Customer*", SearchType.Types, [typeInfo], [], 1, 1, false, "next-token");
        debuggerMock
            .Setup(d => d.SearchModulesAsync("*Customer*", SearchType.Types, null, false, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResult);
        var tool = new ModulesSearchTool(sessionManagerMock.Object, debuggerMock.Object, Mock.Of<ILogger<ModulesSearchTool>>());

        var result = await tool.SearchModules("*Customer*", search_type: "types");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("query").GetString().Should().Be("*Customer*");
        parsed.GetProperty("searchType").GetString().Should().Be("types");
        var types = parsed.GetProperty("types");
        types[0].GetProperty("fullName").GetString().Should().Be("MyApp.Customer");
        types[0].GetProperty("kind").GetString().Should().Be("class");
        types[0].GetProperty("visibility").GetString().Should().Be("public");
        parsed.GetProperty("totalMatches").GetInt32().Should().Be(1);
        parsed.GetProperty("returnedMatches").GetInt32().Should().Be(1);
        parsed.GetProperty("truncated").GetBoolean().Should().BeFalse();
        parsed.GetProperty("continuationToken").GetString().Should().Be("next-token");
    }

    [Fact]
    public async Task ModulesSearch_EmptyPattern_PreservesLegacyErrorShape()
    {
        var tool = new ModulesSearchTool(
            Mock.Of<IDebugSessionManager>(), Mock.Of<IProcessDebugger>(), Mock.Of<ILogger<ModulesSearchTool>>());

        var result = await tool.SearchModules(" ");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be(ErrorCodes.InvalidPattern);
    }

    // ─── batch_evaluate (T052) ───
    // Legacy field names are snake_case: completion_reason, total_experiments, not_triggered,
    // hit_count, thread_id, eval_errors — the default camelCase policy does not reproduce these,
    // hence the explicit [JsonPropertyName] overrides on BatchEvaluateResult/its nested types.

    [Fact]
    public async Task BatchEvaluate_Success_PreservesLegacySnakeCaseFieldNames()
    {
        var runnerMock = new Mock<IBatchRunner>();
        var hit = new ExperimentHit(
            DateTimeOffset.UtcNow, 7,
            new BreakpointLocation("src/App.cs", 10, null),
            new Dictionary<string, string> { ["myVar"] = "42" }, // mixed-case dictionary key
            new Dictionary<string, string>());
        var experimentResult = new ExperimentResult(0, ExperimentStatus.Triggered, 1, [hit]);
        var batchResult = new BatchResult(BatchCompletionReason.AllTriggered, 1, 1, 0, 0, [experimentResult]);
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<BatchRequest>(), It.IsAny<CancellationToken>(), It.IsAny<IProgressReporter?>()))
            .ReturnsAsync(batchResult);
        var tool = new BatchEvaluateTool(runnerMock.Object, Mock.Of<ILogger<BatchEvaluateTool>>());

        var result = await tool.BatchEvaluateAsync("""[{"trigger":{"file":"src/App.cs","line":10}}]""");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("completion_reason").GetString().Should().Be("all_triggered");
        parsed.GetProperty("total_experiments").GetInt32().Should().Be(1);
        parsed.GetProperty("triggered").GetInt32().Should().Be(1);
        parsed.GetProperty("not_triggered").GetInt32().Should().Be(0);
        parsed.GetProperty("errors").GetInt32().Should().Be(0);

        var experiment = parsed.GetProperty("experiments")[0];
        experiment.GetProperty("index").GetInt32().Should().Be(0);
        experiment.GetProperty("status").GetString().Should().Be("triggered");
        experiment.GetProperty("hit_count").GetInt32().Should().Be(1);

        var wireHit = experiment.GetProperty("hits")[0];
        wireHit.GetProperty("thread_id").GetInt32().Should().Be(7);
        wireHit.GetProperty("location").GetProperty("file").GetString().Should().Be("src/App.cs");
        wireHit.GetProperty("location").GetProperty("line").GetInt32().Should().Be(10);
        // Dictionary keys are untouched by PropertyNamingPolicy — mixed case must pass through.
        wireHit.GetProperty("values").GetProperty("myVar").GetString().Should().Be("42");
        wireHit.TryGetProperty("eval_errors", out _).Should().BeFalse("legacy omitted eval_errors when empty");
    }

    [Fact]
    public async Task BatchEvaluate_Failure_PreservesLegacyLowercaseCodeValue()
    {
        // Legacy failure codes were tool-invented lowercase strings, never drawn from
        // ErrorCodes — preserved verbatim to keep the wire byte-identical (see
        // BatchEvaluateResult's remarks; flagged in the US3 T052 report).
        var runnerMock = new Mock<IBatchRunner>();
        var tool = new BatchEvaluateTool(runnerMock.Object, Mock.Of<ILogger<BatchEvaluateTool>>());

        var result = await tool.BatchEvaluateAsync("not json");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("invalid_json");
    }

    // ─── timeline_query (T052) ───
    // Legacy note: characterized (not fixed) pre-existing bugs — eventType was already a raw int
    // ordinal (no enum converter), and payload was already always {} (abstract-typed property, no
    // polymorphic config) — see TimelineQueryResult's remarks and the US3 T052 report.

    [Fact]
    public async Task TimelineQuery_Success_PreservesLegacyIntEventTypeAndEmptyPayload()
    {
        var storeMock = new Mock<ITimelineStore>();
        var timestamp = DateTimeOffset.UtcNow;
        var evt = new TimelineEvent(1, timestamp, TimelineEventType.BreakpointHit, 3,
            new BreakpointHitPayload("bp-1", "src/App.cs", 10));
        storeMock.Setup(s => s.GetFiltered(It.IsAny<TimelineFilter>()))
            .Returns(new TimelineResponse([evt], 1, 0, "session-1"));
        var tool = new TimelineQueryTool(storeMock.Object);

        var result = await tool.TimelineQueryAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("totalEvents").GetInt32().Should().Be(1);
        parsed.GetProperty("eventsDropped").GetInt32().Should().Be(0);
        parsed.TryGetProperty("sessionId", out _).Should().BeFalse("legacy never emitted sessionId");

        var wireEvent = parsed.GetProperty("events")[0];
        wireEvent.GetProperty("eventId").GetInt32().Should().Be(1);
        wireEvent.GetProperty("eventType").GetInt32().Should().Be((int)TimelineEventType.BreakpointHit);
        wireEvent.GetProperty("threadId").GetInt32().Should().Be(3);
        wireEvent.GetProperty("payload").EnumerateObject().Should().BeEmpty("legacy always emitted an empty payload object");
    }

    [Fact]
    public async Task TimelineQuery_InvalidEventTypes_UsesTypedErrorShape()
    {
        // Legacy wire change (flagged): the pre-migration failure was a bare string
        // ({"success":false,"error":"..."}), not {code,message} — ErrorShapeContractTests forces
        // every migrated tool's Error onto the shared ToolError type, so this tool's failure shape
        // necessarily changes on the wire.
        var storeMock = new Mock<ITimelineStore>();
        var tool = new TimelineQueryTool(storeMock.Object);

        var result = await tool.TimelineQueryAsync(eventTypes: "not a json array");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be(ErrorCodes.InvalidParameter);
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }
}
