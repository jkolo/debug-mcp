# Tasks: MCP Event-Driven Debugger Interface (030)

**Input**: Design documents from `specs/030-mcp-event-driven/`  
**Branch**: `030-mcp-event-driven`  
**TDD**: Test-First NON-NEGOTIABLE (Constitution §III)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- All tasks include exact file paths

---

## Phase 1: Setup

**Purpose**: Establish green baseline on the new branch.

- [X] T001 Run `dotnet test tests/DebugMcp.Tests --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` and confirm all pass (baseline before any changes)
- [X] T002 Run `dotnet build` and confirm 0 errors, 0 warnings

**Checkpoint**: Baseline confirmed. All 41 tools exist, all tests green.

---

## Phase 2: Foundational — Cleanup (Blocking Prerequisite)

**Purpose**: Remove 6 polling/redundant tools and clean BreakpointManager polling state. All subsequent phases depend on this.

**⚠️ CRITICAL**: Write RED tests FIRST (they must fail), then delete/remove, then verify GREEN.

### TDD — RED first

- [X] T003 Write contract test in `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs`: change `HaveCount(41)` → `HaveCount(35)`, remove entries for `breakpoint_wait`, `breakpoint_list`, `debug_state`, `threads_list`, `modules_list`, `snapshot_list` from `ExpectedAnnotations` dict — run test and confirm RED (6 tools still exist)
- [X] T004 Write unit test `tests/DebugMcp.Tests/Unit/Breakpoints/BreakpointManagerPollingRemovalTests.cs`: assert via reflection that `IBreakpointManager` has no `WaitForBreakpointAsync` method — confirm RED

### Delete 6 tools

- [X] T005 Delete `DebugMcp/Tools/BreakpointWaitTool.cs`
- [X] T006 [P] Delete `DebugMcp/Tools/BreakpointListTool.cs`
- [X] T007 [P] Delete `DebugMcp/Tools/DebugStateTool.cs`
- [X] T008 [P] Delete `DebugMcp/Tools/ThreadsListTool.cs`
- [X] T009 [P] Delete `DebugMcp/Tools/ModulesListTool.cs`
- [X] T010 [P] Delete `DebugMcp/Tools/SnapshotListTool.cs`

### Remove polling mechanism

- [X] T011 Remove `WaitForBreakpointAsync(TimeSpan timeout, CancellationToken)` declaration from `DebugMcp/Services/Breakpoints/IBreakpointManager.cs`
- [X] T012 Remove `WaitForBreakpointAsync` implementation + fields `_pendingHit`, `_hitWaiter`, `_hitLock` + the lock block in `OnBreakpointHit` that signals `_hitWaiter` from `DebugMcp/Services/Breakpoints/BreakpointManager.cs`

### Verify GREEN

- [X] T013 Run `dotnet build` and confirm 0 errors; run `dotnet test tests/DebugMcp.Tests --filter "FullyQualifiedName~Contract"` and confirm T003's count assertion passes (35 tools); confirm T004's reflection test passes

**Checkpoint**: 35 tools, no polling state. All contract tests green.

---

## Phase 3: User Story 1 — Breakpoint Hit Notification with Locals (P1)

**Goal**: The `debugger/breakpointHit` notification payload includes locals from the top frame.

**Independent Test**: Run `BreakpointNotificationLocalsTests` — asserts that the JSON notification contains a `locals` array (or `localsError`) without calling any additional tool.

### TDD — RED first

- [X] T014 Write unit test `tests/DebugMcp.Tests/Unit/Notifications/BreakpointNotificationLocalsTests.cs` with four test cases:
  - `BreakpointHit_WhenLocalsAvailable_NotificationContainsLocalsArray` — mock IDebugSessionManager returning 2 variables; assert notification JSON has `"locals":[{name,type,value,has_children},...]`
  - `BreakpointHit_WhenLocalsEvaluationTimesOut_NotificationContainsLocalsError` — mock returns after 200ms, budget 100ms; assert `"locals_error":"timeout"`
  - `BreakpointHit_WhenLocalsEvaluationFails_NotificationContainsLocalsError` — mock throws; assert `"locals_error":"unavailable"`
  - `BreakpointHit_ForTracepoint_LocalsNotFetched` — assert tracepoint notifications do NOT include locals (too expensive for non-blocking)
  Confirm all RED (struct lacks Locals field)

### Models

- [X] T015 [US1] Create record `DebugMcp/Models/Breakpoints/VariableSummary.cs`:
  ```csharp
  public sealed record VariableSummary(string Name, string Type, string Value, bool HasChildren);
  ```
- [X] T016 [US1] Extend `DebugMcp/Models/Breakpoints/BreakpointNotification.cs` with two optional fields: `IReadOnlyList<VariableSummary>? Locals = null` and `string? LocalsError = null`

