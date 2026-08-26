using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Timeouts;

/// <summary>FR-033, SC-012: an exhausted timeout budget returns the timeout error code, naming the elapsed budget.</summary>
public sealed class TimeoutExpiryTests
{
    [Fact]
    public async Task DebugPause_UnderlyingCallHangsPastTimeout_ReturnsTimeoutErrorNamingBudget()
    {
        var sessionManager = new Mock<IDebugSessionManager>();
        sessionManager.Setup(m => m.CurrentSession).Returns(RunningSession());
        sessionManager
            .Setup(m => m.PauseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            });
        var tool = new DebugPauseTool(sessionManager.Object, Mock.Of<ILogger<DebugPauseTool>>());

        var result = await tool.PauseAsync(timeout: 20);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.Timeout);
        result.Error.Message.Should().Contain("20", because: "FR-033 requires the message to name the elapsed budget");
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
