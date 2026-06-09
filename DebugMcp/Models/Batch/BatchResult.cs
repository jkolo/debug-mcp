namespace DebugMcp.Models.Batch;

/// <summary>Top-level result returned to the agent after batch completion.</summary>
/// <param name="CompletionReason">Why the batch ended.</param>
/// <param name="TotalExperiments">Count of experiments submitted.</param>
/// <param name="TriggeredCount">Experiments with Status == Triggered.</param>
/// <param name="NotTriggeredCount">Experiments with Status == NotTriggered.</param>
/// <param name="ErrorCount">Experiments with Status == Error.</param>
/// <param name="ExperimentResults">Per-experiment results in submission order.</param>
public record BatchResult(
    BatchCompletionReason CompletionReason,
    int TotalExperiments,
    int TriggeredCount,
    int NotTriggeredCount,
    int ErrorCount,
    IReadOnlyList<ExperimentResult> ExperimentResults);
