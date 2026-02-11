using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services;
using DebugMcp.Services.Snapshots;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotServiceCleanupTests
{
    private readonly SnapshotStore _store;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly SnapshotService _service;

    public SnapshotServiceCleanupTests()
    {
        var storeLogger = new Mock<ILogger<SnapshotStore>>();
        _store = new SnapshotStore(storeLogger.Object);

        var sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        var serviceLogger = new Mock<ILogger<SnapshotService>>();

        _service = new SnapshotService(_store, sessionManagerMock.Object, _processDebuggerMock.Object, serviceLogger.Object);
    }

    [Fact]
    public void OnDisconnect_ClearsAllSnapshots()
    {
        _store.Add(new Snapshot("snap-1", "a", DateTimeOffset.UtcNow, 1, 0, "Main", 0,
            new List<SnapshotVariable> { new("x", "x", "System.Int32", "1", VariableScope.Local) }));
        _store.Add(new Snapshot("snap-2", "b", DateTimeOffset.UtcNow, 1, 0, "Main", 0,
            new List<SnapshotVariable> { new("y", "y", "System.Int32", "2", VariableScope.Local) }));

        _store.Count.Should().Be(2);

        _processDebuggerMock.Raise(p => p.StateChanged += null,
            _processDebuggerMock.Object,
            new SessionStateChangedEventArgs { OldState = SessionState.Paused, NewState = SessionState.Disconnected });

        _store.Count.Should().Be(0);
    }

    [Fact]
    public void OnRunning_DoesNotClearSnapshots()
    {
        _store.Add(new Snapshot("snap-1", "a", DateTimeOffset.UtcNow, 1, 0, "Main", 0,
            new List<SnapshotVariable> { new("x", "x", "System.Int32", "1", VariableScope.Local) }));

        _processDebuggerMock.Raise(p => p.StateChanged += null,
            _processDebuggerMock.Object,
            new SessionStateChangedEventArgs { OldState = SessionState.Paused, NewState = SessionState.Running });

        _store.Count.Should().Be(1);
    }

    [Fact]
    public void SoftLimitWarning_LogsAtThreshold()
    {
        var storeLogger = new Mock<ILogger<SnapshotStore>>();
        var store = new SnapshotStore(storeLogger.Object);
        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var serviceLogger = new Mock<ILogger<SnapshotService>>();

        sessionManagerMock.Setup(s => s.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestApp",
            ExecutablePath = "/usr/bin/testapp",
            RuntimeVersion = ".NET 10.0",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Launch,
            ActiveThreadId = 123
        });

        sessionManagerMock.Setup(s => s.GetVariables(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(new List<Variable>());
        sessionManagerMock.Setup(s => s.GetStackFrames(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((new List<Models.Inspection.StackFrame>(), 0));

        var service = new SnapshotService(store, sessionManagerMock.Object, processDebuggerMock.Object, serviceLogger.Object);

        // Add 99 snapshots directly to reach just below threshold
        for (var i = 0; i < 99; i++)
        {
            store.Add(new Snapshot($"snap-{i}", $"s{i}", DateTimeOffset.UtcNow, 1, 0, "Main", 0,
                new List<SnapshotVariable>()));
        }

        store.Count.Should().Be(99);

        // The 100th snapshot triggers the warning
        service.CreateSnapshot();

        store.Count.Should().Be(100);

        // Verify warning was logged
        serviceLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("100")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
