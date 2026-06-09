# Implementation Plan: Batch Evaluate & Hypothesis Runner

**Branch**: `031-batch-evaluate` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

## Summary

Introduces `batch_evaluate` — a single MCP tool that accepts up to 20 experiments, registers triggers (breakpoints/tracepoints), runs the program, collects variables at each hit, and returns a structured summary without requiring the agent to orchestrate hit-inspect-continue loops. Builds on `BreakpointManager` and `ISafeExpressionAnalyzer` (feature 029).

---

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**: ClrDebug 0.3.4, ModelContextProtocol 1.3.0, System.Threading.Channels (already in use)  
**Storage**: In-memory only; batch state lives in `BatchRunner` singleton for the duration of a run  
**Testing**: xUnit + FluentAssertions + Moq (existing pattern)  
**Target Platform**: Linux/macOS/Windows x64+arm64 (same as project)  
**Performance Goals**: Summary delivery within 2s after program completion (SC-005); 100ms per-hit variable evaluation budget (inherited from feature 030 pattern)  
**Constraints**: Max 20 experiments per batch; soft total-hit cap default 500; only one active batch at a time  
**Scale/Scope**: Single debug session, single process

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ PASS | Uses ICorDebug exclusively via `BreakpointManager` |
| II. MCP Compliance | ✅ PASS | Tool follows `noun_verb` format (`batch_evaluate`); structured JSON responses; timeout parameter |
| III. Test-First | ✅ PASS | TDD mandatory; unit tests cover BatchRunner dispatch logic and hit-cap enforcement |
| IV. Simplicity | ✅ PASS | No new abstraction layers beyond `BatchRunner`; composes on existing `BreakpointManager` and `IBreakpointNotifier` chain |
| V. Observability | ✅ PASS | Log: batch start/end, each experiment hit, hit-cap trigger, partial-result reasons |

No violations. No complexity justification required.

---

## Project Structure

### Documentation (this feature)

```text
specs/031-batch-evaluate/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code

```text
DebugMcp/
├── Models/Batch/
│   ├── BatchRequest.cs          # Input model: list of Experiment + global params
│   ├── BatchResult.cs           # Top-level result: summary + list of ExperimentResult
│   ├── Experiment.cs            # Single experiment: trigger, capture list, condition, mode, maxHits
│   ├── ExperimentHit.cs         # One firing: timestamp, location, threadId, variable values
│   ├── ExperimentResult.cs      # All hits + final status for one experiment
│   ├── BatchCompletionReason.cs # Enum: AllTriggered, Timeout, ProcessExited, Cancelled, HitLimitReached
│   └── ExperimentStatus.cs      # Enum: Triggered, NotTriggered, Error
├── Services/Batch/
│   ├── IBatchRunner.cs          # Interface: RunAsync, CancelAsync
│   └── BatchRunner.cs           # Implementation: orchestrates experiments, hooks into BreakpointManager
└── Tools/
    └── BatchEvaluateTool.cs     # MCP tool: batch_evaluate

tests/DebugMcp.Tests/
├── Unit/Batch/
│   ├── BatchRunnerDispatchTests.cs      # same-location dispatch, condition filtering, hit cap
│   └── BatchRunnerLifecycleTests.cs     # pre-existing BP freeze/restore, cancellation, timeout
└── Contract/
    └── ToolAnnotationTests.cs   # Extended with batch_evaluate entry (existing file)
