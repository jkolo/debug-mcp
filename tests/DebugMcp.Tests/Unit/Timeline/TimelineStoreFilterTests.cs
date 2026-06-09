using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Timeline;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Timeline;
using DebugMcp.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Timeline;

/// <summary>
/// Unit tests for TimelineStore.GetFiltered filtering logic (T026-T029).
/// These tests must FAIL (RED) before GetFiltered is implemented.
/// </summary>
public class TimelineStoreFilterTests
{
    private readonly FakeBreakpointEventSource _bpSource = new();
    private readonly FakeOutputEventSource _outputSource = new();
    private readonly Mock<IProcessDebugger> _debuggerMock = new();
    private readonly TimelineStore _sut;

    public TimelineStoreFilterTests()
    {
        _sut = new TimelineStore(
            _bpSource,
            _debuggerMock.Object,
            _outputSource,
            new Mock<ILogger<TimelineStore>>().Object);
    }

    // ─── T026: EventType filter ───

    [Fact]
    public void GetFiltered_EventTypeBreakpointHit_ReturnsOnlyBreakpointHitEvents()
    {
        // Arrange — mix of breakpoint hits and stdout events
        _bpSource.RaiseBreakpointResolved(MakeBpArgs("bp-1", 10));
        _outputSource.RaiseOutput("stdout text", "stdout");
        _bpSource.RaiseBreakpointResolved(MakeBpArgs("bp-2", 10));

        var filter = new TimelineFilter(
            EventTypes: ["breakpoint_hit"],
            ThreadId: null,
            FromEventId: null,
            MaxEvents: 200);

        // Act
        var response = _sut.GetFiltered(filter);

        // Assert
        response.Events.Should().HaveCount(2);
        response.Events.Should().AllSatisfy(e => e.EventType.Should().Be(TimelineEventType.BreakpointHit));
    }

    // ─── T027: ThreadId filter ───

    [Fact]
    public void GetFiltered_ThreadId42_ReturnsOnlyEventsForThread42()
    {
        // Arrange — bp events on thread 42 and thread 99
        _bpSource.RaiseBreakpointResolved(MakeBpArgs("bp-t42", 42));
        _bpSource.RaiseBreakpointResolved(MakeBpArgs("bp-t99", 99));
        _outputSource.RaiseOutput("stdout", "stdout"); // thread-less

        var filter = new TimelineFilter(
            EventTypes: null,
            ThreadId: 42,
            FromEventId: null,
            MaxEvents: 200);

        // Act
        var response = _sut.GetFiltered(filter);

        // Assert
        response.Events.Should().HaveCount(1);
        response.Events.Single().ThreadId.Should().Be(42);
    }

    // ─── T028: FromEventId cursor + MaxEvents cap ───

    [Fact]
    public void GetFiltered_FromEventId5_ReturnsEventsWithIdAtLeast5()
    {
        // Arrange — 8 events
        for (var i = 0; i < 8; i++)
            _bpSource.RaiseBreakpointResolved(MakeBpArgs($"bp-{i}", 1));

        var filter = new TimelineFilter(
            EventTypes: null,
            ThreadId: null,
            FromEventId: 5,
            MaxEvents: 200);

        // Act
        var response = _sut.GetFiltered(filter);

        // Assert
        response.Events.Should().HaveCount(4); // events 5,6,7,8
        response.Events.Should().AllSatisfy(e => e.EventId.Should().BeGreaterThanOrEqualTo(5));
    }

    [Fact]
    public void GetFiltered_MaxEvents3_CappsResultAt3()
    {
        // Arrange — 8 events
        for (var i = 0; i < 8; i++)
            _bpSource.RaiseBreakpointResolved(MakeBpArgs($"bp-{i}", 1));

        var filter = new TimelineFilter(
            EventTypes: null,
            ThreadId: null,
            FromEventId: null,
            MaxEvents: 3);

        // Act
        var response = _sut.GetFiltered(filter);

        // Assert
        response.Events.Should().HaveCount(3);
    }

    // ─── T029: Null filter same as GetAll ───

    [Fact]
    public void GetFiltered_NullFilters_ReturnsSameEventsAsGetAll()
    {
        // Arrange — a few mixed events
        _bpSource.RaiseBreakpointResolved(MakeBpArgs("bp-1", 1));
        _outputSource.RaiseOutput("out", "stdout");

        var filter = new TimelineFilter(
            EventTypes: null,
            ThreadId: null,
            FromEventId: null,
            MaxEvents: 200);

        // Act
        var filtered = _sut.GetFiltered(filter);
        var all = _sut.GetAll();

        // Assert
        filtered.Events.Select(e => e.EventId)
            .Should().Equal(all.Events.Select(e => e.EventId));
    }

    private static ResolvedBreakpointHitEventArgs MakeBpArgs(string id, int threadId) =>
        new()
        {
            BreakpointId = id,
            ThreadId = threadId,
            Location = new BreakpointLocation("/src/Test.cs", 1),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 1
        };
}
