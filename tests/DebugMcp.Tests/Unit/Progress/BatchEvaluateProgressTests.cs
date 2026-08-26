using DebugMcp.Models.Batch;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services;
using DebugMcp.Services.Batch;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Tests.Support;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Tests.Unit.Progress;

/// <summary>
/// FR-004: <c>batch_evaluate</c> reports a counted "experiment triggered n of m" update as each
/// experiment triggers — corrected from "evaluating expression n of m" (experiments trigger
/// reactively as breakpoints fire; see <see cref="BatchRunner"/>'s <c>allTriggeredCount</c>).
/// </summary>
public class BatchEvaluateProgressTests
{
    private readonly FakeBreakpointEventSource _eventSource = new();
    private readonly Mock<IBreakpointManager> _bpManagerMock = new();
    private readonly Mock<IDebugSessionManager> _sessionManagerMock = new();
    private readonly RecordingProgressReporter _progress = new();
    private readonly IBatchRunner _sut;

    public BatchEvaluateProgressTests()
    {
        _bpManagerMock.Setup(x => x.GetBreakpointsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bpManagerMock.Setup(x => x.GetExceptionBreakpointsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bpManagerMock.Setup(x => x.RemoveBreakpointAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sessionManagerMock
            .Setup(x => x.GetVariables(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns([]);

        _sut = new BatchRunner(
            _eventSource, _bpManagerMock.Object, _sessionManagerMock.Object, null,
            new Mock<ILogger<BatchRunner>>().Object);
    }

    [Fact]
    public async Task RunAsync_ThreeExperiments_ReportsTriggeredCountAfterEach()
    {
        SetupBreakpoint("src/App.cs", 10, "bp-001");
        SetupBreakpoint("src/App.cs", 20, "bp-002");
        SetupBreakpoint("src/App.cs", 30, "bp-003");

        var request = new BatchRequest(
        [
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 10)),
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 20)),
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 30)),
        ]);

        var runTask = _sut.RunAsync(request, CancellationToken.None, _progress);
        await Task.Yield();

        _eventSource.RaiseBreakpointResolved(MakeHit("bp-001", "src/App.cs", 10));
        _eventSource.RaiseBreakpointResolved(MakeHit("bp-002", "src/App.cs", 20));
        _eventSource.RaiseBreakpointResolved(MakeHit("bp-003", "src/App.cs", 30));

        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        _progress.Reported.Should().HaveCount(3);
        _progress.Reported.Select(r => r.Stage).Should().AllBe("experiment triggered");
        _progress.Reported.Select(r => r.Completed).Should().Equal(1, 2, 3);
        _progress.Reported.Should().OnlyContain(r => r.Total == 3);
    }

    private void SetupBreakpoint(string file, int line, string bpId)
        => _bpManagerMock
            .Setup(x => x.SetBreakpointAsync(file, line, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint(bpId, file, line));

    private static Breakpoint MakeBreakpoint(string id, string file, int line)
        => new(id, new BreakpointLocation(file, line, null),
            BreakpointState.Bound, Enabled: true, Verified: true, HitCount: 0);

    private static ResolvedBreakpointHitEventArgs MakeHit(string bpId, string file, int line, int threadId = 1)
        => new()
        {
            BreakpointId = bpId,
            ThreadId = threadId,
            Location = new BreakpointLocation(file, line, null),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 1,
        };
}
