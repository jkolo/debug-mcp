---

description: "Task list for 069-mcp-surface-modernization"
---

# Tasks: MCP Surface Modernization

**Input**: Design documents from `/specs/069-mcp-surface-modernization/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: **MANDATORY, not optional.** The project constitution makes Test-First
NON-NEGOTIABLE: *"Tests MUST be written before implementation. Tests MUST fail before
implementation (Red phase)."* Every story phase below therefore opens with its tests, and those
tests must be seen failing before the implementation tasks in that phase begin.

**Organization**: Tasks are grouped by user story. Unlike the template's default assumption,
these five stories are **not** freely parallelizable — see [Dependencies](#dependencies--execution-order).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Exact file paths in every description

## Path Conventions

Single project. Server code under `DebugMcp/`, tests under `tests/DebugMcp.Tests/`, fixtures
under `tests/DebugTestApp/`. Paths below are repository-relative.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Make the new dependency available without disturbing anything else.

- [X] T001 Add `ModelContextProtocol.Extensions.Tasks` version `2.2.0` to `Directory.Packages.props` (Label="MCP" ItemGroup) and a versionless `<PackageReference>` to `DebugMcp/DebugMcp.csproj`
- [X] T002 Verify `dotnet build` still reports 0 errors and 0 warnings after T001, and record the package in `docs/dependencies.md` alongside the existing MCP SDK section

**Checkpoint**: Dependency present, build clean, nothing behavioural changed yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared types and test doubles that more than one story needs.

**⚠️ CRITICAL**: No user story work begins until this phase is complete.

- [X] T003 [P] Create `IProgressReporter` in `DebugMcp/Services/Progress/IProgressReporter.cs` — a first-party wrapper over `IProgress<ProgressNotificationValue>` exposing `ReportStage(string stage, int? completed, int? total)`, mirroring the `IBreakpointNotifier` precedent so it can be mocked
- [X] T004 [P] Create `RecordingProgressReporter` test double in `tests/DebugMcp.Tests/Support/RecordingProgressReporter.cs` capturing an ordered list of reported stages
- [X] T005 [P] Create `ToolResult<T>`, `ToolError` and `TruncationInfo` positional records in `DebugMcp/Models/Results/ToolResult.cs` per [data-model.md](./data-model.md) §1, with the invariants (`Success == true` ⟹ `Error` null and `Data` non-null, and the converse) enforced in the constructor
- [X] T006 [P] Write failing unit tests for the `ToolResult<T>` invariants in `tests/DebugMcp.Tests/Unit/Models/ToolResultTests.cs`
- [X] T007 Add a JSON-schema validation helper to `tests/DebugMcp.Tests/Support/SchemaValidator.cs` that validates an arbitrary payload against a tool's published `outputSchema` — used by US3 and US4 contract tests (backed by the `JsonSchema.Net` package, test-project-only, added to CPM)

**Checkpoint**: Shared types exist and are tested; story work can begin.

---

## Phase 3: User Story 1 — Long operations become observable and interruptible (Priority: P1) 🎯 MVP

**Goal**: Every tool is asynchronous and cancellable; the five long operations report named
stages. No result shape changes, so no client can break.

**Independent Test**: Invoke a long-running tool with a progress token and assert ordered stage
updates arrive before the result; invoke it without a token and assert identical behaviour to
today; cancel an in-flight call and assert the session stays usable.

### Tests for User Story 1 ⚠️ Write first, watch them fail

- [ ] T008 [P] [US1] Write failing tests asserting the ReSharper stage sequence (acquiring engine → restoring → building solution → inspecting → parsing report) against `RecordingProgressReporter` in `tests/DebugMcp.Tests/Unit/Progress/ReSharperProgressTests.cs`
- [ ] T009 [P] [US1] Write failing tests asserting `batch_evaluate` emits one counted update per expression in `tests/DebugMcp.Tests/Unit/Progress/BatchEvaluateProgressTests.cs`
- [ ] T010 [P] [US1] Write failing tests asserting `code_load` and `debug_launch` stage sequences in `tests/DebugMcp.Tests/Unit/Progress/WorkspaceAndLaunchProgressTests.cs`
- [ ] T011 [P] [US1] Write failing tests asserting a tool with no progress reporter attached completes normally and emits nothing in `tests/DebugMcp.Tests/Unit/Progress/ProgressDegradationTests.cs`
- [ ] T012 [P] [US1] Write failing cancellation tests asserting the debug session remains usable after a cancelled call in `tests/DebugMcp.Tests/Unit/Cancellation/SessionConsistencyAfterCancelTests.cs`
- [ ] T013 [P] [US1] Write a failing contract test asserting every one of the 39 tool methods returns `Task<...>` and accepts a `CancellationToken` in `tests/DebugMcp.Tests/Contract/ToolAsyncContractTests.cs`

### Implementation for User Story 1

- [ ] T014 [P] [US1] Convert `DebugMcp/Tools/VariablesGetTool.cs` and `DebugMcp/Tools/StacktraceGetTool.cs` from `public string` to `public async Task<string>` and thread `CancellationToken`
- [ ] T015 [P] [US1] Convert `DebugMcp/Tools/SnapshotCreateTool.cs`, `SnapshotDeleteTool.cs` and `SnapshotDiffTool.cs` to async with `CancellationToken`
- [ ] T016 [P] [US1] Convert `DebugMcp/Tools/TimelineQueryTool.cs`, `ProcessReadOutputTool.cs` and `ProcessWriteInputTool.cs` to async with `CancellationToken`
- [ ] T017 [US1] Add `CancellationToken` parameters to the 16 tool files in `DebugMcp/Tools/` that lack one, and thread each token down to the first cancellable boundary in the corresponding service
- [ ] T018 [US1] Audit every conversion from T014–T017 against the rule in [research.md](./research.md) R7 — **no `await` may span a region holding `_lock` or `_stateLock`** — and record the audit result per file in the task's commit message
- [ ] T019 [P] [US1] Add `IProgressReporter` to `DebugMcp/Tools/ReSharperInspectSolutionTool.cs` and `ReSharperInspectProjectTool.cs`, emitting the five stages from [contracts/progress-contract.md](./contracts/progress-contract.md)
- [ ] T020 [P] [US1] Add counted progress (`expression n of m`) to `DebugMcp/Tools/BatchEvaluateTool.cs`
- [ ] T021 [P] [US1] Add progress stages to `DebugMcp/Tools/DebugLaunchTool.cs` (starting process → attaching → resolving symbols → ready)
- [ ] T022 [P] [US1] Add progress stages to `DebugMcp/Tools/CodeLoadTool.cs` (locating MSBuild → loading workspace → project n of m)
- [ ] T023 [US1] Implement the 60-second heartbeat in `DebugMcp/Services/Progress/` — a silent stage re-emits its current stage without advancing `Completed`, satisfying SC-001 during engine download
- [ ] T024 [US1] Create an adapter in `DebugMcp/Services/Progress/ProgressReporterAdapter.cs` that wraps the SDK-bound `IProgress<ProgressNotificationValue>` **method parameter** into `IProgressReporter`. Deliberately **no** DI registration keyed on progress tokens: whether a token was supplied is per-request information the container cannot see, and the SDK already discards reports when there is none — degradation is structural, not conditional ([research.md](./research.md) R2)

**Checkpoint**: US1 complete. Long operations are visible and interruptible; result shapes
untouched; a client that ignores progress sees no change whatsoever.

---

## Phase 4: User Story 2 — Long operations return a handle instead of blocking (Priority: P2)

**Goal**: The five qualifying tools return a handle to opted-in clients and behave exactly as
before to everyone else.

**Independent Test**: With a client declaring the tasks extension, assert a handle returns in
under a second and that polling to completion yields a payload byte-identical to the blocking
path; without the declaration, assert the blocking path is used.

### Tests for User Story 2 ⚠️ Write first, watch them fail

- [ ] T025 [P] [US2] Write a failing test asserting a request **without** the tasks extension never receives a `resultType: "task"` in `tests/DebugMcp.Tests/Unit/Tasks/OptInGatingTests.cs`
- [ ] T026 [P] [US2] Write a failing test asserting the deferred payload equals the blocking payload byte-for-byte for the same inputs in `tests/DebugMcp.Tests/Unit/Tasks/PathEquivalenceTests.cs`
- [ ] T027 [P] [US2] Write failing tests for the lifecycle transitions (working → completed / failed / cancelled, terminal states immutable) in `tests/DebugMcp.Tests/Unit/Tasks/TaskLifecycleTests.cs`
- [ ] T028 [P] [US2] Write failing tests asserting unknown-id and expired-id produce **distinguishable** errors in `tests/DebugMcp.Tests/Unit/Tasks/HandleErrorTests.cs`
- [ ] T029 [P] [US2] Write a failing contract test asserting all 34 non-qualifying tools are pinned to `TaskSupport = Forbidden` in `tests/DebugMcp.Tests/Contract/TaskSupportContractTests.cs`
- [ ] T030 [P] [US2] Write failing tests for debuggee-termination and client-disconnect while a handle is outstanding in `tests/DebugMcp.Tests/Unit/Tasks/TaskFailurePathTests.cs`

### Implementation for User Story 2

- [ ] T031 [US2] Register `InMemoryMcpTaskStore` via `builder.WithTasks(store, opts => ...)` in `DebugMcp/Program.cs` — confirmed against the installed 2.2.0 assemblies that this is the only wiring path; `AddMcpServer(options => options.TaskStore = ...)` and a per-tool `Execution.TaskSupport` property do **not** exist in this SDK version ([research.md](./research.md) R1, corrected). Also verify whether `InMemoryMcpTaskStore` distinguishes an **expired** id from an **unknown** one — FR-012 requires the two to be distinguishable — and if it does not, wrap it in a decorator in `DebugMcp/Services/Tasks/` that does. Set and justify the handle defaults FR-009 requires: `ttlMs` (proposed 1 hour, long enough to outlive the slowest qualifying operation) and `pollIntervalMs` (proposed 2 s, cheap over stdio), both overridable by configuration
- [ ] T032 [US2] Create `TaskExecutionPolicy` in `DebugMcp/Services/Tasks/TaskExecutionPolicy.cs` holding the five qualifying tool names (FR-013) → `McpTaskExecutionMode.Optional`, everything else → `Synchronous`, and wire it as `McpTasksOptions.ExecutionModeSelector` in T031's registration — **the SDK's documented default selector treats every tool as task-capable, so omitting this silently makes all 39 tools task-eligible**. There is no per-tool setting to "pin"; the policy is the single source of truth T029's contract test asserts against
- [ ] T033 [US2] *(merged into T032 — the qualifying-tool table lives in one file, not scattered per-tool settings; kept as a no-op marker so task numbering stays stable)*
- [ ] T034 [US2] *(merged into T032 — see T033)*
- [ ] T035 [US2] Document in `DebugMcp/Tools/DebugLaunchTool.cs`'s `[Description]` that an opted-in client receives a handle even for a fast launch (see [contracts/deferred-result-contract.md](./contracts/deferred-result-contract.md)) — the qualification itself is registered in T032's policy table, not on this file
- [ ] T036 [US2] Bridge `IProgressReporter` stage updates into the task's `statusMessage` in `DebugMcp/Services/Progress/ProgressReporterAdapter.cs`, so a polling client sees the same stage names a progress-token client sees
- [ ] T037 [US2] Implement cooperative cancellation: `tasks/cancel` acknowledges immediately, but an indivisible ICorDebug step completes first so the debuggee is never left inconsistent (FR-003)
- [ ] T038 [US2] Implement the debuggee-termination path in `DebugMcp/Services/DebugSessionManager.cs` and the task bridge — drive an outstanding handle to `failed` with the reason, never `completed`, never left `working`

**Checkpoint**: US2 complete. Opted-in clients get handles; everyone else is unaffected.

---

## Phase 5: User Story 3 — Every tool publishes a checkable result contract (Priority: P3)

**Goal**: All 39 tools return typed results with a published `outputSchema`, retain the text
block for backward compatibility, and fail in one shared shape.

**Independent Test**: Assert every tool in `tools/list` carries an `outputSchema`; assert each
tool's `structuredContent` validates against it; assert a client reading only `content[0].text`
works unchanged.

### Tests for User Story 3 ⚠️ Write first, watch them fail

- [ ] T039 [P] [US3] Write a failing contract test asserting all 39 tools publish an `outputSchema` in `tests/DebugMcp.Tests/Contract/OutputSchemaPresenceTests.cs`
- [ ] T040 [P] [US3] Write a failing contract test validating each tool's result against its own published schema, using the T007 helper, in `tests/DebugMcp.Tests/Contract/OutputSchemaConformanceTests.cs`
- [ ] T041 [P] [US3] Write a failing contract test asserting every failure carries `isError: true` **and** `success: false` **and** a code drawn from `ErrorCodes` in `tests/DebugMcp.Tests/Contract/ErrorShapeContractTests.cs`
- [ ] T042 [P] [US3] Write a failing backward-compatibility test that parses only `content[0].text` and asserts field names and meanings are unchanged from today, in `tests/DebugMcp.Tests/Contract/LegacyTextContractTests.cs`
- [ ] T043 [P] [US3] Write a failing documentation-coverage test asserting every tool is named in `website/docs/tools/*.md` and no documented tool is missing, in `tests/DebugMcp.Tests/Contract/ToolDocCoverageTests.cs`

### Implementation for User Story 3

Each task below creates the payload records in `DebugMcp/Models/Results/`, sets
`UseStructuredContent = true`, and converts the tool's return type. Field names and meanings are
carried over unchanged (FR-021). All are `[P]` — disjoint file sets.

- [ ] T044 [P] [US3] Migrate the 6 session/execution tools (`DebugAttachTool`, `DebugLaunchTool`, `DebugDisconnectTool`, `DebugContinueTool`, `DebugPauseTool`, `DebugStepTool`) in `DebugMcp/Tools/`
- [ ] T045 [P] [US3] Migrate the 5 breakpoint tools (`BreakpointSetTool`, `BreakpointRemoveTool`, `BreakpointEnableTool`, `BreakpointSetExceptionTool`, `TracepointSetTool`) in `DebugMcp/Tools/`
- [ ] T046 [P] [US3] Migrate 6 inspection tools (`VariablesGetTool`, `StacktraceGetTool`, `EvaluateTool`, `EvaluateSafeTool`, `ObjectInspectTool`, `ObjectSummarizeTool`) in `DebugMcp/Tools/`
- [ ] T047 [P] [US3] Migrate 6 further inspection tools (`CollectionAnalyzeTool`, `ExceptionGetContextTool`, `LayoutGetTool`, `MembersGetTool`, `ReferencesGetTool`, `TypesGetTool`) in `DebugMcp/Tools/`
- [ ] T048 [P] [US3] Migrate the 5 code-analysis tools (`CodeLoadTool`, `CodeGoToDefinitionTool`, `CodeFindUsagesTool`, `CodeFindAssignmentsTool`, `CodeGetDiagnosticsTool`) in `DebugMcp/Tools/`
- [ ] T049 [P] [US3] Migrate the 2 ReSharper tools (`ReSharperInspectSolutionTool`, `ReSharperInspectProjectTool`) in `DebugMcp/Tools/`
- [ ] T050 [P] [US3] Migrate the 3 snapshot tools (`SnapshotCreateTool`, `SnapshotDeleteTool`, `SnapshotDiffTool`) in `DebugMcp/Tools/`
- [ ] T051 [P] [US3] Migrate the 2 process-I/O tools (`ProcessReadOutputTool`, `ProcessWriteInputTool`) in `DebugMcp/Tools/`
- [ ] T052 [P] [US3] Migrate `MemoryReadTool`, `ModulesSearchTool`, `BatchEvaluateTool` and `TimelineQueryTool` in `DebugMcp/Tools/`
- [ ] T053 [US3] In `DebugMcp/Models/Results/ToolResultSerializer.cs`, set protocol-level `isError` from `ToolResult.Success` centrally so no tool sets it individually, **and** guarantee the serialized-JSON text block accompanies `structuredContent` on every result — FR-017 is a spec-level SHOULD that the SDK may not honour by itself, so this task owns it rather than leaving T042 as a test with no implementation behind it
- [ ] T054 [US3] Implement the 256 KB serialized-result budget from FR-035 in `DebugMcp/Models/Results/ToolResultSerializer.cs`, and attach `TruncationInfo` in the 14 collection-returning tools FR-035 enumerates, replacing any silent trimming. Tools outside that list must not truncate
- [ ] T055 [US3] Update `website/docs/tools/*.md` so every tool name is present and none is stale, making T043 pass
- [ ] T056 [US3] Wire T039–T043 into the build so divergence fails it, per the four conditions in [contracts/tool-result-contract.md](./contracts/tool-result-contract.md)

**Checkpoint**: US3 complete. 39 of 39 tools typed, schema-published, backward compatible.

---

## Phase 6: User Story 4 — Diagnostic results arrive pre-ranked (Priority: P4)

**Goal**: The four analysis tools return a deterministic ranking of candidate frames with
concrete evidence. No language model anywhere.

**Independent Test**: Run each fixture in the corpus and assert the human-identified fault frame
ranks first in at least 8 of 10; run one fixture 10 times and assert identical normalized
enrichment output.

### Corpus first — nothing else in this phase is measurable without it

- [ ] T057 [US4] Create 10 deterministic fault fixtures in `tests/DebugTestApp/FaultScenarios/`, covering at minimum a null dereference, a fault in a nested call chain, a fault across an async boundary, an aggregate/inner exception, and one with symbols deliberately unavailable (FR-030). No unseeded randomness, wall-clock branching or racing threads — see [data-model.md](./data-model.md#what-determinism-means-here-precisely)
- [ ] T058 [US4] Record the human-identified fault frame for each fixture in `tests/DebugTestApp/FaultScenarios/expected-answers.json`

### Tests for User Story 4 ⚠️ Write first, watch them fail

- [ ] T059 [P] [US4] Write a failing test asserting the fault site ranks first in ≥8 of the 10 fixtures, in `tests/DebugMcp.Tests/Unit/Enrichment/RankingAccuracyTests.cs`
- [ ] T060 [P] [US4] Write a failing test running one fixture 10 times and comparing **normalized** enrichment output (frame index, score, heuristic, weight, evidence, ordering — excluding addresses, thread IDs, PIDs, durations), in `tests/DebugMcp.Tests/Unit/Enrichment/DeterminismTests.cs`
- [ ] T061 [P] [US4] Write a failing test asserting every pre-existing raw field survives enrichment untouched, in `tests/DebugMcp.Tests/Unit/Enrichment/AdditiveOnlyTests.cs`
- [ ] T062 [P] [US4] Write a failing test asserting the no-symbols fixture yields an explicit `RankingUnavailable` with a reason, raw data intact and the call succeeding, in `tests/DebugMcp.Tests/Unit/Enrichment/RankingUnavailableTests.cs`
- [ ] T063 [P] [US4] Write a failing per-heuristic test file in `tests/DebugMcp.Tests/Unit/Enrichment/Heuristics/` — one test per rule, each asserting the rule fires on its own fixture and not on others (FR-027)

### Implementation for User Story 4

- [ ] T064 [P] [US4] Create `RankedSuspect`, `SuspicionReason` and `RankingUnavailable` records in `DebugMcp/Models/Inspection/RankedSuspect.cs` per [data-model.md](./data-model.md) §5
- [ ] T065 [US4] Implement the heuristic engine in `DebugMcp/Services/Inspection/SuspicionRanker.cs` with documented constant weights, no wall-clock, no random source, no hash-order iteration, and ties broken on `FrameIndex` ascending
- [ ] T066 [US4] Document every heuristic and its weight in `docs/enrichment-heuristics.md`, so FR-027's "documented and individually testable" is satisfiable
- [ ] T067 [US4] Add ranking to `DebugMcp/Services/ExceptionAutopsyService.cs`, surfaced through `ExceptionGetContextTool`
- [ ] T068 [P] [US4] Add ranking to `DebugMcp/Tools/StacktraceGetTool.cs`
- [ ] T069 [P] [US4] Add ranking to `DebugMcp/Tools/ObjectSummarizeTool.cs` and `CollectionAnalyzeTool.cs`
- [ ] T070 [US4] Extend the affected payload records in `DebugMcp/Models/Results/` and their published schemas to carry the new fields, keeping every existing field in place (FR-025)
- [ ] T071 [US4] Measure token cost of diagnosing a fixture before and after, and record the comparison in `docs/enrichment-heuristics.md` to evidence SC-006

**Checkpoint**: US1–US4 functional. Only the timeout slice remains.

---

## Phase 7: User Story 5 — Every blocking operation can be bounded by a timeout (Priority: P5)

**Goal**: Every blocking tool accepts an optional timeout with a documented default, closing the
constitution's tool-standards requirement that ~27 of 40 tool files violate today.

**Independent Test**: Invoke a blocking tool with a deliberately short timeout against work known
to exceed it, and assert a timeout error returns within the budget and the next call succeeds;
invoke it with no timeout and assert the documented default applies.

### Tests for User Story 5 ⚠️ Write first, watch them fail

- [ ] T072 [P] [US5] Write a failing contract test asserting every tool classified as blocking exposes an optional timeout parameter with a documented default, and that non-blocking tools do **not**, in `tests/DebugMcp.Tests/Contract/TimeoutParameterContractTests.cs` (FR-031, SC-011)
- [ ] T073 [P] [US5] Write failing tests asserting an exhausted budget returns the timeout error code naming the elapsed budget and leaves the session usable, in `tests/DebugMcp.Tests/Unit/Timeouts/TimeoutExpiryTests.cs` (FR-033, SC-012)
- [ ] T074 [P] [US5] Write failing tests asserting per-tool defaults — 30 s for ordinary tools, the tool's own longer documented default for the long-running five — in `tests/DebugMcp.Tests/Unit/Timeouts/TimeoutDefaultsTests.cs`
- [ ] T075 [P] [US5] Write a failing test asserting a timeout does not abandon an indivisible runtime step mid-flight (FR-034 defers to FR-003), in `tests/DebugMcp.Tests/Unit/Timeouts/TimeoutConsistencyTests.cs`

### Implementation for User Story 5

- [ ] T076 [US5] Create `TimeoutPolicy` in `DebugMcp/Services/Timeouts/TimeoutPolicy.cs` classifying each of the 39 tools as **blocking** (waits on debuggee, build, symbol server or ReSharper engine) or **in-memory only**, and holding each blocking tool's default. The classification is data, not scattered judgement — T072 asserts against it
- [ ] T077 [US5] Add a timeout error code to `DebugMcp/Models/ErrorResponse.cs` if the existing `ErrorCodes` set has none, keeping FR-019's "no tool invents a code" intact
- [ ] T078 [P] [US5] Add the optional timeout parameter to the blocking session/execution and breakpoint tools in `DebugMcp/Tools/`, per the FR-031 classification
- [ ] T079 [P] [US5] Add the optional timeout parameter to the blocking inspection tools in `DebugMcp/Tools/`, per the FR-031 classification
- [ ] T080 [P] [US5] Add the optional timeout parameter to the blocking code-analysis and ReSharper tools in `DebugMcp/Tools/`, per the FR-031 classification
- [ ] T081 [P] [US5] Add the optional timeout parameter to the remaining blocking tools (memory, modules, snapshots, process I/O, batch) in `DebugMcp/Tools/`, per the FR-031 classification
- [ ] T082 [US5] In `DebugMcp/Services/Timeouts/TimeoutPolicy.cs`, verify the five long-running tools keep their existing longer documented defaults rather than inheriting 30 s — `ReSharperOptions.InspectionTimeoutSeconds` is already 300, and imposing 30 s on `resharper_inspect_solution` would break a tool that routinely runs for minutes (FR-032)
- [ ] T083 [US5] Document every blocking tool's timeout parameter and its default in the tool `[Description]` attributes and in `website/docs/tools/*.md`

**Checkpoint**: US5 complete. No constitution requirement outstanding.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T084 [P] Fix `ROADMAP.md`: resolve the duplicate `Tier 4` heading, resolve the `034` collision between shipped ReSharper Inspections and proposed Edit-and-Continue, and reconcile `031`/`032` currently listed as both completed and proposed (FR-028)
- [ ] T085 [P] Mark `#061`, `#062` and `#066` as fully absorbed by this feature in `ROADMAP.md`, and `#037` as partially absorbed with its remaining scope open (FR-029)
- [ ] T086 [P] Add this feature to the Completed Features table in `ROADMAP.md` and to the Recent Changes section of `CLAUDE.md`
- [ ] T087 [P] Update `website/docs/tools/index.md` and `website/docs/architecture.md` to describe progress, deferred results, typed outputs and timeouts
- [ ] T088 [P] Write a contract test in `tests/DebugMcp.Tests/Contract/NoModelDependencyTests.cs` asserting the server references no language-model provider client and exposes no configuration path accepting a model credential (FR-022, SC-013). FR-022 is the defining architectural decision of this feature and is the one requirement with no natural implementation task — without this guard nothing would ever detect its violation
- [ ] T089 Run every scenario in [quickstart.md](./quickstart.md) end to end over stdio, filtering responses by `id` because the server interleaves notification lines, and record the longest single request/response exchange for each of the five qualifying operations to evidence SC-003
- [ ] T090 Final gate: `dotnet build` and `dotnet build -c Release` at 0 errors / 0 warnings (SC-010), plus the full Unit+Contract suite green

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup. **Blocks all five stories.**
- **Stories (Phases 3–7)**: see the ordering constraint below — they are *not* freely parallel.
- **Polish (Phase 8)**: depends on whichever stories were shipped.

### Story ordering — a real constraint, not a preference

The template's default is that stories are independent. **Here they are not**, and pretending
otherwise would produce rework:

- **US1 → US2**: an operation cannot be handed off as a task until it can first report on itself
  and be stopped. T036 bridges US1's stage reporting into US2's task status; without US1 there is
  nothing to bridge.
- **US3 → US4**: enrichment adds fields. Adding them to typed records is one change; adding them
  to hand-built JSON strings and then migrating means doing it twice.
- **US3 → US5**: US5's timeout errors use the shared error shape built in US3. Shipping US5 first
  would mean writing that error twice.
- **US1/US2 ↔ US3/US4**: independent of each other. A team could run the async track (US1→US2) and
  the contracts track (US3→US4) in parallel, with one merge point — T070 extends records created
  in T044–T052, and T032 pins `TaskSupport` on tools that T044–T052 also edit.

Intended sequence: **US1 → US2 → US3 → US4 → US5**. Every arrow is a viable stopping point.

US5 sits last for a reason worth restating: it is the only slice that touches tool **inputs**.
Every other slice changes how results are shaped and delivered. Keeping the input change in its
own slice is what stops the two migrations landing in the same review — and it is the surviving
half of the argument that originally, wrongly, kept timeouts out of this feature entirely.

### Within each story

- Tests are written first and must be seen failing (constitution, Principle III).
- Models before services, services before tools.
- For US4, the corpus (T057–T058) precedes its tests, because the tests assert against it.

### Parallel opportunities

- Phase 2: T003–T006 all `[P]`.
- US1: all six test tasks `[P]`; conversions T014–T016 `[P]`; progress additions T019–T022 `[P]`.
- US2: all six test tasks `[P]`. T032 (the execution-mode policy) is a single-file task, not parallelizable across tools — see the T031 correction note above.
- US3: all five test tasks `[P]`; **the nine migration tasks T044–T052 are fully parallel** — disjoint files, this is the largest parallel win in the feature.
- US4: five test tasks `[P]`; T068–T069 `[P]`.
- US5: all four test tasks `[P]`; the four parameter-addition tasks T078–T081 `[P]`.
- Phase 8: T084–T088 `[P]`.

---

## Parallel Example: User Story 3

```bash
# All contract tests first — they must fail before any migration:
Task: "Assert all 39 tools publish an outputSchema in tests/DebugMcp.Tests/Contract/OutputSchemaPresenceTests.cs"
Task: "Validate each tool result against its schema in tests/DebugMcp.Tests/Contract/OutputSchemaConformanceTests.cs"
Task: "Assert error shape in tests/DebugMcp.Tests/Contract/ErrorShapeContractTests.cs"
Task: "Assert legacy text block unchanged in tests/DebugMcp.Tests/Contract/LegacyTextContractTests.cs"
Task: "Assert doc coverage in tests/DebugMcp.Tests/Contract/ToolDocCoverageTests.cs"

# Then all nine migrations at once — disjoint files:
Task: "Migrate 6 session/execution tools in DebugMcp/Tools/"
Task: "Migrate 5 breakpoint tools in DebugMcp/Tools/"
Task: "Migrate 6 inspection tools in DebugMcp/Tools/"
Task: "Migrate 6 further inspection tools in DebugMcp/Tools/"
Task: "Migrate 5 code-analysis tools in DebugMcp/Tools/"
Task: "Migrate 2 ReSharper tools in DebugMcp/Tools/"
Task: "Migrate 3 snapshot tools in DebugMcp/Tools/"
Task: "Migrate 2 process-I/O tools in DebugMcp/Tools/"
Task: "Migrate memory, modules, batch and timeline tools in DebugMcp/Tools/"
```

---

## Implementation Strategy

### MVP — User Story 1 only

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1.
2. **Stop and validate**: run quickstart Scenario 1.
3. This alone closes the constitution's outstanding Principle II violation on progress reporting,
   and changes no result shape — the lowest-risk shippable increment in the feature.

### Incremental delivery

| Increment | Delivers | Client impact |
|---|---|---|
| + US1 | progress and cancellation | none — additive |
| + US2 | deferred results | none unless the client opts in |
| + US3 | typed results and schemas | none — text block retained |
| + US4 | ranked diagnostics | none — additive fields |
| + US5 | per-call timeouts | none — the parameter is optional and defaults preserve today's behaviour |

Every increment is backward compatible by construction, which is what SC-009 demands.

Note that US5 is backward compatible **only because** FR-032 keeps the long-running tools' own
longer defaults. A uniform 30-second default would break `resharper_inspect_solution` on its very
first call — compliance achieved by regression is not compliance.

### Highest-risk tasks

Four tasks are where this feature most plausibly goes wrong:

- **T018** — the lock-invariant audit. A missed `await` under a lock introduces interleaving that
  is impossible today, and the symptom would be an intermittent deadlock, not a test failure.
- **T032** — the `ExecutionModeSelector` policy. The SDK's own default selector treats every tool
  as task-capable, so omitting a custom one makes all 39 tools task-eligible and silently
  contradicts FR-013 — the same failure mode the plan originally described for a `TaskSupport`
  property that, per T031's on-disk verification, does not exist in this SDK version.
- **T057** — the fixture corpus. A fixture with any non-determinism makes SC-008 unachievable and
  the failure will look like a heuristics bug.
- **T082** — keeping the long-running tools' longer timeout defaults. Applying the constitution's
  30 seconds uniformly would satisfy the letter of the requirement while breaking the tools it
  most matters for.

---

## Notes

- `[P]` = different files, no dependencies on incomplete tasks.
- `[Story]` maps each task to its user story for traceability.
- Tests are mandatory here and must be seen failing first — constitution Principle III.
- Commit after each task or logical group.
- Every checkpoint is a valid stopping point.
