using ModelContextProtocol;

namespace DebugMcp.Services.Progress;

/// <summary>
/// Wraps the SDK-bound <c>IProgress&lt;ProgressNotificationValue&gt;</c> tool-method parameter
/// into <see cref="IProgressReporter"/>. Deliberately **not** DI-registered: whether a client
/// supplied a progress token is per-request information a container cannot see, and the SDK
/// already discards reports when there is none — degradation is structural, not conditional
/// (research.md R2). A tool constructs this adapter itself, once, from its own SDK parameter.
/// </summary>
public sealed class ProgressReporterAdapter(IProgress<ProgressNotificationValue>? sdkProgress) : IProgressReporter
{
    /// <summary>
    /// Returns an <see cref="IProgressReporter"/> for <paramref name="sdkProgress"/>, or null
    /// when the SDK supplied none — callers pass that straight through to service methods that
    /// already treat a null reporter as "nothing to report to" (FR-005).
    /// </summary>
    public static IProgressReporter? Create(IProgress<ProgressNotificationValue>? sdkProgress) =>
        sdkProgress is null ? null : new ProgressReporterAdapter(sdkProgress);

    public void ReportStage(string stage, int? completed = null, int? total = null)
    {
        sdkProgress!.Report(new ProgressNotificationValue
        {
            Progress = completed ?? 0,
            Total = total,
            Message = stage
        });
    }
}