### Implementation

- [X] T017 [US1] In `DebugMcp/Services/Breakpoints/BreakpointManager.cs`: inject `IDebugSessionManager` via constructor (add parameter); in `CreateNotification()` for blocking breakpoints only: call `sessionManager.GetVariablesAsync(hit.ThreadId, frameIndex: 0, scope: "locals")` wrapped in `CancellationTokenSource(TimeSpan.FromMilliseconds(100))` try/catch; populate `Locals` on success, `LocalsError` on timeout/exception
- [X] T018 [US1] In `DebugMcp/Infrastructure/BreakpointNotifier.cs` `SendNotificationToMcpAsync()`: add `locals` array (mapping `VariableSummary` → `{name, type, value, has_children}`) and `localsError` fields to the anonymous notification params object

### Verify GREEN

- [X] T019 [US1] Run `dotnet test tests/DebugMcp.Tests --filter "FullyQualifiedName~BreakpointNotificationLocalsTests"` — all 4 tests GREEN; run full test suite, confirm no regressions

**Checkpoint**: Notification includes locals. US1 independently verifiable.

---

## Phase 4: User Story 2 — New Resources: modules + snapshots (P2)

**Goal**: `debugger://modules` and `debugger://snapshots` are readable MCP resources with auto-update notifications on change.

**Independent Test**: Run `ModulesResourceTests` and `SnapshotsResourceTests` — verify resource JSON and that `NotifyResourceUpdated` is called after module load/snapshot create.

### TDD — RED first

