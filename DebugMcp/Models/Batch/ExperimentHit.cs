using DebugMcp.Models.Breakpoints;

namespace DebugMcp.Models.Batch;

/// <summary>A single firing of an experiment's trigger.</summary>
/// <param name="Timestamp">When the hit occurred.</param>
/// <param name="ThreadId">Thread that triggered the hit.</param>
/// <param name="Location">Resolved source location.</param>
/// <param name="Values">Captured variable name → string value pairs.</param>
/// <param name="EvalErrors">Variable name → error message for failed evaluations.</param>
public record ExperimentHit(
    DateTimeOffset Timestamp,
    int ThreadId,
    BreakpointLocation Location,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, string> EvalErrors);
