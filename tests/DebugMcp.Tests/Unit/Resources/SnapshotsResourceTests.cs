using System.Text.Json;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Resources;
using DebugMcp.Services.Snapshots;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for debugger://snapshots resource (feature 030 US2).
/// </summary>
public class SnapshotsResourceTests
{
    private readonly Mock<ISnapshotStore> _snapshotStoreMock;
    private readonly DebuggerResourceProvider _provider;

    public SnapshotsResourceTests()
    {
        _snapshotStoreMock = new Mock<ISnapshotStore>();

        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        var registry = new BreakpointRegistry(registryLogger.Object);
        var threadCache = new ThreadSnapshotCache();
        var allowedPaths = new AllowedSourcePaths();
        var providerLogger = new Mock<ILogger<DebuggerResourceProvider>>();
        var sessionManagerMock = new Mock<IDebugSessionManager>();

        _provider = new DebuggerResourceProvider(
            sessionManagerMock.Object,
            registry,
            threadCache,
            allowedPaths,
            providerLogger.Object,
            snapshotStore: _snapshotStoreMock.Object);
    }

    [Fact]
    public void GetSnapshotsJson_WhenEmpty_ReturnsEmptyList()
    {
        _snapshotStoreMock.Setup(x => x.GetAll()).Returns(Array.Empty<Snapshot>());

        var json = _provider.GetSnapshotsJson();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("snapshots").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public void GetSnapshotsJson_WhenStoreHasSnapshots_ReturnsSnapshotList()
    {
        var createdAt = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new List<Snapshot>
        {
            new(
                Id: "snap-abc",
                Label: "My snapshot",
                CreatedAt: createdAt,
                ThreadId: 1,
                FrameIndex: 0,
                FunctionName: "MyApp.MyClass.MyMethod",
                Depth: 1,
                Variables: [])
        };

        _snapshotStoreMock.Setup(x => x.GetAll()).Returns(snapshots);

        var json = _provider.GetSnapshotsJson();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("count").GetInt32().Should().Be(1);

        var arr = root.GetProperty("snapshots");
        arr.GetArrayLength().Should().Be(1);

        var s0 = arr[0];
        s0.GetProperty("id").GetString().Should().Be("snap-abc");
        s0.GetProperty("label").GetString().Should().Be("My snapshot");
        s0.GetProperty("threadId").GetInt32().Should().Be(1);
        s0.GetProperty("frameIndex").GetInt32().Should().Be(0);
        s0.GetProperty("functionName").GetString().Should().Be("MyApp.MyClass.MyMethod");
        s0.GetProperty("variableCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public void SnapshotStore_WhenAdd_FiresChangedEvent()
    {
        // Arrange — use real SnapshotStore
        var logger = new Mock<ILogger<SnapshotStore>>();
        var store = new SnapshotStore(logger.Object);

        SnapshotChangedEventArgs? captured = null;
        store.Changed += (_, e) => captured = e;

        var snapshot = new Snapshot(
            Id: "snap-test",
            Label: "test",
            CreatedAt: DateTimeOffset.UtcNow,
            ThreadId: 1,
            FrameIndex: 0,
            FunctionName: "Test.Method",
            Depth: 0,
            Variables: []);

        // Act
        store.Add(snapshot);

        // Assert
        captured.Should().NotBeNull("Changed event should fire when snapshot is added");
        captured!.Kind.Should().Be(SnapshotChangedKind.Added);
        captured.SnapshotId.Should().Be("snap-test");
    }

    [Fact]
    public async Task McpResourceNotifier_OnSnapshotChanged_CallsNotifyResourceUpdated()
    {
        // Arrange
        var processDebuggerMock = new Mock<IProcessDebugger>();
        SetupMockProcessDebuggerEvents(processDebuggerMock);

        var snapshotStoreMock = new Mock<ISnapshotStore>();

        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        var registry = new BreakpointRegistry(registryLogger.Object);
        var pdbCache = new PdbSymbolCache(new Mock<ILogger<PdbSymbolCache>>().Object);
        var loggerMock = new Mock<ILogger<McpResourceNotifier>>();

        var spy = new SpyNotifierForSnapshots(
            serviceProvider: Mock.Of<IServiceProvider>(),
            processDebugger: processDebuggerMock.Object,
            sessionManager: Mock.Of<IDebugSessionManager>(),
            breakpointRegistry: registry,
            threadSnapshotCache: new ThreadSnapshotCache(),
            allowedSourcePaths: new AllowedSourcePaths(),
            pdbSymbolCache: pdbCache,
            snapshotStore: snapshotStoreMock.Object,
            logger: loggerMock.Object,
            debounceMs: 1);

        spy.Subscribe("debugger://snapshots");

        // Act — fire the Changed event on ISnapshotStore
        snapshotStoreMock.Raise(x => x.Changed += null, new SnapshotChangedEventArgs
        {
            Kind = SnapshotChangedKind.Added,
            SnapshotId = "snap-new"
        });

        // Wait for debounce (1ms) to fire
        await Task.Delay(100);

        // Assert
        spy.NotifiedUris.Should().Contain("debugger://snapshots",
            "McpResourceNotifier should call NotifyResourceUpdated(\"debugger://snapshots\") when snapshot changes");
    }

    private static void SetupMockProcessDebuggerEvents(Mock<IProcessDebugger> mock)
    {
        mock.SetupAdd(x => x.ModuleLoaded += It.IsAny<EventHandler<ModuleLoadedEventArgs>>());
        mock.SetupAdd(x => x.ModuleUnloaded += It.IsAny<EventHandler<ModuleUnloadedEventArgs>>());
        mock.SetupAdd(x => x.StateChanged += It.IsAny<EventHandler<SessionStateChangedEventArgs>>());
        mock.SetupAdd(x => x.StepCompleted += It.IsAny<EventHandler<StepCompleteEventArgs>>());
    }
}

/// <summary>
/// Spy subclass of McpResourceNotifier for snapshot tests.
/// </summary>
file sealed class SpyNotifierForSnapshots : McpResourceNotifier
{
    private readonly List<string> _notifiedUris = [];
    public IReadOnlyList<string> NotifiedUris => _notifiedUris;

    public SpyNotifierForSnapshots(
        IServiceProvider serviceProvider,
        IProcessDebugger processDebugger,
        IDebugSessionManager sessionManager,
        BreakpointRegistry breakpointRegistry,
        ThreadSnapshotCache threadSnapshotCache,
        AllowedSourcePaths allowedSourcePaths,
        PdbSymbolCache pdbSymbolCache,
        ISnapshotStore snapshotStore,
        ILogger<McpResourceNotifier> logger,
        int debounceMs = 1)
        : base(serviceProvider, processDebugger, sessionManager, breakpointRegistry,
               threadSnapshotCache, allowedSourcePaths, pdbSymbolCache, snapshotStore, logger, debounceMs)
    {
    }

    protected override void OnResourceUpdated(string uri)
    {
        _notifiedUris.Add(uri);
    }

    protected override void OnListChanged() { }
}
