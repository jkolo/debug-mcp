namespace DebugMcp.Models.Timeline;

public sealed record TimelineResponse(
    IReadOnlyList<TimelineEvent> Events,
    int TotalEvents,
    int EventsDropped,
    string? SessionId);
