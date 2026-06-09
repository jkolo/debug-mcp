namespace DebugMcp.Models.Batch;

/// <summary>Single observation unit in a batch.</summary>
/// <param name="Trigger">What to intercept.</param>
/// <param name="Mode">Blocking pauses momentarily for collection; NonBlocking collects and continues.</param>
/// <param name="Capture">Variable names or expressions to evaluate at the trigger.</param>
/// <param name="Condition">Optional C# expression; experiment only fires when true.</param>
/// <param name="MaxHits">Stop collecting after N hits (default 1).</param>
public record Experiment(
    ExperimentTrigger Trigger,
    ExperimentMode Mode = ExperimentMode.Blocking,
    IReadOnlyList<string>? Capture = null,
    string? Condition = null,
    int MaxHits = 1);
