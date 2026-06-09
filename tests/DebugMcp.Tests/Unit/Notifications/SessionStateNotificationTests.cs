using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Resources;
using DebugMcp.Services.Snapshots;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Notifications;

/// <summary>
/// Tests that McpResourceNotifier sends debugger/sessionStateChanged notifications
/// on session state transitions (feature 030 US3).
/// </summary>
public class SessionStateNotificationTests
{
    private SpySessionNotifier CreateSpy(Mock<IProcessDebugger> processDebuggerMock)
    {
        SetupMockProcessDebuggerEvents(processDebuggerMock);
        return new SpySessionNotifier(
            serviceProvider: Mock.Of<IServiceProvider>(),
            processDebugger: processDebuggerMock.Object,
            sessionManager: Mock.Of<IDebugSessionManager>(),
            breakpointRegistry: new BreakpointRegistry(new Mock<ILogger<BreakpointRegistry>>().Object),
            threadSnapshotCache: new ThreadSnapshotCache(),
            allowedSourcePaths: new AllowedSourcePaths(),
            pdbSymbolCache: new PdbSymbolCache(new Mock<ILogger<PdbSymbolCache>>().Object),
            snapshotStore: Mock.Of<ISnapshotStore>(),
            logger: new Mock<ILogger<McpResourceNotifier>>().Object);
    }

    [Fact]
    public async Task OnStateChanged_RunningToPaused_SendsSessionStateChangedNotification()
    {
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var spy = CreateSpy(processDebuggerMock);
        var location = new SourceLocation("Program.cs", 42, Column: null, FunctionName: "Main", ModuleName: "TestApp");

        processDebuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            OldState = SessionState.Running,
            NewState = SessionState.Paused,
            PauseReason = PauseReason.Breakpoint,
            Location = location,
            ThreadId = 7
        });

        await Task.Delay(50);

        spy.SessionStateNotifications.Should().HaveCount(1);
        var args = spy.SessionStateNotifications[0];
        args.NewState.Should().Be(SessionState.Paused);
        args.OldState.Should().Be(SessionState.Running);
        args.PauseReason.Should().Be(PauseReason.Breakpoint);
        args.Location.Should().NotBeNull();
        args.ThreadId.Should().NotBeNull();
    }

    [Fact]
    public async Task OnStateChanged_PausedToRunning_SendsNotificationWithNullLocation()
    {
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var spy = CreateSpy(processDebuggerMock);

        processDebuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            OldState = SessionState.Paused,
            NewState = SessionState.Running,
            PauseReason = null,
            Location = null,
            ThreadId = null
        });

        await Task.Delay(50);

        spy.SessionStateNotifications.Should().HaveCount(1);
        var args = spy.SessionStateNotifications[0];
        args.NewState.Should().Be(SessionState.Running);
        args.Location.Should().BeNull();
    }

    [Fact]
    public async Task OnStateChanged_AnyToDisconnected_SendsNotification()
    {
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var spy = CreateSpy(processDebuggerMock);

        processDebuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            OldState = SessionState.Running,
            NewState = SessionState.Disconnected
        });

        await Task.Delay(50);

        spy.SessionStateNotifications.Should().HaveCount(1);
        spy.SessionStateNotifications[0].NewState.Should().Be(SessionState.Disconnected);
    }

    [Fact]
    public async Task OnStateChanged_DisconnectedToRunning_SendsNotification()
    {
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var spy = CreateSpy(processDebuggerMock);

        processDebuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            OldState = SessionState.Disconnected,
            NewState = SessionState.Running
        });

        await Task.Delay(50);

        spy.SessionStateNotifications.Should().HaveCount(1);
        var args = spy.SessionStateNotifications[0];
        args.NewState.Should().Be(SessionState.Running);
        args.OldState.Should().Be(SessionState.Disconnected);
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
/// Spy subclass of McpResourceNotifier that captures session state notification calls.
/// </summary>
internal sealed class SpySessionNotifier : McpResourceNotifier
{
    public List<SessionStateChangedEventArgs> SessionStateNotifications { get; } = [];

    public SpySessionNotifier(
        IServiceProvider serviceProvider,
        IProcessDebugger processDebugger,
        IDebugSessionManager sessionManager,
        BreakpointRegistry breakpointRegistry,
        ThreadSnapshotCache threadSnapshotCache,
        AllowedSourcePaths allowedSourcePaths,
        PdbSymbolCache pdbSymbolCache,
        ISnapshotStore snapshotStore,
        ILogger<McpResourceNotifier> logger)
        : base(serviceProvider, processDebugger, sessionManager, breakpointRegistry,
               threadSnapshotCache, allowedSourcePaths, pdbSymbolCache, snapshotStore, logger, debounceMs: 1)
    {
    }

    protected override Task SendSessionStateNotificationAsync(SessionStateChangedEventArgs e)
    {
        SessionStateNotifications.Add(e);
        return Task.CompletedTask;
    }

    protected override void OnResourceUpdated(string uri) { }
    protected override void OnListChanged() { }
}
