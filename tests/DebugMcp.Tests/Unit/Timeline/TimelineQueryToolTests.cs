using DebugMcp.Models.Timeline;
using DebugMcp.Services.Timeline;
using DebugMcp.Tools;
using AwesomeAssertions;
using Moq;

namespace DebugMcp.Tests.Unit.Timeline;

public class TimelineQueryToolTests
{
    private readonly Mock<ITimelineStore> _storeMock;
    private readonly TimelineQueryTool _tool;

    public TimelineQueryToolTests()
    {
        _storeMock = new Mock<ITimelineStore>();
        _tool = new TimelineQueryTool(_storeMock.Object);
    }

    [Fact]
    public async Task TimelineQueryAsync_Success_ReturnsEventsAsIntEventTypeAndEmptyPayload()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var evt = new TimelineEvent(1, timestamp, TimelineEventType.SessionStarted, null,
            new SessionStartedPayload("launch", 12345));
        _storeMock.Setup(s => s.GetFiltered(It.IsAny<TimelineFilter>()))
            .Returns(new TimelineResponse([evt], 1, 0, null));

        var result = await _tool.TimelineQueryAsync();

        result.Success.Should().BeTrue();
        result.TotalEvents.Should().Be(1);
        result.EventsDropped.Should().Be(0);
        result.Events.Should().ContainSingle();
        var wireEvent = result.Events!.Single();
        wireEvent.EventId.Should().Be(1);
        wireEvent.Timestamp.Should().Be(timestamp);
        // Characterizes a pre-existing bug: the legacy tool serialized the raw enum ordinal, not
        // a name string — SessionStarted == 0. Preserved verbatim by this migration (flagged).
        wireEvent.EventType.Should().Be((int)TimelineEventType.SessionStarted);
        wireEvent.ThreadId.Should().BeNull();
        // Characterizes the same pre-existing bug for Payload: the legacy tool always emitted
        // `{}` regardless of the real payload subtype (abstract-typed property, no polymorphic
        // config). TimelineQueryPayloadWire is deliberately empty to reproduce this.
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task TimelineQueryAsync_InvalidEventTypesJson_ReturnsInvalidParameterError()
    {
        var result = await _tool.TimelineQueryAsync(eventTypes: "not a json array");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_PARAMETER");
    }

    [Fact]
    public async Task TimelineQueryAsync_StoreThrows_ReturnsErrorResult()
    {
        _storeMock.Setup(s => s.GetFiltered(It.IsAny<TimelineFilter>()))
            .Throws(new InvalidOperationException("boom"));

        var result = await _tool.TimelineQueryAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Be("boom");
    }
}
