using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Timeouts;

/// <summary>
/// FR-034 (defers to FR-003): a timeout is bounded waiting, not forced termination — it does not
/// abandon an indivisible runtime step mid-flight, and never leaves the session unusable. This
/// reuses the same linked-CTS mechanism already proven consistent for caller-cancellation in
/// <c>SessionConsistencyAfterCancelTests</c> — a timeout is just another source cancelling the
/// same linked token.
/// </summary>
public sealed class TimeoutConsistencyTests
{
    [Fact]
    public async Task DebugPause_AfterTimeoutExpiry_SessionRemainsUsableForNextCall()
    {
        var callCount = 0;
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns(RunningSession());
        sessionManager
            .Setup(m => m.PauseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    throw new InvalidOperationException("unreachable");
                }

                return new List<ThreadInfo> { new(1, null, DebugMcp.Models.Inspection.ThreadState.Running, IsCurrent: true) };
            });
        var tool = new DebugPauseTool(sessionManager.Object, Mock.Of<ILogger<DebugPauseTool>>());

        var timedOut = await tool.PauseAsync(timeout: 20);
        timedOut.Success.Should().BeFalse();
        timedOut.Error!.Code.Should().Be(ErrorCodes.Timeout);

        var next = await tool.PauseAsync(timeout: 30000);
        next.Success.Should().BeTrue(because: "a timeout must never leave the session unusable for the next call");
    }

    private static DebugSession RunningSession() => new()
    {
        ProcessId = 1234,
        ProcessName = "TestApp",
        ExecutablePath = "/path/to/TestApp.dll",
        RuntimeVersion = ".NET 10.0",
        AttachedAt = DateTimeOffset.UtcNow,
        State = SessionState.Running,
        LaunchMode = LaunchMode.Launch,
    };
}
