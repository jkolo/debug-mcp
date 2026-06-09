using DebugMcp.Models;
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
/// Unit tests for BatchRunner lifecycle: freeze/restore, blocking, timeout, exit, cancellation (T014, T015, T030-T032, T036).
/// </summary>
public class BatchRunnerLifecycleTests
{
    private readonly FakeBreakpointEventSource _eventSource = new();
    private readonly Mock<IBreakpointManager> _bpManagerMock = new();
    private readonly Mock<IDebugSessionManager> _sessionManagerMock = new();
    private readonly IBatchRunner _sut;

    public BatchRunnerLifecycleTests()
    {
        _bpManagerMock
            .Setup(x => x.GetBreakpointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _bpManagerMock
            .Setup(x => x.GetExceptionBreakpointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _bpManagerMock
            .Setup(x => x.SetBreakpointEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, bool _, CancellationToken _) => MakeBreakpoint(id, "file.cs", 1));
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string file, int line, int? col, string? cond, CancellationToken ct) => MakeBreakpoint("bp-default", file, line));
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

    // ─── T014: pre-existing BP freeze/restore ───

    [Fact]
    public async Task RunAsync_DisablesPreExistingBreakpointsOnStart_ReEnablesOnCompletion()
    {
        // Arrange — pre-existing enabled breakpoint
        var existingBp = MakeBreakpoint("bp-existing", "src/Old.cs", 5, enabled: true);
        _bpManagerMock
            .Setup(x => x.GetBreakpointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingBp]);
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync("src/App.cs", 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint("bp-batch", "src/App.cs", 10));

