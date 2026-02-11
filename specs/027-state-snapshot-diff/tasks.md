# Tasks: State Snapshot & Diff

**Input**: Design documents from `/specs/027-state-snapshot-diff/`
**Prerequisites**: plan.md, spec.md, data-model.md, contracts/, research.md, quickstart.md

**Tests**: Included — constitution principle III (Test-First) is NON-NEGOTIABLE.

**Organization**: Tasks grouped by user story. Each story is independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Model records and service interfaces shared across all user stories

- [x] T001 [P] Create Snapshot record in DebugMcp/Models/Snapshots/Snapshot.cs — positional record with Id (string), Label (string), CreatedAt (DateTimeOffset), ThreadId (int), FrameIndex (int), FunctionName (string), Depth (int), Variables (IReadOnlyList\<SnapshotVariable\>)
- [x] T002 [P] Create SnapshotVariable record in DebugMcp/Models/Snapshots/SnapshotVariable.cs — positional record with Name, Path, Type, Value (all string), Scope (VariableScope), Children (IReadOnlyList\<SnapshotVariable\>? = null)
- [x] T003 [P] Create DiffChangeType enum and DiffEntry record in DebugMcp/Models/Snapshots/DiffEntry.cs — DiffChangeType (Added, Removed, Modified); DiffEntry positional record with Name, Path, Type (string), OldValue (string?), NewValue (string?), ChangeType (DiffChangeType)
- [x] T004 [P] Create SnapshotDiff record in DebugMcp/Models/Snapshots/SnapshotDiff.cs — positional record with SnapshotIdA, SnapshotIdB (string), Added/Removed/Modified (IReadOnlyList\<DiffEntry\>), ThreadMismatch (bool), TimeDelta (TimeSpan), Unchanged (int)
- [x] T005 [P] Create ISnapshotStore interface in DebugMcp/Services/Snapshots/ISnapshotStore.cs — Add(Snapshot), Get(string id), GetAll(), Remove(string id), Clear(), Count property
- [x] T006 [P] Create ISnapshotService interface in DebugMcp/Services/Snapshots/ISnapshotService.cs — CreateSnapshotAsync(label?, threadId?, frameIndex, depth), DiffSnapshots(id1, id2), ListSnapshots(), DeleteSnapshot(id?), methods returning appropriate types

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: SnapshotStore implementation and DI registration — MUST complete before user stories

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T007 Write SnapshotStore unit tests in tests/DebugMcp.Tests/Unit/Snapshots/SnapshotStoreTests.cs — test Add/Get/GetAll/Remove/Clear/Count, duplicate ID handling, thread safety with concurrent adds, Get returns null for missing ID
- [x] T008 Implement SnapshotStore in DebugMcp/Services/Snapshots/SnapshotStore.cs — ConcurrentDictionary\<string, Snapshot\> storage, implement ISnapshotStore, inject ILogger\<SnapshotStore\>
- [x] T009 Register snapshot services in DebugMcp/Program.cs — add `builder.Services.AddSingleton<ISnapshotStore, SnapshotStore>()` and `builder.Services.AddSingleton<ISnapshotService, SnapshotService>()` alongside existing service registrations

**Checkpoint**: Foundation ready — SnapshotStore CRUD works, services registered in DI

---

## Phase 3: User Story 1 — Capture Debug State (Priority: P1) MVP

**Goal**: Agent can pause at breakpoint, call `snapshot_create`, get back a snapshot ID with captured variable metadata

**Independent Test**: Pause at a breakpoint, call snapshot_create, verify a snapshot ID is returned with the captured variable names, types, and values

### Tests for User Story 1

> **Write these tests FIRST, ensure they FAIL before implementation**

