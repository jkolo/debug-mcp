namespace DebugMcp.Models.Timeline;

public sealed record TimelineEvent(
    int EventId,
    DateTimeOffset Timestamp,
    TimelineEventType EventType,
    int? ThreadId,
    TimelineEventPayload Payload);
