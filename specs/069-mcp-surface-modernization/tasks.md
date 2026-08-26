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

- [X] T008 [P] [US1] Write failing tests asserting the ReSharper stage sequence (acquiring engine → running inspection → parsing report — corrected from an earlier 5-stage draft during T008 ground-truthing; see progress-contract.md and data-model.md §4) against `RecordingProgressReporter` in `tests/DebugMcp.Tests/Unit/Progress/ReSharperProgressTests.cs`
- [X] T009 [P] [US1] Write failing tests asserting `batch_evaluate` emits one counted "experiment triggered n of m" update per experiment as `BatchRunner.RunAsync`'s `allTriggeredCount` advances (corrected from "one update per expression" — experiments trigger reactively as breakpoints fire, not in an evaluation loop) in `tests/DebugMcp.Tests/Unit/Progress/BatchEvaluateProgressTests.cs`
- [X] T010 [P] [US1] Write failing tests asserting `code_load`'s stage sequence (loading workspace, project *n* of *m* via Roslyn's own `IProgress<ProjectLoadProgress>` — `locating MSBuild` dropped, it runs once in `CodeAnalysisService`'s static constructor, not inside `LoadAsync`) and `debug_launch`'s (starting process → ready, corrected from an earlier 4-stage draft — `attaching`/`resolving symbols` live inside the ICorDebug-callback boundary R7 protects) in `tests/DebugMcp.Tests/Unit/Progress/WorkspaceAndLaunchProgressTests.cs`
- [X] T011 [P] [US1] Write failing tests asserting a tool with no progress reporter attached completes normally and emits nothing in `tests/DebugMcp.Tests/Unit/Progress/ProgressDegradationTests.cs`
- [X] T012 [P] [US1] Write failing cancellation tests asserting the debug session remains usable after a cancelled call in `tests/DebugMcp.Tests/Unit/Cancellation/SessionConsistencyAfterCancelTests.cs`
- [X] T013 [P] [US1] Write a failing contract test asserting every one of the 39 tool methods returns `Task<...>` and accepts a `CancellationToken` in `tests/DebugMcp.Tests/Contract/ToolAsyncContractTests.cs`

### Implementation for User Story 1