- [X] T020 [US2] Write unit test `tests/DebugMcp.Tests/Unit/Resources/ModulesResourceTests.cs` with:
  - `GetModulesJson_WhenNoSession_ReturnsEmptyList` — `{"modules":[],"count":0}`
  - `GetModulesJson_WhenSessionHasModules_ReturnsModuleList` — mock session returning 2 modules; assert JSON has name, path, version, hasSymbols, baseAddress, size
  - `OnModuleLoaded_CallsNotifyResourceUpdated_WithModulesUri` — fire `ModuleLoaded` event; assert `NotifyResourceUpdated("debugger://modules")` called
  Confirm RED (method doesn't exist yet)

- [X] T021 [US2] Write unit test `tests/DebugMcp.Tests/Unit/Resources/SnapshotsResourceTests.cs` with:
  - `GetSnapshotsJson_WhenEmpty_ReturnsEmptyList` — `{"snapshots":[],"count":0}`
  - `GetSnapshotsJson_WithOneSnapshot_ReturnsSnapshotMeta` — assert id, label, createdAt, threadId, variableCount
  - `SnapshotStore_WhenAdd_FiresChangedEvent` — Add snapshot, assert Changed event fired
  - `SnapshotStore_WhenRemove_FiresChangedEvent`
  - `SnapshotStore_WhenClear_FiresChangedEvent`
  - `McpResourceNotifier_OnSnapshotChanged_CallsNotifyResourceUpdated` — subscribe Changed handler, fire event, assert `NotifyResourceUpdated("debugger://snapshots")` called
  Confirm RED (Changed event doesn't exist yet)

### Models

- [X] T022 [US2] Create `DebugMcp/Services/Snapshots/SnapshotChangedEventArgs.cs`:
  ```csharp
  public enum SnapshotChangeKind { Added, Removed, Cleared }
  public sealed class SnapshotChangedEventArgs : EventArgs
  {
      public required string SnapshotId { get; init; }
      public required SnapshotChangeKind Kind { get; init; }
  }
  ```

### Services

- [X] T023 [US2] Add `event EventHandler<SnapshotChangedEventArgs>? Changed;` to `DebugMcp/Services/Snapshots/ISnapshotStore.cs`
- [X] T024 [US2] Implement `Changed` event in `DebugMcp/Services/Snapshots/SnapshotStore.cs`: fire in `Add()` (Kind.Added), `Remove()` (Kind.Removed), `Clear()` (Kind.Cleared)

### Resources

- [X] T025 [US2] Add `GetModulesJson()` to `DebuggerResourceProvider` in `DebugMcp/Services/Resources/DebuggerResourceProvider.cs` with attribute `[McpServerResource(UriTemplate = "debugger://modules", Name = "Loaded Modules", MimeType = "application/json")]`; reuse same module-fetching service call used by the now-deleted `ModulesListTool` (check git history for that call pattern if needed; it likely uses `_processDebugger.GetLoadedModulesAsync()` or `_sessionManager.GetModulesAsync()`); return `{"modules":[...],"count":N}` or `{"modules":[],"count":0}` when no session
- [X] T026 [US2] Add `GetSnapshotsJson()` to `DebuggerResourceProvider` in `DebugMcp/Services/Resources/DebuggerResourceProvider.cs` with `[McpServerResource(UriTemplate = "debugger://snapshots", Name = "Snapshots", MimeType = "application/json")]`; inject `ISnapshotStore` via constructor; return `{"snapshots":[{id,label,createdAt,threadId,frameIndex,functionName,variableCount}],"count":N}`

### Notifications

- [X] T027 [US2] In `DebugMcp/Services/Resources/McpResourceNotifier.cs`: add `NotifyResourceUpdated("debugger://modules")` to both `OnModuleLoaded` and `OnModuleUnloaded` handlers (existing handlers, one line each)
- [X] T028 [US2] In `DebugMcp/Services/Resources/McpResourceNotifier.cs`: inject `ISnapshotStore` via constructor; in constructor subscribe `_snapshotStore.Changed += (_, _) => NotifyResourceUpdated("debugger://snapshots")`

### Verify GREEN

- [X] T029 [US2] Run `dotnet test tests/DebugMcp.Tests --filter "FullyQualifiedName~ModulesResourceTests|FullyQualifiedName~SnapshotsResourceTests"` — all tests GREEN; run full suite, confirm no regressions

**Checkpoint**: Both new resources readable and auto-notifying. US2 independently verifiable.

---

## Phase 5: User Story 3 — Session State Change Notifications (P3)

**Goal**: The server sends `debugger/sessionStateChanged` notification on every session state transition.

**Independent Test**: Run `SessionStateNotificationTests` — fire `StateChanged` event, assert MCP notification with correct payload.

### TDD — RED first

- [X] T030 [US3] Write unit test `tests/DebugMcp.Tests/Unit/Notifications/SessionStateNotificationTests.cs` with:
  - `OnStateChanged_RunningToPaused_SendsSessionStateChangedNotification` — assert method `"debugger/sessionStateChanged"`, payload has `newState:"Paused"`, `oldState:"Running"`, `pauseReason:"Breakpoint"`, `location` non-null, `activeThreadId` non-null
  - `OnStateChanged_PausedToRunning_SendsNotificationWithNullLocation` — assert `newState:"Running"`, `location:null`
  - `OnStateChanged_AnyToDisconnected_SendsNotification` — assert `newState:"Disconnected"`
  - `OnStateChanged_LaunchingToRunning_SendsNotification` — assert `newState:"Running"`, `oldState:"Starting"`
  Confirm RED (method doesn't exist yet)

### Implementation

- [X] T031 [US3] In `DebugMcp/Services/Resources/McpResourceNotifier.cs`: add private method `SendSessionStateNotificationAsync(SessionStateChangedEventArgs e)` that calls `server.SendNotificationAsync("debugger/sessionStateChanged", new { oldState, newState, pauseReason, location, activeThreadId, timestamp })` fire-and-forget; call `_ = SendSessionStateNotificationAsync(e)` inside the existing `OnStateChanged` handler alongside the existing `NotifyResourceUpdated(...)` calls

### Verify GREEN

- [X] T032 [US3] Run `dotnet test tests/DebugMcp.Tests --filter "FullyQualifiedName~SessionStateNotificationTests"` — all 4 tests GREEN; run full suite

**Checkpoint**: Session state changes push notifications. US3 independently verifiable.

---

## Phase 6: User Story 4 — Process I/O Honest Signatures (P4)

**Goal**: `process_read_output` and `process_write_input` tools return `string`, not `Task<string>`.

**Independent Test**: Run `ProcessIoAsyncTests` — reflection verifies return type.

### TDD — RED first

- [X] T033 [US4] Write unit test `tests/DebugMcp.Tests/Unit/ProcessIo/ProcessIoAsyncTests.cs` with:
  - `ProcessReadOutputTool_ReadOutput_ReturnsString_NotTask` — reflection: assert tool method return type is `string` (not `Task<string>` or `ValueTask<string>`)
  - `ProcessWriteInputTool_WriteInput_ReturnsString_NotTask` — same
  Confirm RED (methods currently return `Task<string>`)

### Implementation

- [X] T034 [P] [US4] In `DebugMcp/Tools/ProcessReadOutputTool.cs`: change method return type from `Task<string>` to `string`, rename from `ReadOutputAsync` to `ReadOutput`, unwrap `Task.FromResult(...)` → return the string directly
- [X] T035 [P] [US4] In `DebugMcp/Tools/ProcessWriteInputTool.cs`: same — `Task<string>` → `string`, rename `WriteInputAsync` → `WriteInput`, unwrap `Task.FromResult(...)`

### Verify GREEN

- [X] T036 [US4] Run `dotnet test tests/DebugMcp.Tests --filter "FullyQualifiedName~ProcessIoAsyncTests"` — both tests GREEN; run full suite

**Checkpoint**: Honest synchronous signatures. US4 independently verifiable.

---

## Final Phase: Polish & Cross-Cutting Concerns

- [X] T037 Run full test suite: `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — all tests GREEN, no regressions
- [X] T038 Run `dotnet build -c Release` — 0 errors, 0 warnings
- [X] T039 Verify `debugger://modules` and `debugger://snapshots` appear in the `resources/list` response (run `dotnet run --project DebugMcp -- --help` to confirm server starts, check resource listing in manual smoke test per `quickstart.md`)
- [X] T040 Update `CLAUDE.md` `Active Technologies` and `Recent Changes` sections for feature 030

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user story phases**
- **Phase 3 (US1)**: Depends on Phase 2
- **Phase 4 (US2)**: Depends on Phase 2 (independent from Phase 3)
- **Phase 5 (US3)**: Depends on Phase 2 (independent from Phase 3 and 4)
- **Phase 6 (US4)**: Depends on Phase 2 (independent from Phase 3, 4, 5)
- **Polish**: Depends on all US phases complete

### User Story Dependencies

- **US1 (P1)**: After Phase 2 complete — no dependency on US2/US3/US4
- **US2 (P2)**: After Phase 2 complete — no dependency on US1/US3/US4
- **US3 (P3)**: After Phase 2 complete — no dependency on US1/US2/US4
- **US4 (P4)**: After Phase 2 complete — no dependency on US1/US2/US3

### Within Each User Story

1. Write RED tests FIRST (mandatory TDD)
2. Create model/record types before services that use them
3. Services before resource providers that call them
4. Verify RED → implement → verify GREEN

### Parallel Opportunities

- T005–T010: All 6 tool deletions can run in parallel (different files)
- T020 and T021: Both test files can be written in parallel
- T023 and T025: ISnapshotStore event + GetModulesJson can run in parallel (different files)
- T024 and T026: SnapshotStore impl + GetSnapshotsJson can run in parallel (different files)
- T034 and T035: Both process I/O tool fixes can run in parallel

---

## Parallel Example: Phase 2 (delete tools)

```bash
# After writing RED tests (T003, T004), delete all 6 tools in parallel:
Task: "Delete DebugMcp/Tools/BreakpointWaitTool.cs"         # T005
Task: "Delete DebugMcp/Tools/BreakpointListTool.cs"         # T006
Task: "Delete DebugMcp/Tools/DebugStateTool.cs"             # T007
Task: "Delete DebugMcp/Tools/ThreadsListTool.cs"            # T008
Task: "Delete DebugMcp/Tools/ModulesListTool.cs"            # T009
Task: "Delete DebugMcp/Tools/SnapshotListTool.cs"           # T010
```

## Parallel Example: Phase 4 (US2 resources)

```bash
# After writing RED tests (T020, T021) and creating EventArgs (T022):
Task: "Add Changed event to ISnapshotStore.cs"              # T023
Task: "Add GetModulesJson() to DebuggerResourceProvider"    # T025

# After T023 completes:
Task: "Implement Changed in SnapshotStore.cs"               # T024
Task: "Add GetSnapshotsJson() to DebuggerResourceProvider"  # T026
```

---

## Implementation Strategy

### MVP: Phase 1 + 2 Only

1. Confirm baseline (T001–T002)
2. Write RED tests (T003–T004)
3. Delete 6 tools (T005–T010)
4. Remove polling (T011–T012)
5. Verify GREEN (T013)
6. **STOP**: 35 tools, clean BreakpointManager, all tests green — already a valid ship

### Incremental Delivery

- MVP: Phase 2 → 35 tools with clean notification foundation
- +US1 (Phase 3) → notifications include locals (immediate agent value)
- +US2 (Phase 4) → new resources for modules + snapshots
- +US3 (Phase 5) → session state push notifications
- +US4 (Phase 6) → process I/O correctness

### Parallel Team Strategy

After Phase 2 completes:
- Developer A: Phase 3 (US1 — locals enrichment)
- Developer B: Phase 4 (US2 — new resources)
- Developer C: Phase 5 + 6 (US3 session notifications + US4 I/O fix)

---

## Notes

- Tests marked before implementation: Constitution §III is NON-NEGOTIABLE
- [P] tasks = different files, no dependency conflicts
- `BreakpointManager` now needs `IDebugSessionManager` injected — check DI registration in `Program.cs` (it's already available there; add to constructor)
- `DebuggerResourceProvider` needs `ISnapshotStore` added to constructor — check DI in `Program.cs`
- `McpResourceNotifier` needs `ISnapshotStore` added to constructor — check DI in `Program.cs`
- Locals fetch in `BreakpointManager.CreateNotification()` is best-effort: exception → `LocalsError = "unavailable"`, timeout → `LocalsError = "timeout"`, never throws
- Git: commit after each checkpoint (T013, T019, T029, T032, T036)
