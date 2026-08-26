using DebugMcp.Services.Progress;
using DebugMcp.Tests.Support;

namespace DebugMcp.Tests.Unit.Progress;

/// <summary>
/// FR-004/SC-001: <see cref="HeartbeatProgress"/> re-emits its stage while wrapped work is
/// pending, and never emits without a reporter.
/// </summary>
public class HeartbeatProgressTests
{
    [Fact]
    public async Task RunAsync_ReportsStageImmediately()
    {
        var progress = new RecordingProgressReporter();

        await HeartbeatProgress.RunAsync(() => Task.FromResult(1), progress, "working");

        progress.Reported.Should().ContainSingle(r => r.Stage == "working");
    }

    [Fact]
    public async Task RunAsync_LongWork_EmitsHeartbeats()
    {
        var progress = new RecordingProgressReporter();

        await HeartbeatProgress.RunAsync(
            async () => { await Task.Delay(TimeSpan.FromMilliseconds(120)); return 1; },
            progress,
            "working",
            interval: TimeSpan.FromMilliseconds(20));

        progress.Reported.Should().HaveCountGreaterThan(2);
        progress.Reported.Should().OnlyContain(r => r.Stage == "working");
    }

    [Fact]
    public async Task RunAsync_NoReporter_CompletesNormally()
    {
        var result = await HeartbeatProgress.RunAsync(() => Task.FromResult(42), progress: null, "working");

        result.Should().Be(42);
    }

    [Fact]
    public async Task RunAsync_StopsHeartbeatingOnceWorkCompletes()
    {
        var progress = new RecordingProgressReporter();

        await HeartbeatProgress.RunAsync(
            () => Task.FromResult(1), progress, "working", interval: TimeSpan.FromMilliseconds(20));

        var countAfterCompletion = progress.Reported.Count;
        await Task.Delay(TimeSpan.FromMilliseconds(80));

        progress.Reported.Count.Should().Be(countAfterCompletion, "the heartbeat loop must stop once work completes");
    }
}
