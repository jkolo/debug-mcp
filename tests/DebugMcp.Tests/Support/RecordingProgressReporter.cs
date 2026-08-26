using DebugMcp.Services.Progress;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Captures every reported stage in order, for asserting progress sequences without an MCP
/// transport.
/// </summary>
public sealed class RecordingProgressReporter : IProgressReporter
{
    private readonly List<ProgressUpdate> _reported = [];

    public IReadOnlyList<ProgressUpdate> Reported => _reported;

    public void ReportStage(string stage, int? completed = null, int? total = null)
    {
        _reported.Add(new ProgressUpdate(stage, completed, total));
    }
}

/// <summary>One captured call to <see cref="IProgressReporter.ReportStage"/>.</summary>
public sealed record ProgressUpdate(string Stage, int? Completed, int? Total);
