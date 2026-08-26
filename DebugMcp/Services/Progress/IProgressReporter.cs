namespace DebugMcp.Services.Progress;

/// <summary>
/// First-party wrapper over the SDK-bound <c>IProgress&lt;ProgressNotificationValue&gt;</c>
/// tool-method parameter, so progress reporting can be exercised without an MCP transport
/// (mirrors the <c>IBreakpointNotifier</c> precedent).
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Reports the current stage of a long-running operation.
    /// </summary>
    /// <param name="stage">Human-readable stage name, e.g. "building solution".</param>
    /// <param name="completed">Units of work completed, or null when not countable.</param>
    /// <param name="total">Total units of work, or null when not knowable in advance.</param>
    void ReportStage(string stage, int? completed = null, int? total = null);
}