- [x] T010 [US1] Write SnapshotService.CreateSnapshotAsync unit tests in tests/DebugMcp.Tests/Unit/Snapshots/SnapshotServiceCreateTests.cs — mock IDebugSessionManager.GetVariables() to return test variables; test: creates snapshot with snap-{guid} ID, stores in SnapshotStore, returns correct metadata (label, timestamp, threadId, frameIndex, functionName, variableCount); test auto-label generation ("snapshot-1", "snapshot-2"); test error when not paused; test soft limit warning at 100 snapshots
- [x] T011 [US1] Write SnapshotCreateTool unit tests in tests/DebugMcp.Tests/Unit/Snapshots/SnapshotCreateToolTests.cs — mock ISnapshotService; test: returns success JSON with snapshot metadata; test: error JSON when not paused (NOT_PAUSED); test: passes thread_id/frame_index/depth parameters through; test: optional label parameter
- [x] T012 [P] [US1] Write contract tests for snapshot_create in tests/DebugMcp.Tests/Contract/SnapshotToolContractTests.cs — verify McpServerTool attribute (Name="snapshot_create"), parameter types and defaults match contract, tool annotations (readOnlyHint=false, destructiveHint=false, idempotentHint=false)

### Implementation for User Story 1

- [x] T013 [US1] Implement SnapshotService.CreateSnapshotAsync in DebugMcp/Services/Snapshots/SnapshotService.cs — inject IDebugSessionManager, ISnapshotStore, IProcessDebugger, ILogger; call GetVariables(threadId, frameIndex, "all"); map Variable list to SnapshotVariable list; generate snap-{guid} ID; auto-generate label if not provided; store in SnapshotStore; log operation; return snapshot metadata
- [x] T014 [US1] Implement SnapshotCreateTool in DebugMcp/Tools/SnapshotCreateTool.cs — McpServerToolType class with McpServerTool method; parameters: label?, thread_id?, frame_index=0, depth=0; call ISnapshotService.CreateSnapshotAsync; return JSON with {success, snapshot: {id, label, timestamp, threadId, frameIndex, functionName, variableCount, depth}}; handle NOT_PAUSED error; tool annotations per contract

**Checkpoint**: snapshot_create works end-to-end. Agent can capture variables at a breakpoint.

---

## Phase 4: User Story 2 — Compare Two Snapshots (Priority: P1)

**Goal**: Agent can diff two snapshots and see added/removed/modified variables with before/after values

**Independent Test**: Take two snapshots at different breakpoint states, call snapshot_diff, verify accurate change lists

### Tests for User Story 2

> **Write these tests FIRST, ensure they FAIL before implementation**

- [x] T015 [US2] Write SnapshotService.DiffSnapshots unit tests in tests/DebugMcp.Tests/Unit/Snapshots/SnapshotServiceDiffTests.cs — test: modified variable (same path, different value) appears in modified list with old/new values; test: added variable (in B not A) appears in added list; test: removed variable (in A not B) appears in removed list; test: identical snapshots produce empty diff; test: thread mismatch warning when threadIds differ; test: timeDelta computed correctly; test: error for invalid snapshot ID; test: unchanged count is correct
- [x] T016 [US2] Write SnapshotDiffTool unit tests in tests/DebugMcp.Tests/Unit/Snapshots/SnapshotDiffToolTests.cs — mock ISnapshotService; test: returns success JSON with diff structure (summary, added, removed, modified); test: error JSON for SNAPSHOT_NOT_FOUND; test: both snapshot_id_1 and snapshot_id_2 are required
- [x] T017 [P] [US2] Add contract tests for snapshot_diff in tests/DebugMcp.Tests/Contract/SnapshotToolContractTests.cs — verify McpServerTool attribute (Name="snapshot_diff"), required parameters snapshot_id_1 and snapshot_id_2, annotations (readOnlyHint=true, idempotentHint=true)

### Implementation for User Story 2