- [X] T014 [P] [US1] Convert `DebugMcp/Tools/VariablesGetTool.cs` and `DebugMcp/Tools/StacktraceGetTool.cs` from `public string` to `public Task<string>` and thread `CancellationToken` (renamed `GetVariables`→`GetVariablesAsync`, `GetStackTrace`→`GetStackTraceAsync` per project convention; work is synchronous in-memory, so `Task.FromResult` per dotnet-csharp.md rather than `async`/`await` with nothing to await)
- [X] T015 [P] [US1] Convert `DebugMcp/Tools/SnapshotCreateTool.cs`, `SnapshotDeleteTool.cs` and `SnapshotDiffTool.cs` to async with `CancellationToken` (renamed `CreateSnapshot`→`CreateSnapshotAsync` etc.; updated 13 call sites across 3 tool test files)
- [X] T016 [P] [US1] Convert `DebugMcp/Tools/TimelineQueryTool.cs`, `ProcessReadOutputTool.cs` and `ProcessWriteInputTool.cs` to async with `CancellationToken` (renamed `TimelineQuery`→`TimelineQueryAsync`, `ReadOutput`→`ReadOutputAsync`, `WriteInput`→`WriteInputAsync`; no test call sites existed for these 3)
- [X] T017 [US1] Add `CancellationToken` parameters to the tool files in `DebugMcp/Tools/` that lack one. A substring `grep -L "CancellationToken"` ground-truthed 8 files (`DebugPauseTool`, `ReferencesGetTool`, `ObjectInspectTool`, `TypesGetTool`, `MemoryReadTool`, `ModulesSearchTool`, `LayoutGetTool`, `MembersGetTool`) and all 8 were fixed — but the grep gave a **false negative** for 4 more: `DebugContinueTool`, `DebugStepTool`, `EvaluateTool`, `EvaluateSafeTool` each already contained the substring "CancellationToken" (inside a locally-constructed `CancellationTokenSource(timeout)` with no way for a caller to pass an external token) without accepting one as a parameter. Caught by re-running `ToolAsyncContractTests.AllTools_AcceptCancellationToken` (T013) after the first 8 and finding `debug_continue` still missing — a live reflection-based check over `McpToolDiscovery.GetAllToolMethods()` found the true remaining 4. Fixed by adding the parameter and linking it with the existing timeout-derived `CancellationTokenSource` via `CancellationTokenSource.CreateLinkedTokenSource`, matching `DebugLaunchTool`'s existing pattern — 12 files total, not 8
- [X] T018 [US1] Audit every conversion from T014–T017 against the rule in [research.md](./research.md) R7 — **no `await` may span a region holding `_lock` or `_stateLock`** — and record the audit result per file in the task's commit message. **Result: clean, by construction.** `grep -l "lock (" DebugMcp/Tools/*.cs` returns zero matches — `_lock`/`_stateLock` are private fields on `DebugSessionManager`/`ProcessDebugger` only, never held by a Tools class. Every T014–T016 conversion either (a) wraps already-synchronous work in `Task.FromResult`, introducing no `await` at all, or (b) threads a `CancellationToken` into an `await` call that already existed pre-conversion, outside any lock. T017's four false-negative fixes (`DebugContinueTool`, `DebugStepTool`, `EvaluateTool`, `EvaluateSafeTool`) link the new token with an existing timeout-derived `CancellationTokenSource` around an existing `await`, again outside any lock. The real R7 risk is deferred to T019–T024 and T021 specifically, where progress/heartbeat logic is added *inside* `DebugSessionManager.LaunchAsync` — audited separately there
- [X] T019 [P] [US1] Thread `IProgressReporter` into `IReSharperInspectionService.InspectAsync` (via `ReSharperInspectSolutionTool.cs` and `ReSharperInspectProjectTool.cs`), emitting the three stages from [contracts/progress-contract.md](./contracts/progress-contract.md) — `acquiring engine` around `IReSharperEngineProvider.EnsureEngineAsync`, `running inspection` around `IReSharperRunner.RunInspectCodeAsync`, `parsing report` around `IInspectionReportParser.Parse`. Each of the first two also needs T023's heartbeat wrapper
- [X] T020 [P] [US1] Add counted progress (`experiment triggered n of m`) to `DebugMcp/Services/Batch/BatchRunner.cs`, at both sites `allTriggeredCount` increments (line ~135 error path, ~156 success path)
- [X] T021 [P] [US1] Add progress to `DebugSessionManager.LaunchAsync` (starting process → ready), wrapping its own `await _processDebugger.LaunchAsync(...)` call — safe because that await holds no lock. **Do not** instrument `ProcessDebugger.LaunchAsync` itself for finer-grained stages; that call runs partly on the ICorDebug callback thread R7 protects. Wrap the call with T023's heartbeat instead of adding real sub-stages
- [X] T022 [P] [US1] Add progress to `CodeAnalysisService.LoadAsync` (loading workspace, project n of ? — no `locating MSBuild` stage; that runs once in the static constructor, not per call) using the real `IProgress<ProjectLoadProgress>` parameter on `MSBuildWorkspace.OpenSolutionAsync`/`OpenProjectAsync`, deduped by `FilePath`. **Total stays null, not pre-counted**: unlike ReSharper's `.sln`-only project-line scan, `code_load` also accepts a bare `.csproj`, whose transitive `ProjectReference` graph is not knowable without duplicating MSBuild's own evaluation — verified via a real multi-project load (`tests/TestTargetApp/TestTargetApp.csproj`, no fake/mocked workspace exists to substitute)
- [X] T023 [US1] Implement a generic heartbeat wrapper in `DebugMcp/Services/Progress/` — races an arbitrary `Task` against a timer that re-emits the *current* stage (unchanged) every ~45s without advancing `Completed`, satisfying SC-001's 60s ceiling for any opaque long call. Used by T019 (engine acquisition, inspection run) and T021 (the launch wait) — the three stages that cannot report real sub-progress
- [X] T024 [US1] Create an adapter in `DebugMcp/Services/Progress/ProgressReporterAdapter.cs` that wraps the SDK-bound `IProgress<ProgressNotificationValue>` **method parameter** into `IProgressReporter`. Deliberately **no** DI registration keyed on progress tokens: whether a token was supplied is per-request information the container cannot see, and the SDK already discards reports when there is none — degradation is structural, not conditional ([research.md](./research.md) R2). Wired into all 5 qualifying tool methods (`ReSharperInspectSolutionTool`, `ReSharperInspectProjectTool` via `ReSharperToolHelper`, `BatchEvaluateTool`, `DebugLaunchTool`, `CodeLoadTool`) — each gains an `IProgress<ProgressNotificationValue>? progress = null` SDK-bound parameter, adapted via `ProgressReporterAdapter.Create(progress)` and passed to the already-wired service call. Also added `SynchronousProgress<T>` (`DebugMcp/Services/Progress/SynchronousProgress.cs`) — the BCL's `System.Progress<T>` dispatches via the captured `SynchronizationContext` (falling back to the thread pool), so its callback can still be pending when the awaited call returns; `CodeAnalysisService.LoadAsync`'s `IProgress<ProjectLoadProgress>` needed synchronous, deterministic delivery instead

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

