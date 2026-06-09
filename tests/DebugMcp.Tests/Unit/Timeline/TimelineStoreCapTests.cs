using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Timeline;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Timeline;
using DebugMcp.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Timeline;

/// <summary>
/// Unit tests for TimelineStore capacity cap and event ordering (T015-T016).
/// </summary>
public class TimelineStoreCapTests
{
    private readonly FakeBreakpointEventSource _eventSource = new();
    private readonly TimelineStore _sut;

    public TimelineStoreCapTests()
    {
        _sut = new TimelineStore(
            _eventSource,
            null,
            null,
            new Mock<ILogger<TimelineStore>>().Object);
    }

    // ─── T015: EventId monotonicity and counters ───

    [Fact]
    public void GetAll_AfterMultipleEvents_EventIdsAreMonotonicallyIncreasingStartingAt1()
    {
        // Arrange — fire 5 breakpoint events through event source (goes through CreateEvent)
        for (var i = 0; i < 5; i++)
            _eventSource.RaiseBreakpointResolved(MakeBpArgs($"bp-{i:D3}", i));

        // Assert
        var response = _sut.GetAll();
        response.Events.Should().HaveCount(5);
        response.TotalEvents.Should().Be(5);
        response.EventsDropped.Should().Be(0);

        var ids = response.Events.Select(e => e.EventId).ToList();
        ids.Should().BeInAscendingOrder();
        ids[0].Should().Be(1);
        ids[4].Should().Be(5);
    }

    // ─── T016: Cap at 10,000 with eviction ───

    [Fact]
    public void Record_Over10000Events_OldestEvictedAndDroppedCountIncremented()
    {
        const int overCap = 10_001;

        // Arrange + Act — fire events through event source
        for (var i = 0; i < overCap; i++)
            _eventSource.RaiseBreakpointResolved(MakeBpArgs($"bp-{i:D5}", i));

        // Assert
        var response = _sut.GetAll();
        response.Events.Should().HaveCount(10_000);
        response.TotalEvents.Should().Be(overCap);
        response.EventsDropped.Should().Be(1);

        // Newest event should be visible
        var maxId = response.Events.Max(e => e.EventId);
        maxId.Should().Be(overCap);

        // Oldest event (EventId==1) should be evicted
        var minId = response.Events.Min(e => e.EventId);
        minId.Should().Be(2);
    }

    private static ResolvedBreakpointHitEventArgs MakeBpArgs(string id, int threadId) =>
        new()
        {
            BreakpointId = id,
            ThreadId = threadId,
            Location = new DebugMcp.Models.Breakpoints.BreakpointLocation("/src/Test.cs", 1),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 1
        };
}