- [x] T018 [US2] Implement SnapshotService.DiffSnapshots in DebugMcp/Services/Snapshots/SnapshotService.cs — retrieve both snapshots from store (error if not found); build Dictionary\<string, SnapshotVariable\> keyed by Path for each; compute added/removed/modified sets; build SnapshotDiff record with ThreadMismatch flag and TimeDelta; log operation
- [x] T019 [US2] Implement SnapshotDiffTool in DebugMcp/Tools/SnapshotDiffTool.cs — McpServerToolType class; required parameters snapshot_id_1 and snapshot_id_2; call ISnapshotService.DiffSnapshots; return JSON with {success, diff: {snapshotIdA, snapshotIdB, threadMismatch, timeDelta, summary: {added, removed, modified, unchanged}, added[], removed[], modified[]}}; handle SNAPSHOT_NOT_FOUND error; tool annotations per contract

**Checkpoint**: Full snapshot-diff workflow works. Agent can capture state, act, capture again, and diff.

---

## Phase 5: User Story 3 — List and Manage Snapshots (Priority: P2)

**Goal**: Agent can list all snapshots and delete specific ones or clear all

**Independent Test**: Create several snapshots, list them, delete one, list again, clear all, list again

### Tests for User Story 3

> **Write these tests FIRST, ensure they FAIL before implementation**

- [x] T020 [P] [US3] Write SnapshotListTool unit tests in tests/DebugMcp.Tests/Unit/Snapshots/SnapshotListToolTests.cs — mock ISnapshotService; test: returns all snapshots with metadata (id, label, timestamp, threadId, functionName, variableCount); test: empty list returns {snapshots: [], count: 0}
- [x] T021 [P] [US3] Write SnapshotDeleteTool unit tests in tests/DebugMcp.Tests/Unit/Snapshots/SnapshotDeleteToolTests.cs — mock ISnapshotService; test: single delete by ID returns {deleted: "snap-...", remaining: N}; test: clear all (no ID) returns {deleted: "all", remaining: 0}; test: SNAPSHOT_NOT_FOUND error for invalid ID
- [x] T022 [P] [US3] Add contract tests for snapshot_list and snapshot_delete in tests/DebugMcp.Tests/Contract/SnapshotToolContractTests.cs — verify McpServerTool attributes, parameter types, annotations per contract

### Implementation for User Story 3

- [x] T023 [P] [US3] Implement SnapshotListTool in DebugMcp/Tools/SnapshotListTool.cs — McpServerToolType class; no parameters; call ISnapshotService.ListSnapshots(); return JSON with {success, snapshots: [...], count}; annotations: readOnlyHint=true, idempotentHint=true
- [x] T024 [P] [US3] Implement SnapshotDeleteTool in DebugMcp/Tools/SnapshotDeleteTool.cs — McpServerToolType class; optional snapshot_id parameter; if ID provided: call DeleteSnapshot(id), return {deleted: id, remaining}; if no ID: call ClearAll, return {deleted: "all", remaining: 0}; handle SNAPSHOT_NOT_FOUND; annotations: destructiveHint=true
- [x] T025 [US3] Implement ListSnapshots and DeleteSnapshot in SnapshotService — ListSnapshots returns store.GetAll() mapped to metadata; DeleteSnapshot(id) removes from store (error if not found); DeleteSnapshot(null) calls store.Clear()

**Checkpoint**: Full CRUD lifecycle works. Agent can create, list, delete, and clear snapshots.

---

## Phase 6: User Story 4 — Capture Nested Object State (Priority: P2)

**Goal**: Snapshots capture nested fields up to configurable depth, and diffs show nested changes

**Independent Test**: Pause where a complex object is in scope, snapshot with depth=2, verify nested fields. Change nested field, snapshot again, diff shows nested change.

### Tests for User Story 4

> **Write these tests FIRST, ensure they FAIL before implementation**

- [x] T026 [US4] Write nested expansion unit tests in tests/DebugMcp.Tests/Unit/Snapshots/SnapshotServiceDepthTests.cs — test: depth=0 captures only top-level variables (no children); test: depth=1 expands variables with HasChildren=true one level; test: depth=2 expands two levels deep; test: expanded variables have correct dot-separated paths (e.g., "order.Customer.Name"); test: diff of expanded snapshots shows nested path changes

### Implementation for User Story 4