> **Corrected after empirical verification (advisor-directed) against SDK 2.2.0**: a prior
> assumption — that the SDK's tool-call wrapper drives an outstanding task to `failed` when the
> wrapped call throws — is **false**. Verified with a real client+server pair connected over an
> in-process duplex transport (`ModelContextProtocol.Server.StreamServerTransport` /
> `ModelContextProtocol.Protocol.StreamClientTransport` over paired `System.IO.Pipelines.Pipe`s —
> see `tests/DebugMcp.Tests/Support/InProcessMcpHarness.cs`): an **uncaught exception is caught by
> the SDK itself** and turned into a **Completed** task whose result carries `isError:true` and a
> generic message — never `Failed`. Since all five FR-013 qualifying tools already catch every
> exception internally and return a structured `{success:false,error:{...}}` JSON string, `Failed`
> is not a status they can organically reach either way — the correct, already-satisfied contract
> is that a deferred call's terminal Result carries the exact same JSON shape the synchronous call
> would have. The harness also confirmed, empirically: opt-in gating is enforced entirely by the
> SDK (a client that never declared `io.modelcontextprotocol/tasks` always gets a direct result,
> regardless of `ExecutionModeSelector`); `tasks/cancel` propagates automatically to the tool's own
> `CancellationToken` (T014–T018's Phase 3 plumbing is what makes this work, for free); raw
> `InMemoryMcpTaskStore.GetTaskAsync` returns the identical `null` for both an expired and a
> never-created id (confirming the FR-012 decorator below is required); and `IMcpTaskStore` has no
> method to update `StatusMessage` mid-flight, nor does `RequestContext.Items` expose an ambient
> task id a tool could use with `SendTaskStatusNotificationAsync` — so bridging progress into the
> polled `statusMessage` field is **not achievable** with this SDK's public surface (T036,
> corrected below). These findings are why T025–T030, T036, T037 and T038 read differently below
> than in the original plan; T031/T032 are unaffected in substance.

- [X] T025 [P] [US2] Covered by `OptedInClient_QualifyingTool_IsDeferredAsTask` and `ClientWithoutCapability_QualifyingTool_ReceivesDirectResultNeverATask` in `tests/DebugMcp.Tests/Unit/Tasks/McpTasksHarnessTests.cs` — a real client that never declares the tasks extension never receives `resultType: "task"`, verified over the in-process harness rather than assumed
- [X] T026 [P] [US2] Covered by `DeferredResult_MatchesTheDirectSynchronousResult_ByteForByte` in `McpTasksHarnessTests.cs` — the deferred task's stored `content[0].text` is compared directly against the synchronous call's `content[0].text` for identical input
- [X] T027 [P] [US2] Covered by `OptedInClient_QualifyingTool_IsDeferredAsTask` (Working immediately after creation) and `DeferredResult_MatchesTheDirectSynchronousResult_ByteForByte` (Working → Completed, terminal Result correct) in `McpTasksHarnessTests.cs`. `Failed` is exercised structurally, not as a tool outcome — see the note above and T030
- [X] T028 [P] [US2] Covered by `ExpiredTaskId_ThrowsADifferentErrorThanAnUnknownTaskId` in `McpTasksHarnessTests.cs` — asserts the two `McpProtocolException` messages differ; the raw SDK store was confirmed to make them identical, which is exactly what `ExpiryAwareTaskStore` (T031) fixes
- [X] T029 [P] [US2] Renamed from the abandoned `TaskSupport = Forbidden` framing (no such property exists — see the correction on T031/T032 below) to `tests/DebugMcp.Tests/Unit/Tasks/TaskExecutionPolicyTests.cs`: asserts `TaskExecutionPolicy.QualifyingTools` is exactly the FR-013 five, and that every one of the 39 registered tools (via `McpToolDiscovery`) classifies consistently
- [X] T030 [P] [US2] Renamed in scope: `DomainFailure_SurvivesDeferral_WithTheSameStructuredJsonContract` and `UncaughtException_CompletesTheTaskWithIsError_NeverFailed` in `McpTasksHarnessTests.cs` prove a failing underlying operation (simulating debuggee termination or any other mid-call failure) reaches the client as a **Completed** task carrying the tool's own `{success:false,...}` JSON — never `Failed`, never stuck `Working`. Client-disconnect has no separate server-side path to test: the SDK's task record simply outlives the disconnected connection, which is exactly what "deferred" means

