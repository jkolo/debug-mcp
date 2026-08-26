using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Cancellation;

/// <summary>
/// FR-003: cancellation is honoured at the earliest point that leaves the debug session
/// consistent. A cancelled call must never corrupt <see cref="DebugSessionManager"/>'s state —
/// the next call must still succeed.
/// </summary>
public class SessionConsistencyAfterCancelTests
{
    private readonly Mock<IProcessDebugger> _processDebuggerMock = new();
    private readonly DebugSessionManager _sut;

    public SessionConsistencyAfterCancelTests()
    {
        _sut = new DebugSessionManager(_processDebuggerMock.Object, Mock.Of<ILogger<DebugSessionManager>>());
    }

    private async Task<DebugSession> LaunchPausedSessionAsync()
    {
        _processDebuggerMock
            .Setup(x => x.LaunchAsync(
                "app.dll", null, null, null, true, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 4242, Name: "app", ExecutablePath: "app.dll", IsManaged: true,
                CommandLine: null, RuntimeVersion: ".NET 10.0"));

        return await _sut.LaunchAsync("app.dll");
    }

    [Fact]
    public async Task EvaluateAsync_CancelledDownstream_SessionRemainsQueryable()
    {
        await LaunchPausedSessionAsync();

        _processDebuggerMock
            .Setup(x => x.EvaluateAsync("1+1", null, 0, 5000, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.EvaluateAsync("1+1");
        await act.Should().ThrowAsync<OperationCanceledException>();

        // The cancelled call must not have corrupted session state.
        _sut.CurrentSession.Should().NotBeNull();
        _sut.GetCurrentState().Should().Be(SessionState.Paused);
    }

    [Fact]
    public async Task EvaluateAsync_AfterCancelledCall_NextCallSucceeds()
    {
        await LaunchPausedSessionAsync();

        _processDebuggerMock
            .SetupSequence(x => x.EvaluateAsync("1+1", null, 0, 5000, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException())
            .ReturnsAsync(new EvaluationResult(Success: true, Value: "2", Type: "int"));

        var act = () => _sut.EvaluateAsync("1+1");
        await act.Should().ThrowAsync<OperationCanceledException>();

        var result = await _sut.EvaluateAsync("1+1");

        result.Success.Should().BeTrue();
        result.Value.Should().Be("2");
    }
}
