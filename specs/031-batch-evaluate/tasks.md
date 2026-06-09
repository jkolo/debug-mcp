# Tasks: Batch Evaluate & Hypothesis Runner

**Input**: Design documents from `/specs/031-batch-evaluate/`  
**Branch**: `031-batch-evaluate`

**Organization**: Tasks grouped by user story. Constitution requires TDD — test tasks appear before implementation tasks within each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no blocking dependencies)
- **[Story]**: User story label (US1/US2/US3)

---

## Phase 1: Setup

**Purpose**: Create directory structure and model files needed by all stories.

- [X] T001 Create `DebugMcp/Models/Batch/` directory and stub files: `BatchCompletionReason.cs`, `ExperimentStatus.cs`, `ExperimentMode.cs`, `EvalMode.cs`
- [X] T002 [P] Create `DebugMcp/Models/Batch/ExperimentTrigger.cs` — abstract record with `SourceLocation(File, Line)` and `ExceptionType(TypeName)` subtypes (see data-model.md)
- [X] T003 [P] Create `DebugMcp/Models/Batch/Experiment.cs` — positional record: Trigger, Mode, Capture, Condition, MaxHits (default 1)
- [X] T004 [P] Create `DebugMcp/Models/Batch/ExperimentHit.cs` — positional record: Timestamp (DateTimeOffset), ThreadId, Location, Values, EvalErrors
- [X] T005 [P] Create `DebugMcp/Models/Batch/ExperimentResult.cs` — positional record: Index, Status, HitCount, Hits, ErrorMessage?
- [X] T006 [P] Create `DebugMcp/Models/Batch/BatchRequest.cs` — positional record: Experiments, TimeoutSeconds (30), EvalMode (Safe), MaxTotalHits (500)
- [X] T007 [P] Create `DebugMcp/Models/Batch/BatchResult.cs` — positional record: CompletionReason, TotalExperiments, TriggeredCount, NotTriggeredCount, ErrorCount, ExperimentResults
- [X] T008 Create `DebugMcp/Services/Batch/IBatchRunner.cs` — interface: `RunAsync(BatchRequest, CancellationToken): Task<BatchResult>`, `bool IsRunning`
- [X] T009 Create stub `DebugMcp/Services/Batch/BatchRunner.cs` implementing `IBatchRunner` — all methods throw `NotImplementedException`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add `BreakpointResolved` event to `BreakpointManager` — required by all user stories.

**⚠️ CRITICAL**: Phase 3+ cannot begin until T010 and T011 are complete.

- [X] T010 Add `ResolvedBreakpointHitEventArgs` sealed class to `DebugMcp/Services/Breakpoints/BreakpointManager.cs`: fields `BreakpointId`, `ThreadId`, `Location`, `Timestamp`, `HitCount`, settable `ShouldContinue`
- [X] T011 Add `event EventHandler<ResolvedBreakpointHitEventArgs>? BreakpointResolved` to `BreakpointManager`; fire it from `OnDebuggerBreakpointHit` AFTER condition check and hit-count increment, passing `e.ShouldContinue` through: if event handler sets `ShouldContinue = true`, set `e.ShouldContinue = true`

**Checkpoint**: `dotnet build` passes; existing tests green.

---

## Phase 3: User Story 1 — Test Multiple Hypotheses Without Sequential Blocking (P1) 🎯 MVP

**Goal**: Agent submits N experiments, gets back a structured summary with variable values — no intermediate tool calls.

**Independent Test**: Submit 3-experiment batch against DebugTestApp; receive `completion_reason: all_triggered` with hits for each experiment. See `quickstart.md` Scenario 1.

### Tests for User Story 1 (TDD — write first, verify they FAIL)

