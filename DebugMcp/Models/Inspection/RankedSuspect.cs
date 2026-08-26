using DebugMcp.Models;

namespace DebugMcp.Models.Inspection;

/// <summary>
/// One candidate frame in a deterministic suspicion ranking (FR-024). Strictly additive — this
/// never replaces or hides a raw frame, only points at one by <see cref="FrameIndex"/> (FR-025).
/// </summary>
/// <param name="FrameIndex">References a frame already present in the raw result.</param>
/// <param name="Score">Deterministic. Same debuggee state yields the same score, bit for bit (FR-023, SC-008).</param>
/// <param name="Reasons">Non-empty — a rank with no supporting evidence is never emitted.</param>
public sealed record RankedSuspect(
    int FrameIndex,
    double Score,
    IReadOnlyList<SuspicionReason> Reasons);

/// <summary>
/// One heuristic's contribution to a <see cref="RankedSuspect"/>'s score, with the concrete
/// evidence that fired it (FR-027).
/// </summary>
/// <param name="Heuristic">Names the rule that fired. Each rule is documented in docs/enrichment-heuristics.md and independently testable.</param>
/// <param name="Weight">This rule's contribution to the frame's total score. A documented constant, never tuned at runtime.</param>
/// <param name="Evidence">Concrete and checkable, e.g. "local 'order' is null".</param>
/// <param name="Location">File and line where the evidence sits, when symbols allow.</param>
public sealed record SuspicionReason(
    string Heuristic,
    double Weight,
    string Evidence,
    SourceLocation? Location = null);

/// <summary>
/// Explicit signal that ranking could not be computed for this call — missing symbols or
/// unavailable state (FR-026). Never silently omitted: when this is present, the raw data is
/// still present and unchanged, and the call still succeeds.
/// </summary>
/// <param name="Reason">Human-readable explanation, e.g. "no PDB loaded for MyApp.dll".</param>
public sealed record RankingUnavailable(string Reason);

/// <summary>
/// The outcome of a ranking attempt: exactly one of <see cref="Ranking"/> (non-empty,
/// FrameIndex-ascending order after score) or <see cref="Unavailable"/> is set — mirrors the
/// codebase's existing paired-nullable convention (e.g. success/error) rather than a union type.
/// </summary>
public sealed record EnrichmentOutcome(
    IReadOnlyList<RankedSuspect>? Ranking = null,
    RankingUnavailable? Unavailable = null);
