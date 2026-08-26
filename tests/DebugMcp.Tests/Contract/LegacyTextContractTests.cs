using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Memory;
using DebugMcp.Models.Modules;
using DebugMcp.Services;
using DebugMcp.Services.Inspection;
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
        ProcessName = "MyApp",
        ExecutablePath = "/bin/MyApp",
        RuntimeVersion = ".NET 10.0",
        AttachedAt = DateTimeOffset.UtcNow,
        LaunchMode = LaunchMode.Launch,
        State = SessionState.Paused,
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
}
