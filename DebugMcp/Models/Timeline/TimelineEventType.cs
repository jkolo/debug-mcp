namespace DebugMcp.Models.Timeline;

public enum TimelineEventType
{
    SessionStarted,
    BreakpointHit,
    TracepointHit,
    ExceptionFirstChance,
    ExceptionUserUnhandled,
    ModuleLoaded,
    ThreadStarted,
    ThreadExited,
    StdoutWritten,
    StderrWritten,
    SessionEnded
}
