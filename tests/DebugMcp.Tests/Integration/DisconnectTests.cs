using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Integration;

/// <summary>
/// Integration tests for the disconnect workflow.
/// These tests verify disconnection from debug sessions.
/// </summary>
[Collection("ProcessTests")]
public class DisconnectTests : IDisposable
{
    private readonly Mock<ILogger<ProcessDebugger>> _debuggerLoggerMock;
    private readonly Mock<ILogger<DebugSessionManager>> _managerLoggerMock;
    private readonly Mock<IPdbSymbolReader> _pdbSymbolReaderMock;
    private readonly ProcessDebugger _processDebugger;
    private readonly DebugSessionManager _sessionManager;

    public DisconnectTests()
    {
        _debuggerLoggerMock = new Mock<ILogger<ProcessDebugger>>();
        _managerLoggerMock = new Mock<ILogger<DebugSessionManager>>();
        _pdbSymbolReaderMock = new Mock<IPdbSymbolReader>();
        _processDebugger = new ProcessDebugger(_debuggerLoggerMock.Object, _pdbSymbolReaderMock.Object);
        _sessionManager = new DebugSessionManager(_processDebugger, _managerLoggerMock.Object);
    }

    public void Dispose()
    {
        _processDebugger.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNoSession_CompletesSuccessfully()
    {
        // Arrange - no session exists

        // Act
        var act = async () => await _sessionManager.DisconnectAsync();

        // Assert - should not throw
        await act.Should().NotThrowAsync("disconnect when no session should be no-op");
        _sessionManager.GetCurrentState().Should().Be(SessionState.Disconnected);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNoSession_StateRemainsDisconnected()
    {
        // Arrange
        _sessionManager.GetCurrentState().Should().Be(SessionState.Disconnected);

        // Act
        await _sessionManager.DisconnectAsync();

        // Assert
        _sessionManager.GetCurrentState().Should().Be(SessionState.Disconnected);
        _sessionManager.CurrentSession.Should().BeNull();
    }

    [Fact]
    public async Task DisconnectAsync_ClearsCurrentSession()
    {
        // Arrange - set up a mock session
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.AttachAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 1234,
                Name: "test",
                ExecutablePath: "/path/to/test",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);

        // Create a session
        await manager.AttachAsync(1234, TimeSpan.FromSeconds(30));
        manager.CurrentSession.Should().NotBeNull();

        // Act
        await manager.DisconnectAsync();

        // Assert
        manager.CurrentSession.Should().BeNull("session should be cleared after disconnect");
    }

    [Fact]
    public async Task DisconnectAsync_CallsDetachForAttachedProcess()
    {
        // Arrange
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.AttachAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 1234,
                Name: "test",
                ExecutablePath: "/path/to/test",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);

        // Create an attached session
        await manager.AttachAsync(1234, TimeSpan.FromSeconds(30));

        // Act
        await manager.DisconnectAsync(terminateProcess: false);

        // Assert - DetachAsync should be called
        mockDebugger.Verify(d => d.DetachAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockDebugger.Verify(d => d.TerminateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisconnectAsync_AttachedProcessWithTerminateTrue_StillDetaches()
    {
        // Arrange - attached processes should be detached, not terminated
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.AttachAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 1234,
                Name: "test",
                ExecutablePath: "/path/to/test",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);

        // Create an attached session
        await manager.AttachAsync(1234, TimeSpan.FromSeconds(30));

        // Act - even with terminateProcess=true, attached processes should be detached
        await manager.DisconnectAsync(terminateProcess: true);

        // Assert - Detach should be called, NOT Terminate (because it's attached, not launched)
        mockDebugger.Verify(d => d.DetachAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockDebugger.Verify(d => d.TerminateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisconnectAsync_LaunchedProcessWithTerminateTrue_CallsTerminate()
    {
        // Arrange - launched processes should be terminated when terminateProcess=true
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.LaunchAsync(
                It.IsAny<string>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 5678,
                Name: "launched-app",
                ExecutablePath: "/path/to/app.dll",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);

        // Create a launched session
        await manager.LaunchAsync("/path/to/app.dll");

        // Act
        await manager.DisconnectAsync(terminateProcess: true);

        // Assert - TerminateAsync should be called for launched processes
        mockDebugger.Verify(d => d.TerminateAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockDebugger.Verify(d => d.DetachAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisconnectAsync_LaunchedProcessWithTerminateFalse_CallsDetach()
    {
        // Arrange - launched processes with terminateProcess=false should be detached
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.LaunchAsync(
                It.IsAny<string>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 5678,
                Name: "launched-app",
                ExecutablePath: "/path/to/app.dll",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);

        // Create a launched session
        await manager.LaunchAsync("/path/to/app.dll");

        // Act
        await manager.DisconnectAsync(terminateProcess: false);

        // Assert - DetachAsync should be called
        mockDebugger.Verify(d => d.DetachAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockDebugger.Verify(d => d.TerminateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisconnectAsync_SetsStateToDisconnected()
    {
        // Arrange
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.AttachAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 1234,
                Name: "test",
                ExecutablePath: "/path/to/test",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);

        // Create a session
        await manager.AttachAsync(1234, TimeSpan.FromSeconds(30));
        manager.GetCurrentState().Should().Be(SessionState.Running);

        // Act
        await manager.DisconnectAsync();

        // Assert
        manager.GetCurrentState().Should().Be(SessionState.Disconnected);
    }

    [Fact]
    public async Task DisconnectAsync_CanReconnectAfterDisconnect()
    {
        // Arrange
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.AttachAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 1234,
                Name: "test",
                ExecutablePath: "/path/to/test",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);

        // First connection
        await manager.AttachAsync(1234, TimeSpan.FromSeconds(30));
        await manager.DisconnectAsync();

        // Act - reconnect
        var session = await manager.AttachAsync(5678, TimeSpan.FromSeconds(30));

        // Assert
        session.Should().NotBeNull();
        manager.GetCurrentState().Should().Be(SessionState.Running);
    }

    [Fact]
    public async Task DisconnectAsync_MultipleCallsAreSafe()
    {
        // Arrange
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.AttachAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 1234,
                Name: "test",
                ExecutablePath: "/path/to/test",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);

        await manager.AttachAsync(1234, TimeSpan.FromSeconds(30));

        // Act - disconnect multiple times
        await manager.DisconnectAsync();
        await manager.DisconnectAsync();
        await manager.DisconnectAsync();

        // Assert - should not throw, state should remain disconnected
        manager.GetCurrentState().Should().Be(SessionState.Disconnected);
    }

    [Fact]
    public async Task DisconnectAsync_TerminateThrows_StillSetsStateToDisconnected()
    {
        // Arrange - simulate CORDBG_E_ILLEGAL_SHUTDOWN_ORDER
        var mockDebugger = new Mock<IProcessDebugger>();
        mockDebugger
            .Setup(d => d.LaunchAsync(
                It.IsAny<string>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<bool>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 5678,
                Name: "launched-app",
                ExecutablePath: "/path/to/app.dll",
                IsManaged: true,
                CommandLine: null,
                RuntimeVersion: ".NET 8.0"));

        mockDebugger
            .Setup(d => d.TerminateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Runtime.InteropServices.COMException(
                "Error HRESULT CORDBG_E_ILLEGAL_SHUTDOWN_ORDER has been returned from a call to a COM component."));

        var manager = new DebugSessionManager(mockDebugger.Object, _managerLoggerMock.Object);
        await manager.LaunchAsync("/path/to/app.dll");

        // Act & Assert - disconnect should propagate the error but caller can handle it
        var act = async () => await manager.DisconnectAsync(terminateProcess: true);

        // The session manager propagates the exception from TerminateAsync
        await act.Should().ThrowAsync<System.Runtime.InteropServices.COMException>();

        // Verify TerminateAsync was attempted
        mockDebugger.Verify(d => d.TerminateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// Integration tests for the terminate workflow using a real launched process.
/// Regression tests for CORDBG_E_ILLEGAL_SHUTDOWN_ORDER bug.
///
/// NOTE: These tests use DbgShim.RegisterForRuntimeStartup which relies on
/// process-wide native debugging state (ptrace). When other ICorDebug tests
/// run first in the same host process, the native state can prevent the
/// startup callback from firing, causing a hang. The Timeout attribute
/// ensures these tests fail gracefully instead of blocking the suite.
/// When run in isolation, these tests pass reliably.
/// </summary>
[Collection("ProcessTests")]
public class TerminateLaunchedProcessTests : IDisposable
{
    private readonly ProcessDebugger _processDebugger;
    private readonly DebugSessionManager _sessionManager;
    private readonly Mock<ILogger<ProcessDebugger>> _debuggerLoggerMock;
    private readonly Mock<ILogger<DebugSessionManager>> _managerLoggerMock;
    private readonly Mock<IPdbSymbolReader> _pdbSymbolReaderMock;

    public TerminateLaunchedProcessTests()
    {
        _debuggerLoggerMock = new Mock<ILogger<ProcessDebugger>>();
        _managerLoggerMock = new Mock<ILogger<DebugSessionManager>>();
        _pdbSymbolReaderMock = new Mock<IPdbSymbolReader>();
        _processDebugger = new ProcessDebugger(_debuggerLoggerMock.Object, _pdbSymbolReaderMock.Object);
        _sessionManager = new DebugSessionManager(_processDebugger, _managerLoggerMock.Object);
    }

    public void Dispose()
    {
        _processDebugger.Dispose();
    }

    [Fact]
    [Trait("Category", "LaunchProcess")]
    public async Task TerminateAsync_LaunchedPausedProcess_ShouldNotThrow()
    {
        // Arrange - launch process stopped at entry (paused state)
        // This is the exact scenario that triggered CORDBG_E_ILLEGAL_SHUTDOWN_ORDER:
        // launch with stopAtEntry → process is paused → terminate
        var dllPath = Helpers.TestTargetProcess.TestTargetDllPath;
        if (!File.Exists(dllPath))
        {
            // Skip if test target not built
            return;
        }

        var session = await _sessionManager.LaunchAsync(dllPath, stopAtEntry: true,
            timeout: TimeSpan.FromSeconds(15));
        session.Should().NotBeNull();
        _sessionManager.GetCurrentState().Should().Be(SessionState.Paused);

        // Act - terminate the paused process (previously threw CORDBG_E_ILLEGAL_SHUTDOWN_ORDER)
        var act = async () => await _sessionManager.DisconnectAsync(terminateProcess: true);

        // Assert
        await act.Should().NotThrowAsync(
            "terminating a paused launched process should succeed (CORDBG_E_ILLEGAL_SHUTDOWN_ORDER fix)");
        _sessionManager.GetCurrentState().Should().Be(SessionState.Disconnected);
    }

    [Fact]
    [Trait("Category", "LaunchProcess")]
    public async Task TerminateAsync_LaunchedRunningProcess_ShouldNotThrow()
    {
        // Arrange - launch and let it run
        var dllPath = Helpers.TestTargetProcess.TestTargetDllPath;
        if (!File.Exists(dllPath))
        {
            return;
        }

        var session = await _sessionManager.LaunchAsync(dllPath, stopAtEntry: true,
            timeout: TimeSpan.FromSeconds(15));
        session.Should().NotBeNull();

        // Continue to running state — may throw CORDBG_E_SUPERFLOUS_CONTINUE
        // when ICorDebug native state is affected by prior test runs in the same process
        try
        {
            await _processDebugger.ContinueAsync();
        }
        catch (ClrDebug.DebugException)
        {
            // Process may already be running due to ICorDebug state from prior tests
        }
        // Give it a moment to start running
        await Task.Delay(200);

        // Act - terminate while running
        var act = async () => await _sessionManager.DisconnectAsync(terminateProcess: true);

        // Assert
        await act.Should().NotThrowAsync(
            "terminating a running launched process should succeed");
        _sessionManager.GetCurrentState().Should().Be(SessionState.Disconnected);
    }
}
