using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Breakpoints;

/// <summary>
/// Unit tests for tracepoint functionality (US2, US3, US4, US5).
/// </summary>
public class TracepointTests
{
    private readonly BreakpointRegistry _registry;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly Mock<IBreakpointNotifier> _notifierMock;
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly BreakpointManager _manager;

    public TracepointTests()
    {
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        _registry = new BreakpointRegistry(registryLogger.Object);

        var pdbReaderMock = new Mock<IPdbSymbolReader>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        var conditionEvaluatorMock = new Mock<IConditionEvaluator>();
        _notifierMock = new Mock<IBreakpointNotifier>();
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        var managerLogger = new Mock<ILogger<BreakpointManager>>();

        conditionEvaluatorMock
            .Setup(x => x.ValidateCondition(It.IsAny<string?>()))
            .Returns(ConditionValidation.Valid());
        conditionEvaluatorMock
            .Setup(x => x.Evaluate(It.IsAny<string?>(), It.IsAny<ConditionContext>()))
            .Returns(ConditionResult.Ok(true));

        _notifierMock
            .Setup(x => x.SendBreakpointHitAsync(It.IsAny<BreakpointNotification>()))
            .Returns(Task.CompletedTask);

        var logMessageEvaluator = new LogMessageEvaluator(
            _sessionManagerMock.Object,
            new Mock<ILogger<LogMessageEvaluator>>().Object);

        _manager = new BreakpointManager(
            _registry,
            pdbReaderMock.Object,
            _processDebuggerMock.Object,
            conditionEvaluatorMock.Object,
            _notifierMock.Object,
            managerLogger.Object,
            logMessageEvaluator);
    }

    // ========== US2: Tracepoint creation and behavior ==========

