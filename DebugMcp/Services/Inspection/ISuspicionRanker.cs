using DebugMcp.Models.Inspection;

namespace DebugMcp.Services.Inspection;

/// <summary>
/// Deterministic, model-free ranking of candidate fault frames (FR-022, FR-023). Every heuristic
/// is a documented constant-weight rule over data already captured in <paramref name="frames"/>
/// and <paramref name="exception"/> — no language model, no wall-clock, no random source.
/// </summary>
public interface ISuspicionRanker
{
    /// <summary>
    /// Ranks <paramref name="frames"/> by suspicion. <paramref name="exception"/> is null for
    /// exception-independent callers (e.g. <c>stacktrace_get</c>); when present, exception-aware
    /// heuristics (matching type/message against evidence) also run.
    /// </summary>
    EnrichmentOutcome Rank(IReadOnlyList<AutopsyFrame> frames, ExceptionDetail? exception);
}
