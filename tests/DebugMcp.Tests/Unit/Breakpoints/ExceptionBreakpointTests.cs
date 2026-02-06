using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Breakpoints;

/// <summary>
/// Tests for exception breakpoint pausing behavior (BUG-2 fix).
/// </summary>
public class ExceptionBreakpointTests
{
    private readonly BreakpointRegistry _registry;
    private readonly Mock<IPdbSymbolReader> _pdbReaderMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly Mock<IConditionEvaluator> _conditionEvaluatorMock;
    private readonly Mock<IBreakpointNotifier> _notifierMock;
    private readonly BreakpointManager _manager;

    public ExceptionBreakpointTests()
    {
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        _registry = new BreakpointRegistry(registryLogger.Object);

        _pdbReaderMock = new Mock<IPdbSymbolReader>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _conditionEvaluatorMock = new Mock<IConditionEvaluator>();
        _notifierMock = new Mock<IBreakpointNotifier>();
        var managerLogger = new Mock<ILogger<BreakpointManager>>();

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

    [Fact]
    public async Task OnExceptionHit_WhenMatchingExceptionBreakpointExists_SetsShouldContinueFalse()
    {
        // Arrange — register an exception breakpoint for InvalidOperationException
        await _manager.SetExceptionBreakpointAsync(
            "System.InvalidOperationException",
            breakOnFirstChance: true);

        var args = new ExceptionHitEventArgs
        {
            ThreadId = 1,
            Location = new SourceLocation("/src/Program.cs", 42),
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "Something went wrong",
            IsFirstChance = true,
            IsUnhandled = false
        };

        // ShouldContinue defaults to true
        args.ShouldContinue.Should().BeTrue();

        // Act — simulate the ExceptionHit event
        _processDebuggerMock.Raise(
            p => p.ExceptionHit += null, _processDebuggerMock.Object, args);

        // Assert — BreakpointManager should have set ShouldContinue to false
        args.ShouldContinue.Should().BeFalse();
    }

    [Fact]
    public async Task OnExceptionHit_WhenNoMatchingBreakpoint_ShouldContinueRemainsTrue()
    {
        // Arrange — register an exception breakpoint for a DIFFERENT exception type
        await _manager.SetExceptionBreakpointAsync(
            "System.ArgumentException",
            breakOnFirstChance: true);

        var args = new ExceptionHitEventArgs
        {
            ThreadId = 1,
            Location = new SourceLocation("/src/Program.cs", 42),
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "Something went wrong",
            IsFirstChance = true,
            IsUnhandled = false
        };

        // Act
        _processDebuggerMock.Raise(
            p => p.ExceptionHit += null, _processDebuggerMock.Object, args);

        // Assert — no match, ShouldContinue remains true
        args.ShouldContinue.Should().BeTrue();
    }

    [Fact]
    public async Task OnExceptionHit_WhenMatchingExceptionBreakpoint_IncrementsHitCount()
    {
        // Arrange
        var exBp = await _manager.SetExceptionBreakpointAsync(
            "System.InvalidOperationException",
            breakOnFirstChance: true);

        var args = new ExceptionHitEventArgs
        {
            ThreadId = 1,
            Location = new SourceLocation("/src/Program.cs", 42),
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "test",
            IsFirstChance = true,
            IsUnhandled = false
        };

        // Act
        _processDebuggerMock.Raise(
            p => p.ExceptionHit += null, _processDebuggerMock.Object, args);

        // Assert
        var updated = _registry.GetException(exBp.Id);
        updated.Should().NotBeNull();
        updated!.HitCount.Should().Be(1);
    }

    [Fact]
    public async Task OnExceptionHit_WhenDisabledBreakpoint_ShouldContinueRemainsTrue()
    {
        // Arrange — register and then disable
        var exBp = await _manager.SetExceptionBreakpointAsync(
            "System.InvalidOperationException",
            breakOnFirstChance: true);

        // Disable the exception breakpoint
        var disabled = exBp with { Enabled = false };
        _registry.UpdateException(disabled);

        var args = new ExceptionHitEventArgs
        {
            ThreadId = 1,
            Location = new SourceLocation("/src/Program.cs", 42),
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "test",
            IsFirstChance = true,
            IsUnhandled = false
        };

        // Act
        _processDebuggerMock.Raise(
            p => p.ExceptionHit += null, _processDebuggerMock.Object, args);

        // Assert — disabled breakpoint should not pause
        args.ShouldContinue.Should().BeTrue();
    }

    [Fact]
    public async Task OnExceptionHit_SendsNotification()
    {
        // Arrange
        await _manager.SetExceptionBreakpointAsync(
            "System.InvalidOperationException",
            breakOnFirstChance: true);

        var args = new ExceptionHitEventArgs
        {
            ThreadId = 1,
            Location = new SourceLocation("/src/Program.cs", 42),
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "test message",
            IsFirstChance = true,
            IsUnhandled = false
        };

        // Act
        _processDebuggerMock.Raise(
            p => p.ExceptionHit += null, _processDebuggerMock.Object, args);

        // Assert
        _notifierMock.Verify(n => n.SendBreakpointHitAsync(
            It.Is<BreakpointNotification>(notif =>
                notif.ExceptionInfo != null &&
                notif.ExceptionInfo.Type == "System.InvalidOperationException" &&
                notif.ExceptionInfo.Message == "test message")),
            Times.Once);
    }

    // --- BUG-4: GetExceptionBreakpointsAsync ---

    [Fact]
    public async Task GetExceptionBreakpointsAsync_ReturnsRegisteredExceptionBreakpoints()
    {
        // Arrange
        await _manager.SetExceptionBreakpointAsync("System.InvalidOperationException");
        await _manager.SetExceptionBreakpointAsync("System.ArgumentNullException");

        // Act
        var exceptionBreakpoints = await _manager.GetExceptionBreakpointsAsync();

        // Assert
        exceptionBreakpoints.Should().HaveCount(2);
        exceptionBreakpoints.Should().Contain(eb => eb.ExceptionType == "System.InvalidOperationException");
        exceptionBreakpoints.Should().Contain(eb => eb.ExceptionType == "System.ArgumentNullException");
    }

    [Fact]
    public async Task GetExceptionBreakpointsAsync_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        var exceptionBreakpoints = await _manager.GetExceptionBreakpointsAsync();

        // Assert
        exceptionBreakpoints.Should().BeEmpty();
    }
}
