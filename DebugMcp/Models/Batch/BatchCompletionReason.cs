namespace DebugMcp.Models.Batch;

public enum BatchCompletionReason
{
    AllTriggered,
    Timeout,
    ProcessExited,
    Cancelled,
    HitLimitReached,
}
