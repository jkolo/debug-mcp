namespace DebugMcp.Models.Timeline;

public abstract record TimelineEventPayload;

public sealed record SessionStartedPayload(string SessionType, int Pid) : TimelineEventPayload;

public sealed record BreakpointHitPayload(string BreakpointId, string File, int Line) : TimelineEventPayload;

public sealed record TracepointHitPayload(string TracepointId, string File, int Line) : TimelineEventPayload;

public sealed record ExceptionPayload(string ExceptionType, string Message, bool IsUserUnhandled) : TimelineEventPayload;

public sealed record ModuleLoadedPayload(string ModuleName, string? AssemblyPath, bool HasSymbols) : TimelineEventPayload;

public sealed record ThreadStartedPayload(string? ThreadName) : TimelineEventPayload;

public sealed record ThreadExitedPayload(string? ThreadName) : TimelineEventPayload;

public sealed record OutputPayload(string Content, bool Truncated, string Stream) : TimelineEventPayload;

public sealed record SessionEndedPayload(string Reason) : TimelineEventPayload;