### Implementation for User Story 2

- [X] T031 [US2] Registered `InMemoryMcpTaskStore` (wrapped — see below) via `builder.WithTasks(store, opts => ...)` in `DebugMcp/Program.cs` — confirmed against the installed 2.2.0 assemblies that this is the only wiring path; `AddMcpServer(options => options.TaskStore = ...)` and a per-tool `Execution.TaskSupport` property do **not** exist in this SDK version ([research.md](./research.md) R1, corrected). Confirmed empirically that raw `InMemoryMcpTaskStore.GetTaskAsync` returns identical `null` for an expired and an unknown id, so it is wrapped in `DebugMcp/Services/Tasks/ExpiryAwareTaskStore.cs`, which remembers each task's expiry instant and throws a distinctly-worded `McpTaskExpiredException` once elapsed (surfaces to the client as a differently-worded `McpProtocolException`, satisfying FR-012's distinguishability requirement). `DefaultTimeToLive` set to 1 hour (long enough to outlive the slowest qualifying operation); `DefaultPollIntervalMs` left at the SDK's own default (1000 ms, confirmed via the harness) rather than the originally proposed 2 s — no evidence justified overriding an already-reasonable SDK default
- [X] T032 [US2] Created `TaskExecutionPolicy` in `DebugMcp/Services/Tasks/TaskExecutionPolicy.cs` holding the five qualifying tool names (FR-013) → `McpTaskExecutionMode.Optional`, everything else → `Synchronous`, wired as `McpTasksOptions.ExecutionModeSelector` in T031's registration — **the SDK's documented default selector treats every tool as task-capable, so omitting this would have silently made all 39 tools task-eligible**. There is no per-tool setting to "pin"; the policy is the single source of truth T029's test asserts against. `GetMode(string?)` factored out as the pure classification function so tests don't need to construct an SDK `RequestContext`
- [X] T033 [US2] *(merged into T032 — the qualifying-tool table lives in one file, not scattered per-tool settings; kept as a no-op marker so task numbering stays stable)*
- [X] T034 [US2] *(merged into T032 — see T033)*
- [X] T035 [US2] Documented in `DebugMcp/Tools/DebugLaunchTool.cs`'s `[Description]` that an opted-in client may receive a handle even for a fast launch — the qualification itself is registered in T032's policy table, not on this file
- [X] T036 [US2] **Corrected, not implemented as originally scoped.** Empirically confirmed (see the note above `tests/DebugMcp.Tests/Unit/Tasks/McpTasksHarnessTests.cs`'s `Progress_ReportedDuringADeferredCall_DoesNotAppearInThePolledStatusMessage`) that bridging `IProgressReporter` stage updates into the polled task `statusMessage` is not achievable with SDK 2.2.0's public surface: `IMcpTaskStore` has no incremental status-message update method, and a running tool method has no way to discover its own task id (`RequestContext.Items` is empty) to use with `McpTasksServerExtensions.SendTaskStatusNotificationAsync`. No code added; documented here and in `research.md` as an SDK limitation, guarded by a regression test that will start failing (prompting a revisit) if a future SDK version adds the missing hook
- [X] T037 [US2] **Verified, not implemented — no new code needed.** `TasksCancel_PropagatesToTheToolsCancellationToken` in `McpTasksHarnessTests.cs` confirms `tasks/cancel` automatically cancels the `CancellationToken` the SDK passed into the tool method. This is a direct, free consequence of Phase 3's T014–T018 work (every tool now genuinely accepts and honours a `CancellationToken`) — there is no separate cancellation mechanism to wire for MCP Tasks specifically
- [X] T038 [US2] **Corrected and merged into T030.** "Drive an outstanding handle to `failed`" was based on the same wrong assumption corrected above: our tools already catch every failure (including a debuggee terminating mid-launch) into structured `{success:false,error:{...}}` JSON before returning, so the SDK sees a normal completion and marks the task `Completed` — exactly the contract `DomainFailure_SurvivesDeferral_WithTheSameStructuredJsonContract` and `UncaughtException_CompletesTheTaskWithIsError_NeverFailed` verify. No changes were needed in `DebugSessionManager.cs`

**Checkpoint**: US2 complete. Opted-in clients get handles; everyone else is unaffected.

---

## Phase 5: User Story 3 — Every tool publishes a checkable result contract (Priority: P3)

**Goal**: All 39 tools return typed results with a published `outputSchema`, retain the text
block for backward compatibility, and fail in one shared shape.

**Independent Test**: Assert every tool in `tools/list` carries an `outputSchema`; assert each
tool's `structuredContent` validates against it; assert a client reading only `content[0].text`
works unchanged.

> **Pilot completed before fan-out (advisor-directed), `snapshot_delete` migrated for real.**
> Two mechanism unknowns were resolved empirically (real client+server over
> `InProcessMcpHarness`, plus `McpServerTool.Create` for schema-only reflection) before touching
> the other 38 tools:
> - `[McpServerTool(UseStructuredContent = true)]` + a method returning `Task<TFlatRecord>`
>   directly is sufficient — the SDK derives **both** `outputSchema` and `structuredContent`
>   (and `content[0].text`, byte-identical to `structuredContent`, compact not indented) from the
>   C# return type by reflection. **`ToolResult<T>` (T004) is not that return type** — its `Data`
>   would serialize nested under a `"data"` key, contradicting the flat wire shape
>   `contracts/tool-result-contract.md` specifies. Each tool instead gets its **own flat record**
>   (`Success`, its domain fields, `Error`), reusing the shared `ToolError`/`TruncationInfo`
>   types. `ToolResult<T>` remains as a validation helper only — see data-model.md §1's
>   correction.
> - **Requiredness pitfall**: a record parameter without `= default` becomes schema-`required`
>   regardless of C# nullability. Every field except `Success` **MUST** declare a default, or a
>   failure result (which omits every domain field) fails its own schema. Caught by T040 on the
>   pilot; `contracts/tool-result-contract.md`'s `required` example is corrected.
> - Default naming policy is **camelCase** (`RemainingCount` → `remainingCount`), matching most
>   existing tools already. Tools with legacy **snake_case** fields (`batch_evaluate` above all)
>   need explicit `[JsonPropertyName("...")]` per field — camelCase will not reproduce them.
> - `isError` is **not** inferred automatically from any field — confirmed both flags stayed
>   unset on the pilot until a central `AddCallToolFilter` (T053, implemented alongside the
>   pilot as `DebugMcp/Models/Results/ToolResultSerializer.IsErrorFilter`, wired in `Program.cs`)
>   reads `StructuredContent.success` after every call and sets it. Verified this composes
>   correctly with MCP Tasks deferral (T031/T032): a deferred domain failure's stored task
>   `Result` carries `isError:true` too.
>
> T044–T052 prompts must carry this pattern explicitly, plus: enum-ish fields stay lowercase
> `string` exactly as today, never a C# enum, in the wire record; nullable-with-default
> reproduces today's conditional-key-omission (confirmed — the SDK omits `null` properties);
> keep existing `CancellationToken`/`IProgress`/logging/try-catch, with catch blocks now
> constructing the typed failure record instead of a JSON string; convert that tool's existing
> unit tests off string-parsing; and add one case per tool to `LegacyTextContractTests.cs`
> alongside the migration, following the `snapshot_delete` cases already there as the worked
> example (so T050's group only has `SnapshotCreateTool`/`SnapshotDiffTool` left).

### Tests for User Story 3 ⚠️ Write first, watch them fail

- [X] T039 [P] [US3] Written in `tests/DebugMcp.Tests/Contract/OutputSchemaPresenceTests.cs` — reflection-only (no DI), asserts every tool's attribute has `UseStructuredContent = true` and `McpServerTool.Create` produces a non-null `OutputSchema`. RED for every unmigrated tool (all `UseStructuredContent = false` today), GREEN for `snapshot_delete` after the pilot
- [X] T040 [P] [US3] Written in `tests/DebugMcp.Tests/Contract/OutputSchemaConformanceTests.cs`, using the T007 `SchemaValidator` plus the new `ToolResultShape` reflection helper (`tests/DebugMcp.Tests/Support/ToolResultShape.cs`) to construct a representative "success" and "failure" instance of each tool's result record and validate both against the tool's own schema — this is what caught the requiredness pitfall above
- [X] T041 [P] [US3] Scoped structurally (a live 39-tool invocation isn't practical pre-migration): `tests/DebugMcp.Tests/Contract/ErrorShapeContractTests.cs` asserts every migrated tool's result type exposes an `Error` property of exactly the shared `ToolError` type — "one shape for all 39 tools" by construction. `code ∈ ErrorCodes` remains enforced the existing way, at each `ErrorCodes.*` call site
- [X] T042 [P] [US3] Written in `tests/DebugMcp.Tests/Contract/LegacyTextContractTests.cs` as a characterization test (passes today, must keep passing through the refactor — not a RED-first feature test): calls the real tool, serializes its result the way the SDK does, and asserts on specific known field names/values. Seeded with the `snapshot_delete` pilot's two cases; each T044–T052 group adds its own tool's case here
- [X] T043 [P] [US3] Written in `tests/DebugMcp.Tests/Contract/ToolDocCoverageTests.cs` — scans `### tool_name` headings across `website/docs/tools/*.md` (excluding `index.md`) and diffs both directions against `McpToolDiscovery`. RED today (`batch_evaluate` and others undocumented — pre-existing gap, not something this migration introduces) until T055

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
- [X] T050 [P] [US3] **`SnapshotDeleteTool` migrated as the pilot** (`DebugMcp/Models/Results/SnapshotDeleteResult.cs`). Remaining: `SnapshotCreateTool`, `SnapshotDiffTool` in `DebugMcp/Tools/`
- [ ] T051 [P] [US3] Migrate the 2 process-I/O tools (`ProcessReadOutputTool`, `ProcessWriteInputTool`) in `DebugMcp/Tools/`
- [ ] T052 [P] [US3] Migrate `MemoryReadTool`, `ModulesSearchTool`, `BatchEvaluateTool` and `TimelineQueryTool` in `DebugMcp/Tools/`
- [X] T053 [US3] Central `isError` mechanism implemented alongside the pilot: `DebugMcp/Models/Results/ToolResultSerializer.IsErrorFilter`, an `AddCallToolFilter` wired once in `Program.cs`, reading `StructuredContent.success` after every call. The FR-017 text-block guarantee needed no extra code — confirmed the SDK always mirrors `structuredContent` into `content[0].text` when `UseStructuredContent = true`
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
  and be stopped. US1's `CancellationToken` plumbing (T014–T018) is what makes US2's `tasks/cancel`
  work at all (T037); without US1 there is nothing for the SDK's cancellation to propagate into.
  (T036's originally-planned progress→statusMessage bridge turned out not to be achievable with
  this SDK version — see T036's correction note — so that particular link no longer applies, but
  the cancellation dependency still holds.)
- **US3 → US4**: enrichment adds fields. Adding them to typed records is one change; adding them
  to hand-built JSON strings and then migrating means doing it twice.
- **US3 → US5**: US5's timeout errors use the shared error shape built in US3. Shipping US5 first
  would mean writing that error twice.
- **US1/US2 ↔ US3/US4**: independent of each other. A team could run the async track (US1→US2) and
  the contracts track (US3→US4) in parallel, with one merge point — T070 extends records created
  in T044–T052, and T032's `TaskExecutionPolicy` classifies tool *names* that T044–T052 also edit
  (a classification table keyed by name, not a per-tool property — see T032's correction note).

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
