# Research: Batch Evaluate & Hypothesis Runner

**Feature**: 031-batch-evaluate | **Date**: 2026-06-09

## Unknowns Resolved

### 1. Hook Point for Batch Dispatch

**Question**: Where in the existing pipeline should `BatchRunner` intercept breakpoint hits?

**Decision**: Add `event EventHandler<ResolvedBreakpointHitEventArgs>? BreakpointResolved` to `BreakpointManager` (concrete class). Fires after location resolution, condition evaluation, and hit-count increment — before the ICorDebug continue/pause decision.

**Rationale**: `BreakpointManager.OnBreakpointHit` already has all the resolved data. A new event requires minimal changes to `BreakpointManager` and no changes to `IBreakpointManager`. Batch dispatch happens synchronously on the ICorDebug thread (fast path: enqueue only), heavy work (variable collection) is on a background Channel worker.

**Alternatives rejected**:
- Subscribe to `IProcessDebugger.BreakpointHit` directly — raw ICorDebug events require re-implementing location resolution.
- Decorate `IBreakpointNotifier` — notifications are already post-pause-decision; can't affect continue/pause behavior.
- Modify `IBreakpointManager` interface — would require updating all mocks in existing tests.

---

### 2. Multi-Experiment Same-Location Dispatch

**Question**: How to handle two experiments at the same source location without registering duplicate ICorDebug breakpoints?

**Decision**: `BatchRunner` maintains `Dictionary<string, List<int>> _bpToExperiments` (keyed by breakpoint ID). When registering experiments, calls `IBreakpointManager.SetBreakpointAsync` — the existing dedup logic in `BreakpointManager` returns the same `Breakpoint` object for duplicate locations. Both experiments share one physical breakpoint ID; `BatchRunner` routes hits to both experiment indexes.

**Rationale**: No changes to `BreakpointManager` or ICorDebug layer. The dedup behavior is already tested and production-proven.

---

### 3. Pre-Existing Breakpoint Freeze

**Decision**: On batch start, iterate `IBreakpointManager.GetBreakpointsAsync()` and `GetExceptionBreakpointsAsync()`; call `SetBreakpointEnabledAsync(id, false)` for each that is currently enabled, storing `(id, true)` for restoration. On batch end (any reason), restore in reverse order.

**Rationale**: `SetBreakpointEnabledAsync` already calls `CorDebugFunctionBreakpoint.Activate(false)` — no new ICorDebug API required. Exception breakpoints don't have native activation; their `Enabled` flag is checked at runtime in `BreakpointManager.OnExceptionHit` — disabling prevents dispatch without native changes.

---

### 4. eval_mode Implementation

**Decision**: `BatchRunner` accepts `EvalMode` enum (`Safe` / `Full`). For variable capture expressions, if `Safe`: call `ISafeExpressionAnalyzer.Analyze(expr)` first; reject if not safe and record as eval error in hit. If `Full`: call `IDebugSessionManager.EvaluateAsync()` directly.

**Integration point**: `ISafeExpressionAnalyzer` is already registered as a singleton via `Program.cs` (feature 029). `BatchRunner` takes it as an optional constructor parameter (nullable) to avoid hard dependency when safe-eval is disabled via `--no-roslyn`.

---

### 5. Blocking Experiment Auto-Resume

**Decision**: For batch experiments registered as `blocking`, `BatchRunner` sets `e.ShouldContinue = true` on `ResolvedBreakpointHitEventArgs` AFTER the background worker has finished collecting variables (synchronous wait with 100ms timeout), then returns from the event handler. `BreakpointManager.OnDebuggerBreakpointHit` reads `e.ShouldContinue` after `BreakpointResolved` fires and sets `BreakpointHitEventArgs.ShouldContinue` accordingly.

**Note**: The 100ms wait happens on the ICorDebug callback thread. This is safe because ICorDebug callbacks are already serialized and the process is stopped. The 100ms cap matches the per-hit evaluation budget from feature 030.

**Alternative rejected**: Call `IDebugSessionManager.ContinueAsync()` from `BatchRunner` background thread — requires careful synchronization to avoid continuing before all experiments at that location have been dispatched.

---

## Existing Infrastructure Reused

| Component | Usage in Batch |
|-----------|---------------|
| `BreakpointManager.SetBreakpointAsync` | Register experiment triggers (dedup at same location) |
| `BreakpointManager.SetTracepointAsync` | Register non-blocking experiment triggers |
| `BreakpointManager.SetBreakpointEnabledAsync` | Freeze/restore pre-existing breakpoints |
| `BreakpointManager.RemoveBreakpointAsync` | Cleanup experiment breakpoints on batch end |
| `IDebugSessionManager.EvaluateAsync` / `GetVariables` | Variable collection per hit |
| `ISafeExpressionAnalyzer.Analyze` | Expression gate for `eval_mode: safe` |
| `System.Threading.Channels.Channel<T>` | Hit queue between ICorDebug callback thread and collection worker |
| `BreakpointRegistry.FindByLocation` | Used internally by `BreakpointManager` for dedup |
