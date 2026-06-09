using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Tests.Support;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Notifications;

/// <summary>
/// Verifies that breakpointHit notification includes locals from the top frame (feature 030 US1).
/// </summary>
public class BreakpointNotificationLocalsTests
{
    private readonly BreakpointRegistry _registry;
    private readonly Mock<IPdbSymbolReader> _pdbReaderMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly Mock<IConditionEvaluator> _conditionEvaluatorMock;
    private readonly Mock<IBreakpointNotifier> _notifierMock;
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;

    private BreakpointNotification? _capturedNotification;

    public BreakpointNotificationLocalsTests()
    {
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        _registry = new BreakpointRegistry(registryLogger.Object);

        _pdbReaderMock = new Mock<IPdbSymbolReader>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _conditionEvaluatorMock = new Mock<IConditionEvaluator>();
        _notifierMock = new Mock<IBreakpointNotifier>();
        _sessionManagerMock = new Mock<IDebugSessionManager>();

        _conditionEvaluatorMock
            .Setup(x => x.ValidateCondition(It.IsAny<string?>()))
            .Returns(ConditionValidation.Valid());
        _conditionEvaluatorMock
            .Setup(x => x.Evaluate(It.IsAny<string?>(), It.IsAny<ConditionContext>()))
            .Returns(ConditionResult.Ok(true));

        _notifierMock
            .Setup(x => x.SendBreakpointHitAsync(It.IsAny<BreakpointNotification>()))
            .Callback<BreakpointNotification>(n => _capturedNotification = n)
            .Returns(Task.CompletedTask);
    }

    private BreakpointManager CreateManager()
    {
        var logger = new Mock<ILogger<BreakpointManager>>();
        return new BreakpointManager(
            _registry,
            _pdbReaderMock.Object,
            _processDebuggerMock.Object,
            _conditionEvaluatorMock.Object,
            _notifierMock.Object,
            logger.Object,
            sessionManager: _sessionManagerMock.Object);
    }

    [Fact]
    public async Task BreakpointHit_WhenLocalsAvailable_NotificationContainsLocalsArray()
    {
        // Arrange
        var variables = new List<Variable>
        {
            new("count", "System.Int32", "42", VariableScope.Local, HasChildren: false),
            new("name", "System.String", "\"hello\"", VariableScope.Local, HasChildren: false)
        };
        _sessionManagerMock
            .Setup(m => m.GetVariables(It.IsAny<int?>(), 0, "locals", null))
            .Returns(variables);

        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var manager = CreateManager();
        var bp = await manager.SetBreakpointAsync("/src/Program.cs", 10);

        var hit = new BreakpointHit(bp.Id, ThreadId: 1, DateTimeOffset.UtcNow, bp.Location, HitCount: 1);

        // Act
        manager.OnBreakpointHit(hit);

        // Assert
        _capturedNotification.Should().NotBeNull();
        _capturedNotification!.Locals.Should().NotBeNull("locals should be populated for blocking breakpoints");
        _capturedNotification.Locals!.Should().HaveCount(2);
        _capturedNotification.Locals[0].Name.Should().Be("count");
        _capturedNotification.Locals[0].Value.Should().Be("42");
        _capturedNotification.Locals[1].Name.Should().Be("name");
        _capturedNotification.LocalsError.Should().BeNull();
    }

    [Fact]
    public async Task BreakpointHit_WhenLocalsEvaluationTimesOut_NotificationContainsLocalsError()
    {
        // Arrange — simulate slow GetVariables (200ms > 100ms budget)
        _sessionManagerMock
            .Setup(m => m.GetVariables(It.IsAny<int?>(), 0, "locals", null))
            .Returns(() =>
            {
                Thread.Sleep(200);
                return Array.Empty<Variable>();
            });

        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var manager = CreateManager();
        var bp = await manager.SetBreakpointAsync("/src/Program.cs", 10);

        var hit = new BreakpointHit(bp.Id, ThreadId: 1, DateTimeOffset.UtcNow, bp.Location, HitCount: 1);

        // Act
        manager.OnBreakpointHit(hit);

        // Assert
        _capturedNotification.Should().NotBeNull();
        _capturedNotification!.Locals.Should().BeNull("locals should be null when evaluation timed out");
        _capturedNotification.LocalsError.Should().Be("timeout");
    }

    [Fact]
    public async Task BreakpointHit_WhenLocalsEvaluationFails_NotificationContainsLocalsError()
    {
        // Arrange — simulate GetVariables throwing
        _sessionManagerMock
            .Setup(m => m.GetVariables(It.IsAny<int?>(), 0, "locals", null))
            .Throws<InvalidOperationException>();

        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var manager = CreateManager();
        var bp = await manager.SetBreakpointAsync("/src/Program.cs", 10);

        var hit = new BreakpointHit(bp.Id, ThreadId: 1, DateTimeOffset.UtcNow, bp.Location, HitCount: 1);

        // Act
        manager.OnBreakpointHit(hit);

        // Assert
        _capturedNotification.Should().NotBeNull();
        _capturedNotification!.Locals.Should().BeNull("locals should be null when evaluation failed");
        _capturedNotification.LocalsError.Should().Be("unavailable");
    }

    [Fact]
    public async Task BreakpointHit_ForTracepoint_LocalsNotFetched()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var manager = CreateManager();

        // Set a tracepoint (non-blocking)
        var tp = await manager.SetTracepointAsync("/src/Program.cs", 10, logMessage: "hit {count}");

        var hit = new BreakpointHit(tp.Id, ThreadId: 1, DateTimeOffset.UtcNow, tp.Location, HitCount: 1);

        // Act
        manager.OnBreakpointHit(hit);

        // Assert — GetVariables should NOT be called for tracepoints
        _sessionManagerMock.Verify(
            m => m.GetVariables(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never,
            "locals should not be fetched for tracepoints");
        _capturedNotification?.Locals.Should().BeNull("tracepoints should not include locals");
        _capturedNotification?.LocalsError.Should().BeNull("tracepoints should not have locals error");
    }
}