```

---

## Phase 0: Research

### Findings

**Decision**: Hook batch experiment dispatch via a new `event EventHandler<ResolvedBreakpointHitEventArgs>? BreakpointResolved` on `BreakpointManager` (concrete class).  
**Rationale**: `BreakpointManager.OnBreakpointHit` already performs location resolution, condition evaluation, and hit-count increment. Firing an event after this processing gives `BatchRunner` pre-digested hit data (resolved location, breakpoint ID, pause/continue hint) without duplicating resolver logic. The event fires synchronously on the ICorDebug callback thread — `BatchRunner` must return quickly and queue work.  
**Alternatives considered**: (a) Decorate `IBreakpointNotifier` — rejected because notifications are post-pause-decision; blocking experiments would already be continuing before the decorator runs. (b) Subscribe directly to `IProcessDebugger.BreakpointHit` — rejected because raw ICorDebug events lack resolved locations and conditions.

**Decision**: Register one physical breakpoint per unique (file, line) location regardless of how many experiments share it; maintain `Dictionary<string, List<int>> _bpToExperiments` in `BatchRunner`.  
**Rationale**: `BreakpointManager.SetBreakpointAsync` already returns the existing breakpoint when a duplicate location is requested. The `BreakpointResolved` event fires once per physical hit; `BatchRunner` fans out to all matching experiments.  
**Alternatives considered**: Separate physical breakpoint per experiment — rejected because ICorDebug fires a callback for each bound function breakpoint and would require deduplicating at a lower level.

**Decision**: Pre-existing breakpoints frozen via `SetBreakpointEnabledAsync(id, false)` on all entries returned by `GetBreakpointsAsync()` + exception breakpoints via `GetExceptionBreakpointsAsync()` before batch starts; restored individually on completion.  
**Rationale**: `SetBreakpointEnabledAsync` already calls `corBp.Activate(false)` on the native breakpoint — no new ICorDebug API needed. Storing `(id, originalEnabled)` pairs in a local list allows correct restoration even if the batch crashes mid-run.  
**Alternatives considered**: Pause-and-ignore approach (A from clarification) — rejected by user.

**Decision**: `eval_mode: safe` validates through existing `ISafeExpressionAnalyzer.Analyze()` before `IDebugSessionManager.EvaluateAsync()`; `eval_mode: full` skips the analyzer.  
**Rationale**: `ISafeExpressionAnalyzer` is already registered as a singleton (feature 029). `BatchRunner` takes it as a constructor parameter and calls `Analyze()` conditionally.

**Decision**: Blocking experiments in a batch auto-resume after variable collection.  
**Rationale**: `BreakpointResolved` event fires on the ICorDebug callback thread. For blocking experiments, `BatchRunner` sets a flag to signal auto-continue; `BreakpointManager.OnDebuggerBreakpointHit` checks this flag when deciding `e.ShouldContinue`. Alternatively, `BatchRunner` calls `IDebugSessionManager.ContinueAsync()` from a background thread after collecting. Given that `BreakpointManager` already handles the continue decision through the `e.ShouldContinue` flag on `BreakpointHitEventArgs`, the cleanest approach is for `BatchRunner` to set `e.ShouldContinue = true` via the event args if the hit is for a batch-registered blocking experiment that should auto-resume.

---

## Phase 1: Design & Contracts

### Key Design: BatchRunner ↔ BreakpointManager Integration

```
ICorDebug thread
  → BreakpointManager.OnDebuggerBreakpointHit
      → resolves location, evaluates condition, increments hit count
      → fires BreakpointResolved event (NEW)
          → BatchRunner.OnBreakpointResolved (sync handler, fast path only)
              → look up experiments by breakpoint ID
              → for each matching experiment: check per-experiment condition, enqueue hit for async collection
              → if all matching experiments are blocking: set e.ShouldContinue = false (batch pause)
              → if all are non-blocking: set e.ShouldContinue = true
      → returns pause/continue decision to ICorDebug

BatchRunner background worker (Channel<T>)
  → dequeue hit
  → collect variables via IDebugSessionManager (100ms budget)
  → append ExperimentHit to ExperimentResult
  → check hit cap; if reached → signal completion with HitLimitReached
  → if blocking experiment: call ContinueAsync after collection
```

### `ResolvedBreakpointHitEventArgs` (added to BreakpointManager.cs)

```csharp
public sealed class ResolvedBreakpointHitEventArgs : EventArgs
{
    public required string BreakpointId { get; init; }
    public required int ThreadId { get; init; }
    public required BreakpointLocation Location { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required int HitCount { get; init; }
    public bool ShouldContinue { get; set; }  // BatchRunner can override
}
```

### Tool: `batch_evaluate`

MCP tool on `BatchEvaluateTool`. Accepts JSON parameters:
- `experiments`: array (1–20) of experiment objects
- `timeout_seconds`: int (default 30)
- `eval_mode`: `"safe"` | `"full"` (default `"safe"`)
- `max_total_hits`: int (default 500)

Each experiment object:
- `trigger`: `{ "file": string, "line": int }` | `{ "exception_type": string }`
- `mode`: `"blocking"` | `"non_blocking"` (default `"non_blocking"`)
- `capture`: string[] — variable names or expressions to evaluate
- `condition`: string? — C# expression (filtered through eval_mode)
- `max_hits`: int (default 1)

Returns:
```json
{
  "success": true,
  "completion_reason": "all_triggered | timeout | process_exited | cancelled | hit_limit_reached",
  "total_experiments": 3,
  "triggered": 3,
  "not_triggered": 0,
  "errors": 0,
  "experiments": [
    {
      "index": 0,
      "status": "triggered",
      "hit_count": 1,
      "hits": [
        {
          "timestamp": "...",
          "thread_id": 1,
          "location": { "file": "...", "line": 42 },
          "values": { "count": "5", "name": "\"Alice\"" },
          "eval_errors": {}
        }
      ]
    }
  ]
}
```

### Data Model

See [data-model.md](data-model.md) for record definitions.

---

## Verification

```bash
# 1. Build
dotnet build

# 2. Unit + contract tests
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"

# 3. Focused batch tests
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~BatchRunner"

# 4. Tool annotation contract
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~ToolAnnotationTests"

# 5. Manual: submit a 3-experiment batch against DebugTestApp
#    (see quickstart.md for step-by-step)
```

Expected: 0 warnings, 0 errors, all tests green.
