namespace DebugMcp.Models.Timeline;

public sealed record TimelineFilter(
    string[]? EventTypes,
    int? ThreadId,
    int? FromEventId,
    int MaxEvents = 200);
