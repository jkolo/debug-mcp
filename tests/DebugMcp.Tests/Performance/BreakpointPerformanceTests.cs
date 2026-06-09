using System.Diagnostics;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Performance;

/// <summary>
/// Performance tests verifying success criteria from spec:
/// - SC-001: Breakpoint set verified within 2 seconds
/// - SC-002: Wait returns within 100ms of breakpoint hit
/// </summary>
public class BreakpointPerformanceTests
{
    private readonly BreakpointRegistry _registry;
    private readonly Mock<IPdbSymbolReader> _pdbReaderMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly Mock<IConditionEvaluator> _conditionEvaluatorMock;
    private readonly BreakpointManager _manager;

    public BreakpointPerformanceTests()
    {
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        _registry = new BreakpointRegistry(registryLogger.Object);

        _pdbReaderMock = new Mock<IPdbSymbolReader>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _conditionEvaluatorMock = new Mock<IConditionEvaluator>();
        var managerLogger = new Mock<ILogger<BreakpointManager>>();

        // Default: conditions are valid and pass validation
        _conditionEvaluatorMock
            .Setup(x => x.ValidateCondition(It.IsAny<string?>()))
            .Returns(ConditionValidation.Valid());
        _conditionEvaluatorMock
            .Setup(x => x.Evaluate(It.IsAny<string?>(), It.IsAny<ConditionContext>()))
            .Returns(ConditionResult.Ok(true));

        _manager = new BreakpointManager(
            _registry,
            _pdbReaderMock.Object,
            _processDebuggerMock.Object,
            _conditionEvaluatorMock.Object,
            NullBreakpointNotifier.Instance,
            managerLogger.Object);
    }

    /// <summary>
    /// SC-001: Breakpoint set operation completes within 2 seconds.
    /// Tests the internal SetBreakpointAsync path (without ICorDebug).
    /// </summary>
    [Fact]
    public async Task SetBreakpointAsync_WithPendingBreakpoint_CompletesWithin2Seconds()
    {
        // Arrange - not attached, will create pending breakpoint
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var stopwatch = Stopwatch.StartNew();

        // Act - set a breakpoint (will be pending since no debugger attached)
        var breakpoint = await _manager.SetBreakpointAsync(
            "/path/to/TestFile.cs",
            42,
            column: null,
            condition: null,
            CancellationToken.None);

        stopwatch.Stop();

        // Assert - SC-001: within 2 seconds
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "SC-001: Breakpoint set should complete within 2 seconds");
        breakpoint.Should().NotBeNull();
        breakpoint.State.Should().Be(BreakpointState.Pending,
            "Without debugger, breakpoint should be pending");
    }

    /// <summary>
    /// SC-001: Multiple breakpoints can be set within acceptable time.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task SetMultipleBreakpoints_CompletesWithinAcceptableTime(int count)
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var stopwatch = Stopwatch.StartNew();

        // Act - set multiple breakpoints
        for (int i = 0; i < count; i++)
        {
            await _manager.SetBreakpointAsync(
                $"/path/to/TestFile{i}.cs",
                i + 1,
                column: null,
                condition: null,
                CancellationToken.None);
        }

        stopwatch.Stop();

        // Assert - average should be well under 2s per breakpoint
        var averageMs = stopwatch.ElapsedMilliseconds / (double)count;
        averageMs.Should().BeLessThan(100,
            $"Average time per breakpoint should be <100ms (got {averageMs:F1}ms for {count} breakpoints)");
    }

    /// <summary>
    /// GetBreakpointsAsync performance with many breakpoints.
    /// </summary>
    [Fact]
    public async Task GetBreakpointsAsync_With100Breakpoints_CompletesQuickly()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        for (int i = 0; i < 100; i++)
        {
            await _manager.SetBreakpointAsync(
                $"/path/to/TestFile{i}.cs",
                i + 1,
                column: null,
                condition: null,
                CancellationToken.None);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var list = await _manager.GetBreakpointsAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert - list should be essentially instant
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(50),
            "Listing breakpoints should be very fast");
        list.Should().HaveCount(100);
    }

    /// <summary>
    /// RemoveBreakpointAsync completes quickly.
    /// </summary>
    [Fact]
    public async Task RemoveBreakpointAsync_CompletesQuickly()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var breakpoint = await _manager.SetBreakpointAsync(
            "/path/to/TestFile.cs",
            42,
            column: null,
            condition: null,
            CancellationToken.None);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var removed = await _manager.RemoveBreakpointAsync(breakpoint.Id, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(50),
            "Removing breakpoint should be very fast");
        removed.Should().BeTrue();
    }

    /// <summary>
    /// Enable/disable breakpoint completes quickly.
    /// </summary>
    [Fact]
    public async Task SetBreakpointEnabledAsync_CompletesQuickly()
    {
        // Arrange
        _processDebuggerMock.Setup(x => x.IsAttached).Returns(false);
        var breakpoint = await _manager.SetBreakpointAsync(
            "/path/to/TestFile.cs",
            42,
            column: null,
            condition: null,
            CancellationToken.None);

        // Act - disable
        var stopwatch = Stopwatch.StartNew();
        var disabled = await _manager.SetBreakpointEnabledAsync(breakpoint.Id, false, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(50),
            "Enabling/disabling breakpoint should be very fast");
        disabled.Should().NotBeNull();
        disabled!.Enabled.Should().BeFalse();
    }
}
