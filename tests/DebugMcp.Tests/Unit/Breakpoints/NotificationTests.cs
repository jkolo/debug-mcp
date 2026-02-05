using DebugMcp.Infrastructure;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;

namespace DebugMcp.Tests.Unit.Breakpoints;

/// <summary>
/// Unit tests for breakpoint notification system (US1).
/// </summary>
public class NotificationTests
{
    private readonly BreakpointRegistry _registry;
    private readonly Mock<IPdbSymbolReader> _pdbReaderMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly Mock<IConditionEvaluator> _conditionEvaluatorMock;
    private readonly Mock<IBreakpointNotifier> _notifierMock;
    private readonly BreakpointManager _manager;

    public NotificationTests()
    {
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        _registry = new BreakpointRegistry(registryLogger.Object);

        _pdbReaderMock = new Mock<IPdbSymbolReader>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _conditionEvaluatorMock = new Mock<IConditionEvaluator>();
        _notifierMock = new Mock<IBreakpointNotifier>();
        var managerLogger = new Mock<ILogger<BreakpointManager>>();

        _conditionEvaluatorMock
            .Setup(x => x.ValidateCondition(It.IsAny<string?>()))
            .Returns(ConditionValidation.Valid());
        _conditionEvaluatorMock
            .Setup(x => x.Evaluate(It.IsAny<string?>(), It.IsAny<ConditionContext>()))
            .Returns(ConditionResult.Ok(true));

        _notifierMock
            .Setup(x => x.SendBreakpointHitAsync(It.IsAny<BreakpointNotification>()))
            .Returns(Task.CompletedTask);

        _manager = new BreakpointManager(
            _registry,
            _pdbReaderMock.Object,
            _processDebuggerMock.Object,
            _conditionEvaluatorMock.Object,
            _notifierMock.Object,
            managerLogger.Object);
    }

    /// <summary>
    /// T005: When a blocking breakpoint is hit, an MCP notification is sent
    /// containing breakpoint ID, location, thread ID, and timestamp.
    /// </summary>
    [Fact]
    public async Task SendNotification_BreakpointHit_SendsMcpNotification()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var breakpoint = await _manager.SetBreakpointAsync("/app/Program.cs", 42);

        var hit = new BreakpointHit(
            BreakpointId: breakpoint.Id,
            ThreadId: 12345,
            Timestamp: DateTimeOffset.UtcNow,
            Location: breakpoint.Location,
            HitCount: 1);

        // Act
        _manager.OnBreakpointHit(hit);

        // Assert
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif =>
                notif.BreakpointId == breakpoint.Id &&
                notif.Type == BreakpointType.Blocking &&
                notif.Location.File == breakpoint.Location.File &&
                notif.Location.Line == breakpoint.Location.Line &&
                notif.ThreadId == 12345 &&
                notif.HitCount == 1)),
            Times.Once);
    }

    /// <summary>
    /// T006: When one of multiple breakpoints is hit, only that breakpoint's
    /// notification is sent (not all breakpoints).
    /// </summary>
    [Fact]
    public async Task SendNotification_MultipleBreakpoints_SendsOnlyHitBreakpoint()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var bp1 = await _manager.SetBreakpointAsync("/app/Program.cs", 10);
        var bp2 = await _manager.SetBreakpointAsync("/app/Program.cs", 20);
        var bp3 = await _manager.SetBreakpointAsync("/app/Service.cs", 5);

        var hit = new BreakpointHit(
            BreakpointId: bp2.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: bp2.Location,
            HitCount: 1);

        // Act
        _manager.OnBreakpointHit(hit);

        // Assert - only bp2 notification sent
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.BreakpointId == bp2.Id)),
            Times.Once);

        // bp1 and bp3 should NOT have notifications
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.BreakpointId == bp1.Id)),
            Times.Never);
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.BreakpointId == bp3.Id)),
            Times.Never);
    }

    /// <summary>
    /// T007: Both the MCP notification is sent AND breakpoint_wait returns
    /// the result (they are not mutually exclusive).
    /// </summary>
    [Fact]
    public async Task SendNotification_WithBreakpointWait_BothWork()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var breakpoint = await _manager.SetBreakpointAsync("/app/Program.cs", 42);

        var hit = new BreakpointHit(
            BreakpointId: breakpoint.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: breakpoint.Location,
            HitCount: 1);

        // Start waiting for breakpoint hit
        var waitTask = _manager.WaitForBreakpointAsync(TimeSpan.FromSeconds(5));

        // Act - simulate hit after short delay
        await Task.Delay(10);
        _manager.OnBreakpointHit(hit);

        var waitResult = await waitTask;

        // Assert - both notification sent AND wait returned result
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.BreakpointId == breakpoint.Id)),
            Times.Once);

        waitResult.Should().NotBeNull();
        waitResult!.BreakpointId.Should().Be(breakpoint.Id);
    }

    /// <summary>
    /// BreakpointNotifier queues notification without throwing - fire-and-forget semantics.
    /// </summary>
    [Fact]
    public async Task BreakpointNotifier_SendsNotification_DoesNotThrow()
    {
        // Arrange - notifier with null server (no MCP connection)
        var logger = new Mock<ILogger<BreakpointNotifier>>();
        using var notifier = new BreakpointNotifier((McpServer?)null, logger.Object);

        var notification = new BreakpointNotification(
            BreakpointId: "bp-test-1",
            Type: BreakpointType.Blocking,
            Location: new NotificationLocation("/app/Test.cs", 10),
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            HitCount: 1);

        // Act & Assert - should not throw even with null server
        var act = () => notifier.SendBreakpointHitAsync(notification);
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// BreakpointNotifier.NotificationMethod constant has correct value.
    /// </summary>
    [Fact]
    public void BreakpointNotifier_NotificationMethod_HasCorrectValue()
    {
        BreakpointNotifier.NotificationMethod.Should().Be("debugger/breakpointHit");
    }

    /// <summary>
    /// Notification includes correct type field for blocking breakpoints.
    /// </summary>
    [Fact]
    public async Task SendNotification_BlockingBreakpoint_TypeIsBlocking()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var breakpoint = await _manager.SetBreakpointAsync("/app/Program.cs", 10);

        var hit = new BreakpointHit(
            BreakpointId: breakpoint.Id,
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: breakpoint.Location,
            HitCount: 1);

        // Act
        _manager.OnBreakpointHit(hit);

        // Assert
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif => notif.Type == BreakpointType.Blocking)),
            Times.Once);
    }
}
