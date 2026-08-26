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

- [X] T044 [P] [US3] Migrate the 6 session/execution tools (`DebugAttachTool`, `DebugLaunchTool`, `DebugDisconnectTool`, `DebugContinueTool`, `DebugPauseTool`, `DebugStepTool`) in `DebugMcp/Tools/`. No wire deviations
- [X] T045 [P] [US3] Migrate the 5 breakpoint tools (`BreakpointSetTool`, `BreakpointRemoveTool`, `BreakpointEnableTool`, `BreakpointSetExceptionTool`, `TracepointSetTool`) in `DebugMcp/Tools/`. Nested payloads reuse domain types (`ExceptionBreakpoint`) directly — accepted coupling, no enum fields on the reused type so no serialization delta; legacy `"details": null` now omitted entirely (consistent with the documented ignore-condition policy)
- [X] T046 [P] [US3] Migrate 6 inspection tools (`VariablesGetTool`, `StacktraceGetTool`, `EvaluateTool`, `EvaluateSafeTool`, `ObjectInspectTool`, `ObjectSummarizeTool`) in `DebugMcp/Tools/`. **Accepted deviation**: `evaluate`/`evaluate_safe` error's `position`/`exception_type` moved from siblings of `code`/`message` into `error.details.position`/`error.details.exception_type` — the shared `ToolError(Code, Message, Details)` shape (required by FR-018) has no dedicated fields for them, and the data survives losslessly under `details`. Documented in data-model.md §1
- [X] T047 [P] [US3] Migrate 6 further inspection tools (`CollectionAnalyzeTool`, `ExceptionGetContextTool`, `LayoutGetTool`, `MembersGetTool`, `ReferencesGetTool`, `TypesGetTool`) in `DebugMcp/Tools/`. `LayoutFieldInfo` is a bespoke wire record (not the domain `LayoutField`) specifically to avoid that model's `JsonIgnoreCondition.WhenWritingDefault` silently dropping `alignment` when 0 — legacy JSON always included it
- [X] T048 [P] [US3] Migrate the 5 code-analysis tools (`CodeLoadTool`, `CodeGoToDefinitionTool`, `CodeFindUsagesTool`, `CodeFindAssignmentsTool`, `CodeGetDiagnosticsTool`) in `DebugMcp/Tools/`. **Accepted deviation**: these reuse shared `DebugMcp.Models.CodeAnalysis.*` domain types directly, so enum properties (`WorkspaceInfo.Type`, `SymbolUsage.Kind`, `SymbolAssignment.Kind`, `DiagnosticInfo.Severity`) now serialize as SDK-default strings instead of legacy raw integers. Policy (documented in data-model.md §1): bespoke wire records preserve the legacy representation exactly (see T052's `eventType`, kept as raw int); reused domain types follow the SDK serializer, and any resulting delta is accepted per-field rather than adding converters to shared models that resources also use
- [X] T049 [P] [US3] Migrate the 2 ReSharper tools (`ReSharperInspectSolutionTool`, `ReSharperInspectProjectTool`) in `DebugMcp/Tools/`. Reuses `DebugMcp.Models.ReSharper.InspectionResult` as-is; T032's progress/task-deferral wiring through `ReSharperToolHelper` re-verified working post-migration
- [X] T050 [P] [US3] **`SnapshotDeleteTool` migrated as the pilot** (`DebugMcp/Models/Results/SnapshotDeleteResult.cs`). `SnapshotCreateTool`, `SnapshotDiffTool` migrated in the fan-out. No wire deviations
- [X] T051 [P] [US3] Migrate the 2 process-I/O tools (`ProcessReadOutputTool`, `ProcessWriteInputTool`) in `DebugMcp/Tools/`. No wire deviations
- [X] T052 [P] [US3] Migrate `MemoryReadTool`, `ModulesSearchTool`, `BatchEvaluateTool` and `TimelineQueryTool` in `DebugMcp/Tools/`. `batch_evaluate`'s five lowercase custom codes (`validation_error`, `batch_already_running`, `invalid_json`, `cancelled`, `internal_error`) added to `ErrorCodes` as-is (case preserved) to satisfy FR-019 without a wire change — no deviation remains. `timeline_query` faithfully reproduces two pre-existing bugs (`eventType` as raw int, `payload` always `{}`) — not introduced by this migration, out of scope to fix here. `modules_search` gained an unpopulated `Truncation` field for T054
- [X] T053 [US3] Central `isError` mechanism implemented alongside the pilot: `DebugMcp/Models/Results/ToolResultSerializer.IsErrorFilter`, an `AddCallToolFilter` wired once in `Program.cs`, reading `StructuredContent.success` after every call. The FR-017 text-block guarantee needed no extra code — confirmed the SDK always mirrors `structuredContent` into `content[0].text` when `UseStructuredContent = true`
- [X] T054 [US3] **Corrected premise**: the 256 KB budget cannot live in `ToolResultSerializer.cs` — that class only sets `isError` from an already-serialized `structuredContent`; it never sees the pre-serialization domain object, so it cannot trim meaningfully. Implemented instead as `DebugMcp/Models/Results/ResultTruncation.Bound<T>()`, a shared helper (binary-search on serialized byte size, `DefaultBudgetBytes = 256 * 1024`) called from inside each of the 14 FR-035 tools, on the specific field that is its unbounded collection, before the result record is constructed: `VariablesGetTool` (Variables), `TypesGetTool` (Types), `MembersGetTool` (Members), `ReferencesGetTool` (References), `StacktraceGetTool` (Frames), `TimelineQueryTool` (Events), `ModulesSearchTool` (Modules — field already existed unpopulated since T052), `ObjectInspectTool` (Fields/Elements), `CollectionAnalyzeTool` (Items), `CodeFindUsagesTool` (Usages), `CodeFindAssignmentsTool` (Assignments), `CodeGetDiagnosticsTool` (Diagnostics), `ReSharperInspectSolutionTool`/`ReSharperInspectProjectTool` (Findings). Every other tool is untouched — no `Truncation` field, no trimming. Unit tests in `tests/DebugMcp.Tests/Unit/Results/ResultTruncationTests.cs`
- [X] T055 [US3] Updated `website/docs/tools/*.md` so every tool name is present and none is stale, making T043 pass. Added `snapshots.md`, `batch-evaluate.md`, `timeline.md` (new pages, registered in `website/sidebars.ts`); added `evaluate_safe`/`object_summarize`/`collection_analyze` to `inspection.md`; added proper `###` headings for `resharper_inspect_solution`/`resharper_inspect_project` to the existing `resharper.md` prose. Rewrote (not just deleted) the 5 stale entries — `breakpoint_list`, `breakpoint_wait`, `debug_state`, `modules_list`, `threads_list` (removed by feature 030, docs never updated) — into prose pointing at their actual replacement (`debugger://*` MCP resources / `debugger/sessionStateChanged` and `debugger/breakpointHit` notifications), including the cross-referencing prose section titles and index.md's tables. `index.md` recomputed to 39 tools / 12 categories and gained a resource-replacement table
- [X] T056 [US3] **Already satisfied by construction** — checked `.github/workflows/ci.yml`: every push/PR (3-OS matrix) runs `dotnet test -c Release --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"`, which includes all four T039–T043 tests living under `tests/DebugMcp.Tests/Contract/`. Condition 1 → `OutputSchemaPresenceTests`, condition 2 → `OutputSchemaConformanceTests`, conditions 3 and 4 → `ToolDocCoverageTests`'s two assertions. No new build wiring needed; a regression on any of the four conditions already fails CI red

**Checkpoint**: US3 complete. 39 of 39 tools typed, schema-published, backward compatible.

---

## Phase 6: User Story 4 — Diagnostic results arrive pre-ranked (Priority: P4)

**Goal**: The four analysis tools return a deterministic ranking of candidate frames with
concrete evidence. No language model anywhere.

**Independent Test**: Run each fixture in the corpus and assert the human-identified fault frame
ranks first in at least 8 of 10; run one fixture 10 times and assert identical normalized
enrichment output.

### Corpus first — nothing else in this phase is measurable without it

- [X] T057 [US4] Created 10 deterministic fault fixtures in `tests/DebugTestApp/FaultScenarios/` (small C# programs — documentation/traceability of what each scenario models, per data-model.md's "what determinism means here": ranking is unit-tested against hand-built `AutopsyFrame` data mirroring these, not a live replay). Covers all 5 mandatory categories (FR-030) plus 5 more, one per remaining heuristic/edge case
- [X] T058 [US4] Recorded in `tests/DebugTestApp/FaultScenarios/expected-answers.json`; the matching hand-built frame data used by the Unit tests lives in `tests/DebugMcp.Tests/Unit/Enrichment/FaultCorpusFixtures.cs` and must stay consistent with it

### Tests for User Story 4 ⚠️ Write first, watch them fail

- [X] T059 [P] [US4] `tests/DebugMcp.Tests/Unit/Enrichment/RankingAccuracyTests.cs` — asserts ≥8/10; `NoSymbolsAvailable`'s correct `RankingUnavailable` doesn't count toward the tally (there's no rank to compare), so this is really ≥8 of the other 9. RED confirmed against the `NotImplementedException` stub before T065
- [X] T060 [P] [US4] `tests/DebugMcp.Tests/Unit/Enrichment/DeterminismTests.cs` — replays `MultipleNullCandidates` 10 times, compares a normalized string (FrameIndex:Score:[Heuristic=Weight:Evidence]) per data-model.md §5's determinism scope
- [X] T061 [P] [US4] `tests/DebugMcp.Tests/Unit/Enrichment/AdditiveOnlyTests.cs` — two angles: `RankedSuspect` may only ever *reference* a frame by index (reflection-asserts its only properties are FrameIndex/Score/Reasons, never a duplicate of frame contents), and `Rank()` never mutates its input frames. Wire-level additivity for the two affected tools is covered by `LegacyTextContractTests` (T070)
- [X] T062 [P] [US4] `tests/DebugMcp.Tests/Unit/Enrichment/RankingUnavailableTests.cs`
- [X] T063 [P] [US4] `tests/DebugMcp.Tests/Unit/Enrichment/Heuristics/` — 5 files, one per heuristic, each asserting fire-on-own-fixture and absent-on-an-unrelated-fixture

### Implementation for User Story 4

- [X] T064 [P] [US4] `DebugMcp/Models/Inspection/RankedSuspect.cs` — `RankedSuspect`, `SuspicionReason`, `RankingUnavailable`, plus `EnrichmentOutcome` (paired-nullable Ranking/Unavailable, mirroring the codebase's existing success/error convention rather than a union type) as the ranker's return shape
- [X] T065 [US4] `DebugMcp/Services/Inspection/SuspicionRanker.cs` + `SuspicionHeuristics.cs` (names/weights as documented constants). Five heuristics: `NullValuedLocal` (+0.5), `ExternalFrameNoSymbols` (-1.0), `InnermostUserFrame` (+0.2), `ExceptionMessageReferencesVariable` (+0.4), `EmptyCollectionArgument` (+0.5). Frames iterated `OrderBy(f => f.Index)` (no hash-order), ties broken `FrameIndex` ascending, no wall-clock/random. All-external or zero-evidence frames yield `RankingUnavailable`. 9/10 corpus fixtures correct on first implementation (verified arithmetically against the weights before running, then confirmed by the test run)
- [X] T066 [US4] `docs/enrichment-heuristics.md` — every heuristic, its weight and rationale, the corpus/accuracy result, and the frame-bearing-only scope note (T069)
- [X] T067 [US4] `ISuspicionRanker` injected into `ExceptionAutopsyService`; `ExceptionAutopsyResult` gained an additive `Enrichment: EnrichmentOutcome?` field; `ExceptionGetContextTool` maps it to new `ranking`/`rankingUnavailable` wire fields on `ExceptionGetContextResult`. Registered in `Program.cs` DI
- [X] T068 [P] [US4] `StacktraceGetTool` takes the same `ISuspicionRanker`, maps its own `StackFrame` list to `AutopsyFrame` and calls `Rank(frames, exception: null)` — only the exception-independent heuristics can fire, as intended (no `LastExceptionInfo` wiring — that's `exception_get_context`'s job, kept out of `stacktrace_get` to avoid gold-plating). New `ranking`/`ranking_unavailable` fields on `StacktraceGetResult` follow that file's existing snake_case `[JsonPropertyName]` convention (nested `RankedSuspect`/`SuspicionReason` properties themselves stay camelCase — reused-domain-type policy from §1)
- [X] T069 [US4] **Descoped — planning-time premise error, same treatment as T054.** `RankedSuspect.FrameIndex` (data-model.md §5) "references a frame already present in the raw result"; neither `CollectionAnalyzeResult` nor `ObjectSummarizeResult` has frames — they summarize a single object/collection. Applying the frame-shaped model here is unsatisfiable, not merely awkward. FR-027 also requires every heuristic be "individually testable against recorded scenarios" — the FR-030 corpus records fault *frames*, so element-level heuristics would be born non-compliant with no corpus to test against. Element-level suspicion scoring (null candidates, structured hypotheses with confidence scores) already exists as its own open roadmap proposal — [#045 Anomaly Detection](../../ROADMAP.md) — which FR-029 deliberately left unabsorbed (only #061/#062/#066 and "the enrichment half of #037" were absorbed). Implementing it here would smuggle in that proposal under a different number. data-model.md §5 gained a one-line note scoping enrichment to frame-bearing results
- [X] T070 [US4] `ExceptionGetContextResult` and `StacktraceGetResult` extended (T067/T068) — both `= null` defaults, so `OutputSchemaConformanceTests` stayed green with no changes needed. Added a ranking-present case to `StacktraceGet_Success_PreservesLegacyFieldNames` and a ranking-absent case to `ExceptionGetContext_Success_PreservesLegacyFieldNames` in `LegacyTextContractTests.cs`, plus a real-ranker assertion in `ExceptionAutopsyServiceTests.cs`
- [X] T071 [US4] Measured in `docs/enrichment-heuristics.md`'s "Token cost (SC-006)" section: round-trip count (the more faithful proxy — response bytes alone exclude the reasoning/deliberation tokens each eliminated round trip actually saves) drops from 6 to 1 (83% reduction) on a representative deep-fault scenario, clearing the 50% bar; raw response bytes alone only reach 10%, reported transparently as the more conservative, incomplete number rather than omitted

**Checkpoint**: US1–US4 functional. Only the timeout slice remains.

---

## Phase 7: User Story 5 — Every blocking operation can be bounded by a timeout (Priority: P5)

**Goal**: Every blocking tool accepts an optional timeout with a documented default, closing the
constitution's tool-standards requirement that ~27 of 40 tool files violate today.

**Independent Test**: Invoke a blocking tool with a deliberately short timeout against work known
to exceed it, and assert a timeout error returns within the budget and the next call succeeds;
invoke it with no timeout and assert the documented default applies.

### Tests for User Story 5 ⚠️ Write first, watch them fail

- [X] T072 [P] [US5] `tests/DebugMcp.Tests/Contract/TimeoutParameterContractTests.cs` — reflection over `TimeoutPolicy.Specs`, both directions (blocking ⟹ param present with matching default + "default" in its description; in-memory ⟹ no param name containing "timeout"), plus a completeness check that every registered tool has a policy entry and vice versa. RED for 30/31 blocking tools at write time (only `batch_evaluate` was already fully compliant)
- [X] T073 [P] [US5] `tests/DebugMcp.Tests/Unit/Timeouts/TimeoutExpiryTests.cs` — pilot against `debug_pause` (a hung mock `PauseAsync` past a 20ms timeout returns `ErrorCodes.Timeout` naming "20" in the message)
- [X] T074 [P] [US5] `tests/DebugMcp.Tests/Unit/Timeouts/TimeoutDefaultsTests.cs` — names the two FR-032 exception classes explicitly (the long-running five keep 300s/30s; `evaluate`/`evaluate_safe`/`object_summarize`/`collection_analyze` keep their pre-existing *shorter* 5000ms default — a case FR-032's letter didn't anticipate either direction, resolved the same way as every other deviation this feature recorded: wire/behavior stability wins, noted here rather than silently) so a regression reads as an intent violation, not just a reflection mismatch
- [X] T075 [P] [US5] `tests/DebugMcp.Tests/Unit/Timeouts/TimeoutConsistencyTests.cs` — same `debug_pause` pilot: after a timeout, a second call still succeeds. Deliberately reuses the exact linked-CTS mechanism already proven consistent for caller-cancellation by `SessionConsistencyAfterCancelTests` (US1) — a timeout is just another source cancelling the same linked token, so no new consistency machinery was needed

### Implementation for User Story 5

- [X] T076 [US5] `DebugMcp/Services/Timeouts/TimeoutPolicy.cs` — all 39 tools classified (31 blocking / 8 in-memory-only), traced against actual service calls, not inferred from tool names (a dedicated research pass over each tool's real call chain — 4 of the "blocking" classifications, the `code_*` Roslyn/MSBuildWorkspace tools, are a judgment call FR-031's text doesn't literally settle; documented in the file's own header). Parameter naming/casing follows each tool's own pre-existing convention (snake_case `timeout_ms` for most families, camelCase `timeout`/`timeoutMs` for session-execution/`code_*`, matching those families' existing params)
- [X] T077 [US5] **Already satisfied** — `ErrorCodes.Timeout = "TIMEOUT"` already existed (used by `debug_launch`, `code_get_diagnostics`, `code_find_usages` already). FR-033's stricter clause (message must *name the elapsed budget*) is a message-content requirement on the new/fixed wiring, not a new code — verified per-tool via `TimeoutExpiryTests`' pilot and the contract test's default-value assertion
- [X] T078 [P] [US5] Pilot batch, done directly to establish the exact pattern before delegating the rest: `DebugPauseTool` (new `timeout` param, linked CTS, timeout catch) and `DebugDisconnectTool` (refactored its existing internal fixed 10s `DisconnectTimeout` constant into the same optional parameter, default 10000 — preserves today's exact behavior for a caller that never supplies one). Plus 10 tools that already had a timeout parameter but not the SC-011-required "default:" text in their `[Description]`: `debug_launch`, `debug_attach`, `debug_continue`, `debug_step`, `evaluate`, `evaluate_safe`, `object_summarize`, `collection_analyze`, `resharper_inspect_solution`, `resharper_inspect_project` — description-only, no behavior change
- [X] T079 [P] [US5] Blocking inspection tools done as part of the T078-established-pattern fan-out (see T081's note — delegated as one batch with T080/T081, not split by the original phase boundary): `exception_get_context`, `object_inspect`. `stacktrace_get`/`variables_get` got special treatment — see T081's note
- [X] T080 [P] [US5] Blocking code-analysis tools, same fan-out: `code_load`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics`, `code_goto_definition` (camelCase `timeoutMs`, matching their own existing camelCase params). ReSharper tools were already compliant (T078)
- [X] T081 [P] [US5] Remaining blocking tools, same fan-out: `breakpoint_set`, `tracepoint_set`, `memory_read`, `layout_get`, `references_get`, `modules_search`, `types_get`, `members_get`, `snapshot_create`. **`stacktrace_get`, `variables_get` and `snapshot_create` needed different treatment**: their underlying calls (`IDebugSessionManager.GetStackFrames`/`GetVariables`, `ISnapshotService.CreateSnapshot`) are genuinely synchronous with no `CancellationToken` at all — nothing exists to race a linked CTS against. Per FR-034 ("where an operation cannot be safely interrupted, the indivisible step completes first"), wrapping a synchronous ICorDebug read in `Task.Run`+`Task.WhenAny` to fake cancellability would abandon a call still touching the live session on a background thread while a different call could start — a real threading violation against this codebase's `_lock`/`_stateLock` invariant (CLAUDE.md). These 3 accept and bounds-validate the parameter (satisfying FR-031's uniform surface) but it is honestly inert today — documented in-code, not silently faked
- [X] T082 [US5] Verified in `TimeoutPolicy.cs` and `TimeoutDefaultsTests`: `resharper_inspect_solution`/`resharper_inspect_project` keep 300s; `batch_evaluate`/`debug_launch` (also in the "long-running five" = `TaskExecutionPolicy.QualifyingTools`) already matched the 30s/30000ms standard, no exception needed for those two
- [X] T083 [US5] Delegated fan-out, independently verified: every blocking tool's `[Description]` attribute documents its timeout parameter's name/unit/default (T078 covered the 10 that already had the parameter but lacked the text; this batch covered the remaining tools wired in T079-T081), and `website/docs/tools/*.md` gained matching Parameters-table rows across all 9 category pages (`session.md`, `breakpoints.md`, `execution.md`, `inspection.md`, `memory.md`, `modules.md`, `code-analysis.md`, `resharper.md`, `snapshots.md`). Confirmed present via `grep -l "default: 30000" website/docs/tools/*.md`

**Checkpoint**: US5 complete. No constitution requirement outstanding.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T084 [P] Fixed `ROADMAP.md`: `Tier 4 — MCP Protocol Evolution` renumbered to `Tier 6` (Tier 5 already existed); `#034 Edit and Continue` renumbered to `#070` (`#034` collided with the shipped ReSharper Inspections entry); `#031`/`#032` proposal entries replaced with a one-line "✅ Shipped in vX — see Completed Features above" pointer instead of deleted (keeps numbering stable), and their Completed-table rows fixed from `TBD` to their real tags (`v0.17.0`/`v0.18.0`, found via `git tag --contains`) (FR-028)
- [X] T085 [P] `ROADMAP.md`: `#061`/`#062`/`#066` marked "✅ Fully absorbed by feature 069" with a one-line pointer to what specifically absorbed each; `#037` marked "🟡 Partially absorbed" — 069's ranking covers the same "pre-digested state" spirit, but `debug_state` no longer exists as a tool (replaced by the `debugger://session` resource in feature 030), so #037's literal `stop_reason`/safe-to-evaluate hints remain open scope on that resource (FR-029)
- [X] T086 [P] Added `069 | MCP Surface Modernization | TBD | ...` to `ROADMAP.md`'s Completed Features table and a full US1-US5 summary to `CLAUDE.md`'s Recent Changes. Also fixed two adjacent stale counts in `CLAUDE.md` noticed while editing: the architecture diagram said "40 tools" / "4 resources" and the project-layout tree said "36 MCP tool classes" — both predate this feature (last touched at 034/030) and were corrected to the actual current 39 tools / 7 resources
- [X] T087 [P] `website/docs/architecture.md` gained a "Cross-Cutting Concerns" section (progress reporting, MCP Tasks deferral, typed structured outputs, per-call timeouts) plus a sequence diagram; fixed one adjacent stale diagram still showing the removed `breakpoint_wait` tool (now shows the `debugger/breakpointHit` notification it was replaced by). `website/docs/tools/index.md` gained matching short sections pointing at the detailed per-tool docs
- [X] T088 [P] `tests/DebugMcp.Tests/Contract/NoModelDependencyTests.cs` (FR-022, SC-013) — 4 checks: `Directory.Packages.props` and the built assembly's referenced-assembly list contain no known model-provider SDK name; `Program.cs` source contains no model-credential-shaped CLI option/env var; `SuspicionRanker`'s constructor stays parameterless (the one place a model dependency could plausibly sneak in later)
- [X] T089 Ran every quickstart.md scenario live over stdio with a purpose-built Python JSON-RPC harness (`scratchpad/t089/{harness,scenarios}.py`, not committed — reproducible from this note), driving a freshly-built `dotnet run --project DebugMcp --no-build` process. 38/38 assertions passed. Two premise corrections found and worked around rather than silently reproduced:
  - **Scenario 2 requires a second, handshake-free session** — decompiling `ModelContextProtocol.Core`/`.Extensions.Tasks` 2.2.0 (`McpSessionHandler.ValidateRequiredPerRequestMetadata`, `PopulateContextFromMeta`, `HasTaskExtensionOptIn`) showed the reserved `_meta/io.modelcontextprotocol/clientCapabilities` key is unconditionally rejected (`-32600`) under every protocol version `initialize` can negotiate (2024-11-05 through 2025-11-25), and the Tasks filter's opt-in check reads *only* that per-request field — never the capabilities negotiated at `initialize`. The only reachable path is the SEP-2575 handshake-free mode: skip `initialize` entirely and carry `_meta` (`protocolVersion: "2026-07-28"`, `clientInfo`, `clientCapabilities`) on every request. quickstart.md's own "does not accept 2026-07-28" warning is scoped to the `initialize` handshake and doesn't call this out — a documentation-clarity gap, not a product bug. Also: quickstart's "34 non-qualifying tools are pinned to `TaskSupport = Forbidden`" doesn't match the shipped mechanism (`TaskExecutionPolicy.GetMode` returning `McpTaskExecutionMode.Synchronous`, not a `TaskSupport` enum that doesn't exist in SDK 2.2.0) — same drift already noted for T072-T083.
  - **`_fault` argv switch added to `tests/DebugTestApp/Program.cs`** (`--fault NullDereference`) to drive one FaultScenarios fixture live under a real debugger session, proving the wire delivers the `ranking` field from an actual exception pause (not just from the Unit-tier hand-built `AutopsyFrame` fixtures). The full 10-fixture/10-replay accuracy and determinism matrices stay Unit-tier (`RankingAccuracyTests`, `DeterminismTests`) — re-running them live would exercise the identical `SuspicionRanker` code path for no new evidence; the live pass's job is proving the *wire*, which it does.
  - Scenario results: **S1** (progress+cancel) — first `notifications/progress` at 0.01s (via `debug_launch`) and 0.03s (via a live `resharper_inspect_solution` run against `tests/ReSharperSampleApp/ReSharperSampleApp.sln`, cached engine 2026.2.1, no download); zero notifications when no token supplied; next call succeeds within budget after cancellation-equivalent disconnect. **S2** (deferred tasks) — `resultType: "task"` returned, task reached `completed`, byte-identical normalized result between deferred and direct paths (FR-014), cancel acknowledged and reached a terminal status, fabricated task id returned a distinguishable error, non-qualifying tool (`timeline_query`) ignored the declared extension. **S3** (result contracts) — 39/39 tools carry `outputSchema`; `code_load` and a deliberate `variables_get` failure both validated `structuredContent`/`content[0]`/`isError`+`success:false`+documented `code`; `content[0].text` parses standalone with the same fields (backward-compat proof). **S4** (enrichment) — `NullDereference` fixture launched live, exception breakpoint set, `debugger/breakpointHit` observed, `exception_get_context` and `stacktrace_get` both carried `ranking` with the fault frame (index 0) ranked first, matching `expected-answers.json`. **S5** (timeouts) — every blocking tool's timeout param present/absent per `TimeoutPolicy.Specs` confirmed over the wire via `tools/list`; a 1ms `debug_launch` timeout produced a `TIMEOUT` error naming the budget within 5s and left the session usable for an immediate next launch; `debug_launch`'s schema documents its 30000ms default and `resharper_inspect_solution`'s documents its own 300s default (not 30000) — both read from the live `tools/list` response, not source.
  - **SC-003**: longest single exchange for the deferred `debug_launch` path (task creation + polls) was 0.026s — far under the 1000ms `pollIntervalMs` the handle itself suggests.
  - Housekeeping from FR-028/FR-029 (already done in T084-T085) re-verified present in `ROADMAP.md`.
- [X] T090 Final gate, all green: `dotnet build --no-incremental` and `dotnet build -c Release` both 0 errors (SC-010). 3 warnings present in both, all pre-existing and unrelated to this feature (`git diff main...HEAD` shows zero changes to either file on this branch) — `tests/TestTargetApp/Libs/Expressions/Expressions.cs` (2× CS0219, unused locals) and `tests/DebugMcp.E2E/StepDefinitions/BreakpointSteps.cs` (1× CS8604, nullable). `DebugMcp` itself (the shipped product) builds at 0 warnings. `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — **1620/1620 passing**, 0 failed, 0 skipped. ROADMAP.md housekeeping (FR-028/FR-029) re-confirmed present.

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