    /// <summary>
    /// T014: SetTracepointAsync returns a tracepoint with unique ID prefixed "tp-".
    /// </summary>
    [Fact]
    public async Task SetTracepoint_ReturnsUniqueId()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);

        // Act
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 42);

        // Assert
        tp.Should().NotBeNull();
        tp.Id.Should().StartWith("tp-");
        tp.Type.Should().Be(BreakpointType.Tracepoint);
        tp.Location.Line.Should().Be(42);
        tp.Enabled.Should().BeTrue();
    }

    /// <summary>
    /// T015: When a tracepoint is hit, it sends notification but returns false
    /// (continues execution, doesn't pause).
    /// </summary>
    [Fact]
    public async Task TracepointHit_SendsNotification_ContinuesExecution()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 42);

        var hit = new BreakpointHit(
            BreakpointId: tp.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: tp.Location,
            HitCount: 1);

        // Act
        var shouldPause = _manager.OnBreakpointHit(hit);

        // Assert
        shouldPause.Should().BeFalse("tracepoint should NOT pause execution");
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif =>
                notif.BreakpointId == tp.Id &&
                notif.Type == BreakpointType.Tracepoint)),
            Times.Once);
    }

    /// <summary>
    /// T016: A tracepoint inside a loop sends multiple notifications (one per iteration).
    /// </summary>
    [Fact]
    public async Task TracepointInLoop_SendsMultipleNotifications()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Loop.cs", 15);

        // Act - simulate 5 hits
        for (int i = 0; i < 5; i++)
        {
            var hit = new BreakpointHit(
                BreakpointId: tp.Id,
                ThreadId: 1,
                Timestamp: DateTimeOffset.UtcNow,
                Location: tp.Location,
                HitCount: i + 1);

            var shouldPause = _manager.OnBreakpointHit(hit);
            shouldPause.Should().BeFalse();
        }

        // Assert - 5 notifications sent
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.BreakpointId == tp.Id)),
            Times.Exactly(5));
    }

    // ========== US3: Log message with expressions ==========

    /// <summary>
    /// T023: Tracepoint with single expression "Value is {myVar}" evaluates correctly.
    /// </summary>
    [Fact]
    public async Task LogMessage_SingleExpression_EvaluatesCorrectly()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 10, logMessage: "Value is {myVar}");

        _sessionManagerMock
            .Setup(s => s.EvaluateAsync("myVar", It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(Success: true, Value: "42"));

        var hit = new BreakpointHit(
            BreakpointId: tp.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: tp.Location,
            HitCount: 1);

        // Act
        _manager.OnBreakpointHit(hit);

        // Assert
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif =>
                notif.LogMessage == "Value is 42")),
            Times.Once);
    }

    /// <summary>
    /// T024: Tracepoint with multiple expressions evaluates all of them.
    /// </summary>
    [Fact]
    public async Task LogMessage_MultipleExpressions_EvaluatesAll()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 10,
            logMessage: "x={x}, y={y}, result={x+y}");

        _sessionManagerMock
            .Setup(s => s.EvaluateAsync("x", It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(Success: true, Value: "10"));
        _sessionManagerMock
            .Setup(s => s.EvaluateAsync("y", It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(Success: true, Value: "20"));
        _sessionManagerMock
            .Setup(s => s.EvaluateAsync("x+y", It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(Success: true, Value: "30"));

        var hit = new BreakpointHit(
            BreakpointId: tp.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: tp.Location,
            HitCount: 1);

        // Act
        _manager.OnBreakpointHit(hit);

        // Assert
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif =>
                notif.LogMessage == "x=10, y=20, result=30")),
            Times.Once);
    }

    /// <summary>
    /// T025: Tracepoint with expression that fails includes error marker.
    /// </summary>
    [Fact]
    public async Task LogMessage_ExpressionError_IncludesErrorMarker()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 10,
            logMessage: "Value is {nullObj.Name}");

        _sessionManagerMock
            .Setup(s => s.EvaluateAsync("nullObj.Name", It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(
                Success: false,
                Error: new EvaluationError(Code: "eval_exception", Message: "Object reference not set", ExceptionType: "NullReferenceException")));

        var hit = new BreakpointHit(
            BreakpointId: tp.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: tp.Location,
            HitCount: 1);

        // Act
        _manager.OnBreakpointHit(hit);

        // Assert
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif =>
                notif.LogMessage != null && notif.LogMessage.Contains("<error: NullReferenceException>"))),
            Times.Once);
    }

    // ========== US4: Tracepoint lifecycle management ==========

    /// <summary>
    /// T033: breakpoint_list includes tracepoints with type="tracepoint".
    /// </summary>
    [Fact]
    public async Task BreakpointList_IncludesTracepointsWithType()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var bp = await _manager.SetBreakpointAsync("/app/Program.cs", 10);
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 20);

        // Act
        var all = await _manager.GetBreakpointsAsync();

        // Assert
        all.Should().HaveCount(2);
        all.Should().Contain(b => b.Id == bp.Id && b.Type == BreakpointType.Blocking);
        all.Should().Contain(b => b.Id == tp.Id && b.Type == BreakpointType.Tracepoint);
    }

    /// <summary>
    /// T034: Disabling a tracepoint stops notifications.
    /// </summary>
    [Fact]
    public async Task DisableTracepoint_StopsNotifications()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 10);

        // Disable tracepoint
        await _manager.SetBreakpointEnabledAsync(tp.Id, false);

        var hit = new BreakpointHit(
            BreakpointId: tp.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: tp.Location,
            HitCount: 1);

        // Act
        _manager.OnBreakpointHit(hit);

        // Assert - no notification sent for disabled tracepoint
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.IsAny<BreakpointNotification>()),
            Times.Never);
    }

    /// <summary>
    /// T035: Re-enabling a tracepoint resumes notifications.
    /// </summary>
    [Fact]
    public async Task EnableTracepoint_ResumesNotifications()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 10);

        // Disable then re-enable
        await _manager.SetBreakpointEnabledAsync(tp.Id, false);
        await _manager.SetBreakpointEnabledAsync(tp.Id, true);

        var hit = new BreakpointHit(
            BreakpointId: tp.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: tp.Location,
            HitCount: 1);

        // Act
        _manager.OnBreakpointHit(hit);

        // Assert - notification sent after re-enabling
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.BreakpointId == tp.Id)),
            Times.Once);
    }

    /// <summary>
    /// T036: Removing a tracepoint deletes it and stops notifications.
    /// </summary>
    [Fact]
    public async Task RemoveTracepoint_DeletesAndStopsNotifications()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Program.cs", 10);

        // Remove tracepoint
        var removed = await _manager.RemoveBreakpointAsync(tp.Id);
        removed.Should().BeTrue();

        // Assert - not in list
        var all = await _manager.GetBreakpointsAsync();
        all.Should().BeEmpty();
    }

    // ========== US5: Notification frequency filtering ==========

    /// <summary>
    /// T041: hit_count_multiple=100 notifies every 100th hit.
    /// </summary>
    [Fact]
    public async Task HitCountMultiple_NotifiesEveryNthHit()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Loop.cs", 10,
            hitCountMultiple: 3);

        // Act - simulate 9 hits
        for (int i = 0; i < 9; i++)
        {
            var hit = new BreakpointHit(
                BreakpointId: tp.Id,
                ThreadId: 1,
                Timestamp: DateTimeOffset.UtcNow,
                Location: tp.Location,
                HitCount: i + 1);

            _manager.OnBreakpointHit(hit);
        }

        // Assert - only hits 3, 6, 9 should trigger notification (3 total)
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.BreakpointId == tp.Id)),
            Times.Exactly(3));
    }

    /// <summary>
    /// T042: max_notifications=3 auto-disables tracepoint after 3 notifications.
    /// </summary>
    [Fact]
    public async Task MaxNotifications_AutoDisablesAfterLimit()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var tp = await _manager.SetTracepointAsync("/app/Loop.cs", 10,
            maxNotifications: 3);

        // Act - simulate 10 hits
        for (int i = 0; i < 10; i++)
        {
            var hit = new BreakpointHit(
                BreakpointId: tp.Id,
                ThreadId: 1,
                Timestamp: DateTimeOffset.UtcNow,
                Location: tp.Location,
                HitCount: i + 1);

            _manager.OnBreakpointHit(hit);
        }

        // Assert - only 3 notifications sent, then auto-disabled
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.BreakpointId == tp.Id)),
            Times.Exactly(3));

        // Verify tracepoint is now disabled
        var result = await _manager.GetBreakpointAsync(tp.Id);
        result.Should().NotBeNull();
        result!.Enabled.Should().BeFalse("tracepoint should be auto-disabled after max notifications");
    }
}