        var request = new BatchRequest(
        [
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 10)),
        ]);

        // Act
        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        // Verify freeze happened before hit
        _bpManagerMock.Verify(
            x => x.SetBreakpointEnabledAsync("bp-existing", false, It.IsAny<CancellationToken>()),
            Times.Once);

        // Trigger batch completion
        _eventSource.RaiseBreakpointResolved(new ResolvedBreakpointHitEventArgs
        {
            BreakpointId = "bp-batch",
            ThreadId = 1,
            Location = new BreakpointLocation("src/App.cs", 10, null),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 1,
        });

        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert restore on completion
        _bpManagerMock.Verify(
            x => x.SetBreakpointEnabledAsync("bp-existing", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── T015: blocking experiment sets ShouldContinue ───

    [Fact]
    public async Task RunAsync_BlockingExperiment_SetsEventArgsShouldContinueTrueAfterCapture()
    {
        // Arrange
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync("src/App.cs", 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint("bp-001", "src/App.cs", 10));

        var request = new BatchRequest(
        [
            new Experiment(
                new ExperimentTrigger.SourceLocation("src/App.cs", 10),
                Mode: ExperimentMode.Blocking,
                Capture: ["counter"]),
        ]);

        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        // Act — raise the event and inspect ShouldContinue after handler runs
        var hitArgs = new ResolvedBreakpointHitEventArgs
        {
            BreakpointId = "bp-001",
            ThreadId = 1,
            Location = new BreakpointLocation("src/App.cs", 10, null),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 1,
            ShouldContinue = false,
        };
        _eventSource.RaiseBreakpointResolved(hitArgs);
        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — blocking experiment overrides ShouldContinue so process auto-resumes
        hitArgs.ShouldContinue.Should().BeTrue(
            "blocking batch experiments must auto-resume the process after variable capture");
    }

    // ─── T030: timeout returns partial results ───

    [Fact]
    public async Task RunAsync_Timeout_ReturnsPartialResultsWithTimeoutReason()
    {
        // Arrange — one reachable, one unreachable experiment
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync("src/App.cs", 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint("bp-001", "src/App.cs", 10));
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync("src/App.cs", 9999, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint("bp-999", "src/App.cs", 9999));

        var request = new BatchRequest(
        [
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 10)),
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 9999)),
        ],
        TimeoutSeconds: 1);

        // Act
        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        // Trigger only the first experiment
        _eventSource.RaiseBreakpointResolved(new ResolvedBreakpointHitEventArgs
        {
            BreakpointId = "bp-001",
            ThreadId = 1,
            Location = new BreakpointLocation("src/App.cs", 10, null),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 1,
        });

        // Wait for timeout
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.CompletionReason.Should().Be(BatchCompletionReason.Timeout);
        result.ExperimentResults[0].Status.Should().Be(ExperimentStatus.Triggered);
        result.ExperimentResults[1].Status.Should().Be(ExperimentStatus.NotTriggered);
    }

    // ─── T031: process exit returns ProcessExited ───

    [Fact]
    public async Task RunAsync_ProcessExits_ReturnsProcessExitedWithCollectedData()
    {
        // Arrange
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync("src/App.cs", 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint("bp-001", "src/App.cs", 10));

        var processDebuggerMock = new Mock<IProcessDebugger>();
        processDebuggerMock.Setup(x => x.CurrentState).Returns(SessionState.Running);

        EventHandler<SessionStateChangedEventArgs>? capturedHandler = null;
        processDebuggerMock
            .SetupAdd(x => x.StateChanged += It.IsAny<EventHandler<SessionStateChangedEventArgs>>())
            .Callback<EventHandler<SessionStateChangedEventArgs>>(h => capturedHandler = h);

        var sutWithProcess = new BatchRunner(
            _eventSource,
            _bpManagerMock.Object,
            _sessionManagerMock.Object,
            null,
            new Mock<ILogger<BatchRunner>>().Object,
            processDebuggerMock.Object);

        var request = new BatchRequest(
        [
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 10)),
        ],
        TimeoutSeconds: 30);

        // Act
        var runTask = sutWithProcess.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        // Simulate process exit
        capturedHandler?.Invoke(this, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Disconnected,
            OldState = SessionState.Running,
        });

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.CompletionReason.Should().Be(BatchCompletionReason.ProcessExited);
        result.ExperimentResults[0].Status.Should().Be(ExperimentStatus.NotTriggered);
    }

    // ─── T032: cancellation returns Cancelled ───

    [Fact]
    public async Task RunAsync_ExternalCancellation_ReturnsCancelledWithCollectedData()
    {
        // Arrange
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync("src/App.cs", 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint("bp-001", "src/App.cs", 10));

        var request = new BatchRequest(
        [
            new Experiment(new ExperimentTrigger.SourceLocation("src/App.cs", 10)),
        ],
        TimeoutSeconds: 60);

        using var cts = new CancellationTokenSource();

        // Act
        var runTask = _sut.RunAsync(request, cts.Token);
        await Task.Yield();

        cts.Cancel();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.CompletionReason.Should().Be(BatchCompletionReason.Cancelled);
        result.ExperimentResults[0].Status.Should().Be(ExperimentStatus.NotTriggered);
    }

    // ─── T036: hit cap returns HitLimitReached ───

    [Fact]
    public async Task RunAsync_HitCapReached_ReturnsHitLimitReachedReason()
    {
        // Arrange
        _bpManagerMock
            .Setup(x => x.SetBreakpointAsync("src/App.cs", 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBreakpoint("bp-001", "src/App.cs", 10));

        var request = new BatchRequest(
        [
            new Experiment(
                new ExperimentTrigger.SourceLocation("src/App.cs", 10),
                MaxHits: 100),
        ],
        MaxTotalHits: 3);

        var runTask = _sut.RunAsync(request, CancellationToken.None);
        await Task.Yield();

        // Fire 3 hits to reach cap
        for (var i = 0; i < 3; i++)
        {
            _eventSource.RaiseBreakpointResolved(new ResolvedBreakpointHitEventArgs
            {
                BreakpointId = "bp-001",
                ThreadId = 1,
                Location = new BreakpointLocation("src/App.cs", 10, null),
                Timestamp = DateTimeOffset.UtcNow,
                HitCount = i + 1,
            });
        }

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.CompletionReason.Should().Be(BatchCompletionReason.HitLimitReached);
    }

    // ─── Helpers ───

    private static Breakpoint MakeBreakpoint(string id, string file, int line, bool enabled = true)
        => new(id, new BreakpointLocation(file, line, null),
            BreakpointState.Bound, Enabled: enabled, Verified: true, HitCount: 0);
}
