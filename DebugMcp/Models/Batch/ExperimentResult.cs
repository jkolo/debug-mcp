namespace DebugMcp.Models.Batch;

/// <summary>All collected data for one experiment.</summary>
/// <param name="Index">0-based index matching input experiments array.</param>
/// <param name="Status">Final status after batch completion.</param>
/// <param name="HitCount">Total times trigger fired (may be less than MaxHits if batch ended early).</param>
/// <param name="Hits">Ordered list of hit records.</param>
/// <param name="ErrorMessage">Set when Status == Error (e.g., invalid trigger location).</param>
public record ExperimentResult(
    int Index,
    ExperimentStatus Status,
    int HitCount,
    IReadOnlyList<ExperimentHit> Hits,
    string? ErrorMessage = null);
