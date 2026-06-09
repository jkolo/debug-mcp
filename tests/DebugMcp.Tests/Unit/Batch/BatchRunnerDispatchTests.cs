using DebugMcp.Models.Batch;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services;
using DebugMcp.Services.Batch;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Batch;

/// <summary>
/// Unit tests for BatchRunner hit dispatch logic (T012, T013, T024, T025, T026).
/// </summary>
public class BatchRunnerDispatchTests
{
    private readonly FakeBreakpointEventSource _eventSource = new();
    private readonly Mock<IBreakpointManager> _bpManagerMock = new();
    private readonly Mock<IDebugSessionManager> _sessionManagerMock = new();
    private readonly IBatchRunner _sut;

    public BatchRunnerDispatchTests()
    {
        _bpManagerMock
            .Setup(x => x.GetBreakpointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _bpManagerMock
            .Setup(x => x.GetExceptionBreakpointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _bpManagerMock
            .Setup(x => x.RemoveBreakpointAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sessionManagerMock
            .Setup(x => x.GetVariables(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns([]);

        _sut = new BatchRunner(
            _eventSource,
            _bpManagerMock.Object,
            _sessionManagerMock.Object,
            null,
            new Mock<ILogger<BatchRunner>>().Object);
    }

    // ─── T012: single hit dispatched to correct experiment by breakpoint ID ───

    [Fact]
    public async Task RunAsync_ThreeExperiments_RegistersBreakpointsAndDispatchesHitsByBpId()
    {
        // Arrange
        SetupBreakpoint("src/App.cs", 10, "bp-001");
        SetupBreakpoint("src/App.cs", 20, "bp-002");
        SetupBreakpoint("src/App.cs", 30, "bp-003");

        var request = new BatchRequest(
        [
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 10)),
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 20)),
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 30)),
        ]);

        // Act
        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        _eventSource.RaiseBreakpointResolved(MakeHit("bp-001", "src/App.cs", 10));
        _eventSource.RaiseBreakpointResolved(MakeHit("bp-002", "src/App.cs", 20));
        _eventSource.RaiseBreakpointResolved(MakeHit("bp-003", "src/App.cs", 30));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.CompletionReason.Should().Be(BatchCompletionReason.AllTriggered);
        result.TriggeredCount.Should().Be(3);
        result.ExperimentResults[0].Status.Should().Be(ExperimentStatus.Triggered);
        result.ExperimentResults[1].Status.Should().Be(ExperimentStatus.Triggered);
        result.ExperimentResults[2].Status.Should().Be(ExperimentStatus.Triggered);
    }

    // ─── T013: same-location experiments share one physical breakpoint ───

    [Fact]
    public async Task RunAsync_TwoExperimentsAtSameLocation_ShareBreakpointAndBothGetHit()
    {
        // Arrange — same location returns the same breakpoint (dedup)
        var sharedBp = MakeBreakpoint("bp-shared", "src/App.cs", 15);
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync("src/App.cs", 15, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedBp);

        var request = new BatchRequest(
        [
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 15), Capture: ["i"]),
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 15), Capture: ["i"]),
        ]);

        // Act
        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        // One physical hit fires both experiments (same breakpoint ID)
        _eventSource.RaiseBreakpointResolved(MakeHit("bp-shared", "src/App.cs", 15));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — both experiments triggered by the single breakpoint hit
        result.CompletionReason.Should().Be(BatchCompletionReason.AllTriggered);
        result.TriggeredCount.Should().Be(2);
        result.ExperimentResults[0].Status.Should().Be(ExperimentStatus.Triggered);
        result.ExperimentResults[1].Status.Should().Be(ExperimentStatus.Triggered);
        result.ExperimentResults[0].HitCount.Should().Be(1);
        result.ExperimentResults[1].HitCount.Should().Be(1);
    }

    // ─── T024: non-blocking experiments use SetTracepointAsync ───

    [Fact]
    public async Task RunAsync_NonBlockingExperiment_RegistersAsTracepoint()
    {
        // Arrange
        _bpManagerMock
            .Setup(x => x.SetTracepointAsync(
                "src/App.cs", 10,
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTracepoint("tp-001", "src/App.cs", 10));

        var request = new BatchRequest(
        [
            new Experiment(
                new ExperimentTrigger.SourceLocation("src/App.cs", 10),
                Mode: ExperimentMode.NonBlocking),
        ]);

        // Act
        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        _eventSource.RaiseBreakpointResolved(MakeHit("tp-001", "src/App.cs", 10));
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — tracepoint was registered
        _bpManagerMock.Verify(
            x => x.SetTracepointAsync(
                "src/App.cs", 10,
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        result.TriggeredCount.Should().Be(1);
    }

    // ─── T025: non-blocking hit does NOT set ShouldContinue ───

    [Fact]
    public async Task RunAsync_NonBlockingExperiment_HitDoesNotSetShouldContinueTrue()
    {
        // Arrange
        _bpManagerMock
            .Setup(x => x.SetTracepointAsync(
                "src/App.cs", 10,
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTracepoint("tp-001", "src/App.cs", 10));

        var request = new BatchRequest(
        [
            new Experiment(
                new ExperimentTrigger.SourceLocation("src/App.cs", 10),
                Mode: ExperimentMode.NonBlocking),
        ]);

        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        // Act — raise event and check ShouldContinue after handler runs
        var hitArgs = MakeHit("tp-001", "src/App.cs", 10);
        hitArgs.ShouldContinue = false;
        _eventSource.RaiseBreakpointResolved(hitArgs);
        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — non-blocking doesn't override ShouldContinue
        hitArgs.ShouldContinue.Should().BeFalse(
            "non-blocking experiments rely on tracepoint auto-continue, not ShouldContinue override");
    }

    // ─── T026: max_hits per experiment ───

    [Fact]
    public async Task RunAsync_ExperimentWithMaxHits_StopsCollectingAfterLimit()
    {
        // Arrange
        SetupBreakpoint("src/App.cs", 10, "bp-001");

        var request = new BatchRequest(
        [
            new Experiment(
                new ExperimentTrigger.SourceLocation("src/App.cs", 10),
                MaxHits: 3),
        ]);

        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        // Act: fire 5 hits — only 3 should be counted
        for (var i = 0; i < 5; i++)
            _eventSource.RaiseBreakpointResolved(MakeHit("bp-001", "src/App.cs", 10));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.CompletionReason.Should().Be(BatchCompletionReason.AllTriggered);
        result.ExperimentResults[0].HitCount.Should().Be(3);
        result.ExperimentResults[0].Hits.Should().HaveCount(3);
    }

    // ─── Helpers ───

    private void SetupBreakpoint(string file, int line, string bpId)
        => _bpManagerMock
            .Setup(x => x.SetBreakpointAsync(file, line, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint(bpId, file, line));

    private static Breakpoint MakeBreakpoint(string id, string file, int line)
        => new(id, new BreakpointLocation(file, line, null),
            BreakpointState.Bound, Enabled: true, Verified: true, HitCount: 0);

    private static Breakpoint MakeTracepoint(string id, string file, int line)
        => new(id, new BreakpointLocation(file, line, null),
            BreakpointState.Bound, Enabled: true, Verified: true, HitCount: 0,
            Type: BreakpointType.Tracepoint);

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
