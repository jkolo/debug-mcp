namespace DebugMcp.Models.Batch;

/// <summary>Full batch submission parameters.</summary>
/// <param name="Experiments">1–20 experiments to run.</param>
/// <param name="TimeoutSeconds">Batch ends and returns partial results after this many seconds (default 30).</param>
/// <param name="EvalMode">Expression evaluation safety mode (default Safe).</param>
/// <param name="MaxTotalHits">Soft cap on combined hit count across all experiments (default 500).</param>
public record BatchRequest(
    IReadOnlyList<Experiment> Experiments,
    int TimeoutSeconds = 30,
    EvalMode EvalMode = EvalMode.Safe,
    int MaxTotalHits = 500);
