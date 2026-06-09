using System.Text.Json;
using DebugMcp.Models;
using DebugMcp.Models.Modules;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Resources;
using DebugMcp.Services.Snapshots;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for debugger://modules resource (feature 030 US2).
/// </summary>
public class ModulesResourceTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly DebuggerResourceProvider _provider;

    public ModulesResourceTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();

        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        var registry = new BreakpointRegistry(registryLogger.Object);
        var threadCache = new ThreadSnapshotCache();
        var allowedPaths = new AllowedSourcePaths();
        var providerLogger = new Mock<ILogger<DebuggerResourceProvider>>();

        _provider = new DebuggerResourceProvider(
            _sessionManagerMock.Object,
            registry,
            threadCache,
            allowedPaths,
            providerLogger.Object,
            processDebugger: _processDebuggerMock.Object);
    }

    [Fact]
    public void GetModulesJson_WhenNoSession_ReturnsEmptyList()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns((DebugSession?)null);

        var json = _provider.GetModulesJson();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("modules").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public void GetModulesJson_WhenSessionHasModules_ReturnsModuleList()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestApp",
            ExecutablePath = "/path/TestApp.dll",
            RuntimeVersion = ".NET 10.0",
            AttachedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Attach
        });

        var modules = new List<ModuleInfo>
        {
            new("TestApp", "TestApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                Path: "/path/TestApp.dll",
                Version: "1.0.0.0",
                IsManaged: true,
                IsDynamic: false,
                HasSymbols: true,
                ModuleId: "mod-1",
                BaseAddress: "0x00007FF800000000",
                Size: 65536),
            new("System.Runtime", "System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                Path: "/dotnet/System.Runtime.dll",
                Version: "10.0.0.0",
                IsManaged: true,
                IsDynamic: false,
                HasSymbols: false,
                ModuleId: "mod-2",
                BaseAddress: "0x00007FF900000000",
                Size: 131072)
        };

        _processDebuggerMock
            .Setup(x => x.GetModulesAsync(It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        var json = _provider.GetModulesJson();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("count").GetInt32().Should().Be(2);

        var arr = root.GetProperty("modules");
        arr.GetArrayLength().Should().Be(2);

        var m0 = arr[0];
        m0.GetProperty("name").GetString().Should().Be("TestApp");
        m0.GetProperty("path").GetString().Should().Be("/path/TestApp.dll");
        m0.GetProperty("version").GetString().Should().Be("1.0.0.0");
        m0.GetProperty("hasSymbols").GetBoolean().Should().BeTrue();
        m0.GetProperty("baseAddress").GetString().Should().Be("0x00007FF800000000");
        m0.GetProperty("size").GetInt32().Should().Be(65536);
    }

    [Fact]
    public async Task OnModuleLoaded_CallsNotifyResourceUpdated_WithModulesUri()
    {
        // Arrange — use a spy notifier that captures OnResourceUpdated calls
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(true);
        SetupMockProcessDebuggerEvents(_processDebuggerMock);

        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        var registry = new BreakpointRegistry(registryLogger.Object);
        var pdbCache = new PdbSymbolCache(new Mock<ILogger<PdbSymbolCache>>().Object);
        var loggerMock = new Mock<ILogger<McpResourceNotifier>>();

        var spy = new SpyNotifier(
            serviceProvider: Mock.Of<IServiceProvider>(),
            processDebugger: _processDebuggerMock.Object,
            sessionManager: sessionManagerMock.Object,
            breakpointRegistry: registry,
            threadSnapshotCache: new ThreadSnapshotCache(),
            allowedSourcePaths: new AllowedSourcePaths(),
            pdbSymbolCache: pdbCache,
            snapshotStore: Mock.Of<ISnapshotStore>(),
            logger: loggerMock.Object,
            debounceMs: 1);

        spy.Subscribe("debugger://modules");

        // Act — fire the ModuleLoaded event (non-dynamic, non-in-memory module)
        _processDebuggerMock.Raise(x => x.ModuleLoaded += null, new ModuleLoadedEventArgs
        {
            ModulePath = "/tmp/TestModule.dll",
            BaseAddress = 0x00007FF800000000UL,
            Size = 65536,
            IsDynamic = false,
            IsInMemory = false,
            NativeModule = new object()
        });

        // Wait for debounce (1ms) to fire
        await Task.Delay(100);

        // Assert
        spy.NotifiedUris.Should().Contain("debugger://modules",
            "OnModuleLoaded should call NotifyResourceUpdated(\"debugger://modules\")");
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
/// Spy subclass of McpResourceNotifier that captures resource update calls.
/// </summary>
file sealed class SpyNotifier : McpResourceNotifier
{
    private readonly List<string> _notifiedUris = [];
    public IReadOnlyList<string> NotifiedUris => _notifiedUris;

    public SpyNotifier(
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