- [X] T012 [P] [US1] Unit test — `BatchRunner` registers 3 experiments as breakpoints via `IBreakpointManager.SetBreakpointAsync`; dispatches single hit to correct experiment by breakpoint ID in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerDispatchTests.cs`
- [X] T013 [P] [US1] Unit test — two experiments at same location share one physical breakpoint but each accumulate independent hits in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerDispatchTests.cs`
- [X] T014 [P] [US1] Unit test — `BatchRunner` disables all pre-existing breakpoints on start and restores them on completion in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerLifecycleTests.cs`
- [X] T015 [P] [US1] Unit test — blocking experiment: `BreakpointResolved` event arg `ShouldContinue` is set to `true` after variable collection in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerLifecycleTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] Implement `BatchRunner._bpToExperiments` dispatch table (`Dictionary<string, List<int>>`); implement `RunAsync` setup: validate request (1–20 experiments, no running batch), register experiments as breakpoints via `IBreakpointManager.SetBreakpointAsync`, populate dispatch table in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T017 [US1] Implement `BatchRunner.OnBreakpointResolved`: look up `_bpToExperiments` by breakpoint ID, for each matching experiment check per-experiment `MaxHits`, enqueue hit for background collection worker in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T018 [US1] Implement `BatchRunner` background `Channel<T>` collection worker: dequeue hit, call `IDebugSessionManager.GetVariables` with 100ms timeout per expression, append `ExperimentHit` to `ExperimentResult`, mark experiment as triggered in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T019 [US1] Implement pre-existing BP freeze: before starting, call `IBreakpointManager.GetBreakpointsAsync` + `GetExceptionBreakpointsAsync`, store `(id, originalEnabled)` pairs, call `SetBreakpointEnabledAsync(id, false)` for each enabled one; restore on batch end in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T020 [US1] Implement blocking-experiment auto-resume: in `OnBreakpointResolved`, for blocking-mode experiments set `e.ShouldContinue = true` after 100ms synchronous wait for variable collection in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T021 [US1] Implement batch cleanup: on completion remove all experiment-registered breakpoints via `RemoveBreakpointAsync`, restore pre-existing breakpoints, fire `TaskCompletionSource`; build and return `BatchResult` in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T022 [US1] Create `DebugMcp/Tools/BatchEvaluateTool.cs` — `[McpServerToolType]`, method `BatchEvaluateAsync`, parse JSON parameters into `BatchRequest`, call `IBatchRunner.RunAsync`, serialize `BatchResult` to JSON response; annotations: `ReadOnly=false, Destructive=false, Idempotent=false, OpenWorld=false`
- [X] T023 [US1] Register `IBatchRunner` / `BatchRunner` as singleton in `DebugMcp/Program.cs`; inject `BreakpointManager` (concrete) for event subscription, `IBreakpointManager` for operations, `IDebugSessionManager`, `ISafeExpressionAnalyzer?`

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~BatchRunner"` green; manual Scenario 1 from quickstart.md passes.

---

## Phase 4: User Story 2 — Observe Multiple Points Non-Blocking (P2)

**Goal**: Non-blocking experiments collect data and continue without pausing; multi-hit supported.

**Independent Test**: Submit 5 non-blocking experiments at a loop line with `max_hits: 5`; program runs to completion; each experiment has 5 hits with distinct variable values. See `quickstart.md` Scenario 2.

### Tests for User Story 2 (TDD — write first, verify they FAIL)

- [X] T024 [P] [US2] Unit test — non-blocking experiments are registered via `IBreakpointManager.SetTracepointAsync` (not `SetBreakpointAsync`) in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerDispatchTests.cs`
- [X] T025 [P] [US2] Unit test — non-blocking experiment hit does NOT set `e.ShouldContinue = true` (it was already false / tracepoint auto-continues) in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerDispatchTests.cs`
- [X] T026 [P] [US2] Unit test — experiment with `max_hits: 3` collects exactly 3 hits then stops collecting further hits at that location in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerDispatchTests.cs`

### Implementation for User Story 2

- [X] T027 [US2] In `BatchRunner.RunAsync` setup: register experiments with `Mode == NonBlocking` via `IBreakpointManager.SetTracepointAsync` instead of `SetBreakpointAsync` in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T028 [US2] In `BatchRunner.OnBreakpointResolved`: for non-blocking experiments, enqueue hit asynchronously (no synchronous wait, no `ShouldContinue` override); tracepoints already auto-continue via `BreakpointManager` in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T029 [US2] Enforce per-experiment `MaxHits`: when `ExperimentResult.HitCount >= Experiment.MaxHits`, skip further collection for that experiment; when all experiments have reached `MaxHits`, signal `AllTriggered` completion in `DebugMcp/Services/Batch/BatchRunner.cs`

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~BatchRunner"` still green; manual Scenario 2 passes.

---

## Phase 5: User Story 3 — Partial Results on Timeout or Early Exit (P3)

**Goal**: Batch returns partial results on timeout, process exit, or cancellation — no data silently dropped.

**Independent Test**: Submit batch with one unreachable experiment, 3-second timeout; response arrives at ~3s with `completion_reason: timeout`; reachable experiment has hits, unreachable has `status: not_triggered`. See `quickstart.md` Scenario 3.

### Tests for User Story 3 (TDD — write first, verify they FAIL)

- [X] T030 [P] [US3] Unit test — batch returns `BatchCompletionReason.Timeout` with partial results when timeout `CancellationToken` fires in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerLifecycleTests.cs`
- [X] T031 [P] [US3] Unit test — batch returns `BatchCompletionReason.ProcessExited` when `IProcessDebugger.StateChanged` fires `Disconnected` in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerLifecycleTests.cs`
- [X] T032 [P] [US3] Unit test — batch returns with collected data when external `CancellationToken` is cancelled in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerLifecycleTests.cs`

### Implementation for User Story 3

- [X] T033 [US3] In `BatchRunner.RunAsync`: create `CancellationTokenSource` linked to `TimeSpan.FromSeconds(request.TimeoutSeconds)` and caller's token; store as `_batchCts` in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T034 [US3] In `BatchRunner` constructor: subscribe to `IProcessDebugger.StateChanged`; when `Disconnected`, cancel `_batchCts` and set `_completionReason = ProcessExited` in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T035 [US3] In `BatchRunner.RunAsync`: await `_completionTcs.Task` with linked token; on `OperationCanceledException` determine reason (Timeout if deadline exceeded, Cancelled if external) and finalize `BatchResult` with `ExperimentStatus.NotTriggered` for any untriggered experiments in `DebugMcp/Services/Batch/BatchRunner.cs`

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~BatchRunner"` green; manual Scenario 3 passes.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Hit cap, eval_mode, contract test, observability, CLAUDE.md update.

