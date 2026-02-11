using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services;
using DebugMcp.Services.Snapshots;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotServiceCreateTests
{
    private readonly SnapshotStore _store;
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly SnapshotService _service;

    public SnapshotServiceCreateTests()
    {
        var storeLogger = new Mock<ILogger<SnapshotStore>>();
        _store = new SnapshotStore(storeLogger.Object);

        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        var serviceLogger = new Mock<ILogger<SnapshotService>>();

        _service = new SnapshotService(
            _store,
            _sessionManagerMock.Object,
            _processDebuggerMock.Object,
            serviceLogger.Object);
    }

    private void SetupPausedSession()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns(new DebugSession
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

        _sessionManagerMock.Setup(s => s.GetCurrentState()).Returns(SessionState.Paused);
    }

    private void SetupVariables(List<Variable>? variables = null)
    {
        var vars = variables ?? new List<Variable>
        {
            new("x", "System.Int32", "42", VariableScope.Local, false),
            new("name", "System.String", "\"hello\"", VariableScope.Argument, false),
            new("this", "TestApp.MyClass", "{TestApp.MyClass}", VariableScope.This, true, 3)
        };

        _sessionManagerMock
            .Setup(s => s.GetVariables(It.IsAny<int?>(), It.IsAny<int>(), "all", null))
            .Returns(vars);
    }

    private void SetupStackFrame(string functionName = "TestApp.MyClass.DoWork")
    {
        _sessionManagerMock
            .Setup(s => s.GetStackFrames(It.IsAny<int?>(), 0, 1))
            .Returns((new List<StackFrame>
            {
                new(0, functionName, "TestApp.dll", false)
            }, 1));
    }

    [Fact]
    public void CreateSnapshot_WhenPaused_ReturnsSnapshotWithCorrectMetadata()
    {
        SetupPausedSession();
        SetupVariables();
        SetupStackFrame();

        var snapshot = _service.CreateSnapshot(label: "before-fix");

        snapshot.Id.Should().StartWith("snap-");
        snapshot.Label.Should().Be("before-fix");
        snapshot.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        snapshot.FunctionName.Should().Be("TestApp.MyClass.DoWork");
        snapshot.Variables.Should().HaveCount(3);
    }

    [Fact]
    public void CreateSnapshot_WithoutLabel_AutoGeneratesLabel()
    {
        SetupPausedSession();
        SetupVariables();
        SetupStackFrame();

        var snap1 = _service.CreateSnapshot();
        var snap2 = _service.CreateSnapshot();

        snap1.Label.Should().Be("snapshot-1");
        snap2.Label.Should().Be("snapshot-2");
    }

    [Fact]
    public void CreateSnapshot_StoresInSnapshotStore()
    {
        SetupPausedSession();
        SetupVariables();
        SetupStackFrame();

        var snapshot = _service.CreateSnapshot(label: "test");

        _store.Get(snapshot.Id).Should().NotBeNull();
        _store.Count.Should().Be(1);
    }

    [Fact]
    public void CreateSnapshot_CapturesVariablesWithCorrectPaths()
    {
        SetupPausedSession();
        SetupVariables();
        SetupStackFrame();

        var snapshot = _service.CreateSnapshot();

        snapshot.Variables.Should().Contain(v => v.Name == "x" && v.Path == "x" && v.Value == "42");
        snapshot.Variables.Should().Contain(v => v.Name == "name" && v.Path == "name" && v.Value == "\"hello\"");
        snapshot.Variables.Should().Contain(v => v.Name == "this" && v.Scope == VariableScope.This);
    }

    [Fact]
    public void CreateSnapshot_WhenNotPaused_ThrowsInvalidOperationException()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestApp",
            ExecutablePath = "/usr/bin/testapp",
            RuntimeVersion = ".NET 10.0",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Running,
            LaunchMode = LaunchMode.Launch
        });
        _sessionManagerMock.Setup(s => s.GetCurrentState()).Returns(SessionState.Running);

        var act = () => _service.CreateSnapshot();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*paused*");
    }

    [Fact]
    public void CreateSnapshot_WhenNoSession_ThrowsInvalidOperationException()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);

        var act = () => _service.CreateSnapshot();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*session*");
    }

    [Fact]
    public void CreateSnapshot_PassesThreadIdAndFrameIndex()
    {
        SetupPausedSession();
        SetupVariables();

        _sessionManagerMock
            .Setup(s => s.GetStackFrames(456, 2, 1))
            .Returns((new List<StackFrame>
            {
                new(2, "TestApp.MyClass.OtherMethod", "TestApp.dll", false)
            }, 1));

        _service.CreateSnapshot(threadId: 456, frameIndex: 2);

        _sessionManagerMock.Verify(s => s.GetVariables(456, 2, "all", null), Times.Once);
        _sessionManagerMock.Verify(s => s.GetStackFrames(456, 2, 1), Times.Once);
    }

    [Fact]
    public void CreateSnapshot_UniqueIds()
    {
        SetupPausedSession();
        SetupVariables();
        SetupStackFrame();

        var snap1 = _service.CreateSnapshot();
        var snap2 = _service.CreateSnapshot();

        snap1.Id.Should().NotBe(snap2.Id);
    }

    [Fact]
    public void CreateSnapshot_EmptyFrame_ReturnsSnapshotWithZeroVariables()
    {
        SetupPausedSession();
        SetupVariables(new List<Variable>());
        SetupStackFrame();

        var snapshot = _service.CreateSnapshot();

        snapshot.Variables.Should().BeEmpty();
    }
}
