using System.Diagnostics;
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
        // Deterministic — polls for the real heartbeat event instead of racing a fixed
        // Task.Delay against the interval. A fixed-duration race flaked under GitHub Actions
        // scheduling contention on both macOS (collapsed to exactly 1 tick) and Windows
        // (collapsed to zero ticks); polling for the actual event with a generous ceiling
        // removes the race entirely, matching this repo's established fix for the same class of
        // flake (see SnapshotsResourceTests' TaskCompletionSource-based notification wait).
        var progress = new RecordingProgressReporter();
        var workGate = new TaskCompletionSource();

        var runTask = HeartbeatProgress.RunAsync(
            async () => { await workGate.Task; return 1; },
            progress,
            "working",
            interval: TimeSpan.FromMilliseconds(20));

        var stopwatch = Stopwatch.StartNew();
        while (progress.Reported.Count <= 1 && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(20);
        }
        workGate.SetResult();
        await runTask;

        progress.Reported.Should().HaveCountGreaterThan(1);
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
