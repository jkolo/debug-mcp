# Data Model: Batch Evaluate & Hypothesis Runner

**Feature**: 031-batch-evaluate | **Date**: 2026-06-09

All types use positional records (project convention). Namespace: `DebugMcp.Models.Batch`.

---

## Enums

```csharp
namespace DebugMcp.Models.Batch;

public enum BatchCompletionReason
{
    AllTriggered,
    Timeout,
    ProcessExited,
    Cancelled,
    HitLimitReached,
}

public enum ExperimentStatus
{
    Triggered,
    NotTriggered,
    Error,
}

public enum ExperimentMode
{
    Blocking,
    NonBlocking,
}

public enum EvalMode
{
    Safe,
    Full,
}
```

---

## Experiment Trigger

```csharp
/// <summary>Discriminated union: source location or exception type.</summary>
public abstract record ExperimentTrigger
{
    public sealed record SourceLocation(string File, int Line) : ExperimentTrigger;
    public sealed record ExceptionType(string TypeName) : ExperimentTrigger;
}
```

---

## Experiment (input)

```csharp
/// <summary>Single observation unit in a batch.</summary>
/// <param name="Trigger">What to intercept.</param>
/// <param name="Mode">Blocking pauses momentarily for collection; NonBlocking collects and continues.</param>
/// <param name="Capture">Variable names or expressions to evaluate at the trigger.</param>
/// <param name="Condition">Optional C# expression; experiment only fires when true.</param>
/// <param name="MaxHits">Stop collecting after N hits (default 1).</param>
public record Experiment(
    ExperimentTrigger Trigger,
    ExperimentMode Mode = ExperimentMode.NonBlocking,
    IReadOnlyList<string>? Capture = null,
    string? Condition = null,
    int MaxHits = 1);
```

---

## ExperimentHit (output)

```csharp
/// <summary>A single firing of an experiment's trigger.</summary>
/// <param name="Timestamp">When the hit occurred.</param>
/// <param name="ThreadId">Thread that triggered the hit.</param>
/// <param name="Location">Resolved source location (may differ from trigger if PDB resolves to exact line).</param>
/// <param name="Values">Captured variable name → string value pairs.</param>
/// <param name="EvalErrors">Variable name → error message for failed evaluations.</param>
public record ExperimentHit(
    DateTimeOffset Timestamp,
    int ThreadId,
    BreakpointLocation Location,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, string> EvalErrors);
```

---

## ExperimentResult (output)

```csharp
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
```

---

## BatchRequest (input)

```csharp
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
```

---

## BatchResult (output)

```csharp
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
```

---

## Service Interface

```csharp
namespace DebugMcp.Services.Batch;

public interface IBatchRunner
{
    /// <summary>
    /// Runs the batch synchronously from the agent's perspective (awaitable).
    /// Returns when all experiments trigger, timeout expires, process exits, or cancellation is requested.
    /// </summary>
    Task<BatchResult> RunAsync(BatchRequest request, CancellationToken cancellationToken = default);

    /// <summary>True if a batch is currently running.</summary>
    bool IsRunning { get; }
}
```

---

## New Event Args on BreakpointManager

```csharp
// Added to BreakpointManager.cs (not to IBreakpointManager interface)

public sealed class ResolvedBreakpointHitEventArgs : EventArgs
{
    public required string BreakpointId { get; init; }
    public required int ThreadId { get; init; }
    public required BreakpointLocation Location { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required int HitCount { get; init; }
    /// <summary>BatchRunner can set to true to override the default pause behaviour for blocking BPs.</summary>
    public bool ShouldContinue { get; set; }
}
```