- [X] T036 [P] Unit test — hit cap: when total hits across all experiments reaches `MaxTotalHits`, batch ends with `HitLimitReached` in `tests/DebugMcp.Tests/Unit/Batch/BatchRunnerLifecycleTests.cs`
- [X] T037 Implement `MaxTotalHits` soft cap in `BatchRunner` collection worker: `Interlocked.Increment` on total-hits counter; when `>= MaxTotalHits`, cancel `_batchCts` with `HitLimitReached` reason in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T038 [P] Implement `eval_mode` in `BatchRunner` collection worker: for each capture expression, if `EvalMode == Safe` call `ISafeExpressionAnalyzer.Analyze(expr)` first — if rejected, add to `EvalErrors` without calling `EvaluateAsync`; if `Full`, skip analyzer in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T039 [P] Contract test — add `batch_evaluate` entry to `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs`: verify tool name, annotations (`ReadOnly=false, Destructive=false, Idempotent=false, OpenWorld=false`), and description mentions "batch"
- [X] T040 [P] Add structured logging to `BatchRunner`: batch start (experiment count, timeout, eval_mode), each experiment registration, each hit (experiment index, thread, hit count), batch end (reason, total hits, duration) in `DebugMcp/Services/Batch/BatchRunner.cs`
- [X] T041 Update `CLAUDE.md` "Active Technologies" and "Recent Changes" sections to reflect `031-batch-evaluate`: `BatchRunner`, `IBatchRunner`, `batch_evaluate` tool, `BreakpointResolved` event

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately; T002–T007 fully parallel
- **Phase 2 (Foundational)**: Depends on Phase 1 completion — **blocks Phase 3+**
- **Phase 3 (US1)**: Depends on Phase 2; T012–T015 (tests) parallel before T016+
- **Phase 4 (US2)**: Depends on Phase 3 being complete (reuses `BatchRunner` infrastructure)
- **Phase 5 (US3)**: Depends on Phase 3 being complete (reuses `BatchRunner` infrastructure)
- **Phase 6 (Polish)**: Depends on Phase 3; T036–T040 partially parallel

### User Story Dependencies

- **US2 (P2)**: Needs `BatchRunner` skeleton from US1 — start Phase 4 after T021 (cleanup) is complete
- **US3 (P3)**: Needs `BatchRunner` skeleton from US1 — start Phase 5 after T021 is complete
- US2 and US3 can proceed in parallel once US1 is complete

### Within Each Story

- Tests → FAIL verification → Models/Service tasks → Integration → Checkpoint
- TDD cycle: RED (T012-T015, T024-T026, T030-T032) → GREEN (implementation) → REFACTOR

### Parallel Opportunities

- Phase 1: T002–T007 all parallel (different files)
- Phase 3: T012–T015 all parallel (different test methods)
- Phase 4: T024–T026 all parallel
- Phase 5: T030–T032 all parallel
- Phase 6: T036, T038, T039, T040 all parallel

---

## Parallel Example: User Story 1 Tests

```bash
# Launch all 4 failing tests simultaneously:
Task: "Unit test — BatchRunner registers experiments as breakpoints" (T012)
Task: "Unit test — same-location experiments share one physical breakpoint" (T013)
Task: "Unit test — pre-existing BP freeze/restore" (T014)
Task: "Unit test — blocking experiment ShouldContinue override" (T015)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T009) — ~30 min
2. Complete Phase 2: Foundational (T010–T011) — critical gate
3. Complete Phase 3: User Story 1 (T012–T023)
4. **STOP and VALIDATE**: Run `quickstart.md` Scenario 1
5. Feature usable by agents for basic hypothesis testing

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready (build passes)
2. Phase 3 → Core batch works (Scenario 1 passes) — **ship-worthy**
3. Phase 4 → Non-blocking observation added (Scenario 2 passes)
4. Phase 5 → Partial results on timeout (Scenario 3 passes) — **production-ready**
5. Phase 6 → Hit cap, eval_mode, full observability

---

## Notes

- 41 tasks total: 9 setup, 2 foundational, 14 US1, 6 US2, 6 US3, 6 polish
- TDD mandatory (constitution principle III): tests in each story MUST fail before implementation
- `BatchRunner` takes `BreakpointManager` (concrete) for event subscription, `IBreakpointManager` (interface) for operations — see research.md decision #1
- `ISafeExpressionAnalyzer` injected as nullable (`ISafeExpressionAnalyzer?`) — graceful degradation when `--no-roslyn` flag used
- Each Checkpoint: run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` and verify green