- [x] T027 [US4] Implement depth expansion in SnapshotService.CreateSnapshotAsync — after capturing top-level variables, for each with HasChildren=true and depth > 0: call IDebugSessionManager.GetVariables with expand parameter to get children; recursively expand up to maxDepth; build SnapshotVariable children list with dot-separated Path values; handle circular references
- [x] T028 [US4] Verify diff works with expanded snapshots — ensure DiffSnapshots compares by full Path (including nested paths like "order.Customer.Name"); existing path-keyed algorithm should work without changes; add integration-level test if needed

**Checkpoint**: Full depth expansion works. Agent can snapshot complex objects and diff nested changes.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Session cleanup, edge cases, build verification

- [x] T029 Implement session cleanup in SnapshotService — subscribe to IProcessDebugger.StateChanged in constructor; on SessionState.Disconnected, call SnapshotStore.Clear() fire-and-forget; write unit test verifying cleanup occurs on disconnect
- [x] T030 Add soft limit warning — in SnapshotService.CreateSnapshotAsync, after store.Add(), check store.Count >= 100; if so, log warning and include "warning" field in return; write unit test for soft limit behavior
- [x] T031 Build verification — run `dotnet build` (0 errors, 0 warnings); run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit.Snapshots|FullyQualifiedName~SnapshotToolContract"` (all pass)
- [x] T032 Run quickstart.md validation — follow manual MCP verification steps from specs/027-state-snapshot-diff/quickstart.md; verify snapshot_create, snapshot_diff, snapshot_list, snapshot_delete all work via live MCP; verify session cleanup on debug_disconnect

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (models needed for store) — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — MVP, must complete first
- **US2 (Phase 4)**: Depends on Phase 3 (needs snapshot_create to create test data)
- **US3 (Phase 5)**: Depends on Phase 2 only (list/delete are independent of capture/diff logic)
- **US4 (Phase 6)**: Depends on Phase 3 (extends CreateSnapshotAsync with depth)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Foundational only — no dependencies on other stories
- **US2 (P1)**: Depends on US1 (snapshot_create must exist to create data for diffing)
- **US3 (P2)**: Independent of US1/US2 (list/delete operate on store directly)
- **US4 (P2)**: Depends on US1 (extends the capture mechanism)

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Contract tests [P] can run in parallel with unit tests
- Service implementation before tool implementation
- Tool implementation last (depends on service)

### Parallel Opportunities

- **Phase 1**: All model/interface tasks (T001–T006) can run in parallel [P]
- **Phase 3**: Contract test T012 can run in parallel with unit tests T010–T011
- **Phase 5**: List tool (T020, T023) and delete tool (T021, T024) can run in parallel [P]
- **Phase 5+6**: US3 and US4 can run in parallel (no cross-dependencies)

---

## Parallel Example: Phase 1 Setup

```
# All model files can be created in parallel:
T001: Create Snapshot.cs
T002: Create SnapshotVariable.cs
T003: Create DiffEntry.cs
T004: Create SnapshotDiff.cs
T005: Create ISnapshotStore.cs
T006: Create ISnapshotService.cs
```

## Parallel Example: Phase 5 US3

```
# List and Delete tools are independent files:
T020 + T023: SnapshotListTool tests + implementation
T021 + T024: SnapshotDeleteTool tests + implementation
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (models + interfaces)
2. Complete Phase 2: Foundational (SnapshotStore + DI)
3. Complete Phase 3: User Story 1 (snapshot_create)
4. **STOP and VALIDATE**: Test snapshot_create independently
5. Proceed to US2 for full value

### Incremental Delivery

1. Phase 1 + 2 → Foundation ready
2. Add US1 → snapshot_create works → Test independently
3. Add US2 → snapshot_diff works → Core workflow complete (capture → diff)
4. Add US3 → list/delete works → Full management
5. Add US4 → nested expansion → Enhanced granularity
6. Polish → cleanup, limits, build verification

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing (RED → GREEN → REFACTOR)
- Commit after each phase completion
- Stop at any checkpoint to validate story independently
