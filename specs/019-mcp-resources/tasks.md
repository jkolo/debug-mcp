# Tasks: MCP Resources for Debugger State

**Input**: Design documents from `/specs/019-mcp-resources/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/mcp-resources.md, quickstart.md

**Tests**: Included — Constitution Principle III (Test-First) is NON-NEGOTIABLE.

**Organization**: Tasks grouped by user story. US1 (Session) and US2 (Breakpoints) are both P1. US3 (Threads) and US4 (Source) are P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Exact file paths included in descriptions

---

## Phase 1: Setup

**Purpose**: Create directory structure and shared infrastructure for MCP Resources

- [x] T001 Create `DebugMcp/Services/Resources/` directory and `tests/DebugMcp.Tests/Unit/Resources/` directory
- [x] T002 Add `Resources` capability with `Subscribe = true` and `ListChanged = true` to MCP server options in `DebugMcp/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that ALL resource user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 [P] Write unit tests for `BreakpointRegistry.Changed` event in `tests/DebugMcp.Tests/Unit/Breakpoints/BreakpointRegistryChangedEventTests.cs`
- [x] T004 [P] Write unit tests for `ThreadSnapshotCache` in `tests/DebugMcp.Tests/Unit/Resources/ThreadSnapshotCacheTests.cs` — test Update on pause, Stale flag when running, CapturedAt timestamp
- [x] T005 [P] Write unit tests for `AllowedSourcePaths` in `tests/DebugMcp.Tests/Unit/Resources/AllowedSourcePathsTests.cs` — test AddModule/RemoveModule, IsAllowed check, path normalization
- [x] T006 [P] Write unit tests for `ResourceNotifier` in `tests/DebugMcp.Tests/Unit/Resources/ResourceNotifierTests.cs` — test subscription tracking, debounce behavior (300ms), notification dispatch for subscribed URIs only, list-changed on session start/end
- [x] T007 Add `event EventHandler? Changed` to `BreakpointRegistry` in `DebugMcp/Services/Breakpoints/BreakpointRegistry.cs` — fire on Add/Remove/Update operations (makes T003 tests pass)
- [x] T008 Implement `ThreadSnapshotCache` in `DebugMcp/Services/Resources/ThreadSnapshotCache.cs` — stores `IReadOnlyList<ThreadInfo>` + `DateTimeOffset CapturedAt` + `bool Stale` property based on current state (makes T004 tests pass)
- [x] T009 Implement `AllowedSourcePaths` in `DebugMcp/Services/Resources/AllowedSourcePaths.cs` — `ConcurrentDictionary<string, string>` of file→module, methods: `AddModule(modulePath, IEnumerable<string> sourcePaths)`, `RemoveModule(modulePath)`, `IsAllowed(filePath) → bool` (makes T005 tests pass)
- [x] T010 Implement `ResourceNotifier` in `DebugMcp/Services/Resources/ResourceNotifier.cs` — subscription tracking (`ConcurrentDictionary<string, bool>`), per-resource debounce timers (300ms), methods: `Subscribe(uri)`, `Unsubscribe(uri)`, `NotifyResourceUpdated(uri)`, `NotifyListChanged()`, event subscriptions to `IProcessDebugger` and `BreakpointRegistry` (makes T006 tests pass)
- [x] T011 Register foundational services in DI: `ThreadSnapshotCache`, `AllowedSourcePaths`, `ResourceNotifier` as singletons in `DebugMcp/Program.cs`

**Checkpoint**: Foundation ready — resource handlers can now use ThreadSnapshotCache, AllowedSourcePaths, ResourceNotifier

---

## Phase 3: User Story 1 — Debug Session Resource (Priority: P1) + User Story 2 — Breakpoints Resource (Priority: P1) MVP

**Goal**: LLM clients can read `debugger://session` and `debugger://breakpoints` as MCP Resources. Resources appear only when a debug session is active. Change notifications emitted on state changes.

**Independent Test**: Attach to a process, call `resources/list` (should return session + breakpoints), call `resources/read` for each URI, verify JSON content matches session/breakpoint state. Disconnect session, call `resources/list` (should return empty).

### Tests for US1 + US2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T012 [P] [US1] Write unit tests for session resource read in `tests/DebugMcp.Tests/Unit/Resources/SessionResourceTests.cs` — test: returns session JSON when active, returns error when no session, JSON contains all expected fields (processId, processName, state, currentLocation, etc.)
- [x] T013 [P] [US2] Write unit tests for breakpoints resource read in `tests/DebugMcp.Tests/Unit/Resources/BreakpointsResourceTests.cs` — test: returns breakpoints+exceptions JSON when active, returns error when no session, JSON contains breakpoint fields (id, type, file, line, hitCount, etc.)
- [x] T014 [P] [US1] Write unit tests for resource listing in `tests/DebugMcp.Tests/Unit/Resources/ResourceListTests.cs` — test: lists session+breakpoints resources when session active, empty list when no session, list-changed notification on session start/end

### Implementation for US1 + US2

- [x] T015 [US1] Create `DebuggerResourceProvider` class with `[McpServerResourceType]` in `DebugMcp/Services/Resources/DebuggerResourceProvider.cs` — inject `IDebugSessionManager`, `BreakpointRegistry`, `ThreadSnapshotCache`, `AllowedSourcePaths`, `ILogger`
- [x] T016 [US1] Implement `GetSession()` method with `[McpServerResource(UriTemplate = "debugger://session", Name = "Debug Session", MimeType = "application/json")]` — serialize `CurrentSession` to JSON per data-model.md `SessionResource` schema
- [x] T017 [US2] Implement `GetBreakpoints()` method with `[McpServerResource(UriTemplate = "debugger://breakpoints", Name = "Breakpoints", MimeType = "application/json")]` — serialize `BreakpointRegistry.GetAll()` + `GetAllExceptions()` to JSON per data-model.md `BreakpointsResource` schema
- [x] T018 [US1] Configure custom `ListResourcesHandler` and `ListResourceTemplatesHandler` on `ResourcesCapability` in `DebugMcp/Program.cs` — return resources only when `CurrentSession != null`, return empty when disconnected
- [x] T019 [US1] Configure `SubscribeToResourcesHandler` and `UnsubscribeFromResourcesHandler` on `ResourcesCapability` in `DebugMcp/Program.cs` — delegate to `ResourceNotifier.Subscribe/Unsubscribe`
- [x] T020 [US1] Register `DebuggerResourceProvider` via `WithResources<DebuggerResourceProvider>()` in `DebugMcp/Program.cs`
- [x] T021 [US1] Add logging for resource reads (URI, duration) and notification sends in `DebuggerResourceProvider` and `ResourceNotifier`

**Checkpoint**: Session and Breakpoints resources functional. `resources/list` returns them when session active, `resources/read` returns correct JSON, notifications debounced.

---

## Phase 4: User Story 3 — Threads Resource (Priority: P2)

**Goal**: LLM clients can read `debugger://threads` as an MCP Resource. Returns fresh thread list when paused, last-known snapshot with stale flag when running.

**Independent Test**: Attach to multi-threaded process, pause, read `debugger://threads` (stale=false), resume, read again (stale=true with previous snapshot).

### Tests for US3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T022 [P] [US3] Write unit tests for threads resource read in `tests/DebugMcp.Tests/Unit/Resources/ThreadsResourceTests.cs` — test: returns thread list with stale=false when paused, returns cached snapshot with stale=true when running, returns error when no session, JSON contains thread fields (id, name, state, isCurrent, location)
- [x] T022b [P] [US3] Add threads listing test to `tests/DebugMcp.Tests/Unit/Resources/ResourceListTests.cs` — test: after US3 implementation, `resources/list` includes `debugger://threads` alongside session+breakpoints

### Implementation for US3

- [x] T023 [US3] Implement `GetThreads()` method with `[McpServerResource(UriTemplate = "debugger://threads", Name = "Threads", MimeType = "application/json")]` in `DebugMcp/Services/Resources/DebuggerResourceProvider.cs` — use `ThreadSnapshotCache` for data, include `stale` and `capturedAt` fields per data-model.md `ThreadsResource` schema
- [x] T024 [US3] Wire `ThreadSnapshotCache` update on `IProcessDebugger.StateChanged` (when paused) in `ResourceNotifier` — call `IDebugSessionManager.GetThreads()` and update cache

**Checkpoint**: Threads resource functional. Returns fresh data when paused, stale cached data when running.

---

## Phase 5: User Story 4 — Source Code Resource (Priority: P2)

**Goal**: LLM clients can read source files referenced by PDB symbols via `debugger://source/{file}`. Only PDB-referenced paths allowed.

**Independent Test**: Attach to process with PDB symbols, read `debugger://source/{validFile}` (returns source code), read `debugger://source/{invalidFile}` (rejected as not in PDB).

### Tests for US4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T025 [P] [US4] Write unit tests for source resource read in `tests/DebugMcp.Tests/Unit/Resources/SourceResourceTests.cs` — test: returns file content for PDB-referenced path, rejects path not in PDB, returns error when file not on disk, returns error when no session, resource template listed in `resources/templates/list`

### Implementation for US4

- [x] T026 [US4] Implement `GetSourceFile(string file)` method with `[McpServerResource(UriTemplate = "debugger://source/{+file}", Name = "Source File", MimeType = "text/plain")]` in `DebugMcp/Services/Resources/DebuggerResourceProvider.cs` — check `AllowedSourcePaths.IsAllowed()`, read file with `File.ReadAllTextAsync()`
- [x] T027 [US4] Wire `AllowedSourcePaths` update on `IProcessDebugger.ModuleLoaded/ModuleUnloaded` in `ResourceNotifier` — enumerate PDB documents via `PdbSymbolCache.GetOrCreateReader()` → `MetadataReader.Documents`, add/remove paths

**Checkpoint**: Source resource functional. Only PDB-referenced files served. Security boundary enforced.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup across all resources

- [x] T028 Verify all existing tests still pass (`dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"`)
- [x] T029 Run full build and verify 0 warnings related to new code (`dotnet build`)
- [x] T030 Verify `--no-roslyn` flag still works correctly with resources (resources should work regardless of Roslyn flag)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1+US2 (Phase 3)**: Depends on Phase 2 completion
- **US3 (Phase 4)**: Depends on Phase 2 completion. Can run in parallel with Phase 3.
- **US4 (Phase 5)**: Depends on Phase 2 completion. Can run in parallel with Phases 3-4.
- **Polish (Phase 6)**: Depends on all user story phases being complete

### User Story Dependencies

- **US1 (Session) + US2 (Breakpoints)**: Combined in Phase 3 because they share the resource provider class and listing infrastructure. No dependency on US3/US4.
- **US3 (Threads)**: Independent of US1/US2. Only needs foundational ThreadSnapshotCache.
- **US4 (Source)**: Independent of US1/US2/US3. Only needs foundational AllowedSourcePaths.

### Within Each Phase

- Tests MUST be written and FAIL before implementation (Constitution Principle III)
- Resource provider methods after tests
- Wiring/registration after implementation
- Logging after core functionality works

### Parallel Opportunities

**Phase 2 (Foundational)**:
- Test tasks T003-T006 can all run in parallel (different files, [P] marker)
- Implementation tasks T007-T010 run sequentially AFTER their corresponding tests pass (TDD Red→Green)
- The three test+impl pairs are parallel *across* pairs: (T003→T007), (T004→T008), (T005→T009), (T006→T010)

**Phase 3 (US1+US2)**:
- T012 (session tests) can run in parallel with T013 (breakpoints tests) and T014 (listing tests)

**Cross-phase**:
- Phase 4 (US3) and Phase 5 (US4) can run in parallel with Phase 3 if foundation is complete

---

## Parallel Example: Phase 2 (Foundational)

```text
# Step 1: All test tasks run in parallel ([P] marker):
T003: tests/DebugMcp.Tests/Unit/Breakpoints/BreakpointRegistryChangedEventTests.cs
T004: tests/DebugMcp.Tests/Unit/Resources/ThreadSnapshotCacheTests.cs
T005: tests/DebugMcp.Tests/Unit/Resources/AllowedSourcePathsTests.cs
T006: tests/DebugMcp.Tests/Unit/Resources/ResourceNotifierTests.cs

# Step 2: Implementation tasks (each makes its corresponding tests pass):
T007: BreakpointRegistry.Changed (makes T003 pass)
T008: ThreadSnapshotCache (makes T004 pass)
T009: AllowedSourcePaths (makes T005 pass)
T010: ResourceNotifier (makes T006 pass)
# Note: T007-T010 can run in parallel with each other (different files),
# but each MUST follow its corresponding test (TDD Red→Green).
```

## Parallel Example: Phase 3 (US1+US2 Tests)

```text
# All test files can be written in parallel:
T012: tests/DebugMcp.Tests/Unit/Resources/SessionResourceTests.cs
T013: tests/DebugMcp.Tests/Unit/Resources/BreakpointsResourceTests.cs
T014: tests/DebugMcp.Tests/Unit/Resources/ResourceListTests.cs
```

---

## Implementation Strategy

### MVP First (US1 + US2)

1. Complete Phase 1: Setup (T001-T002)
2. Complete Phase 2: Foundational (T003-T011)
3. Complete Phase 3: Session + Breakpoints resources (T012-T021)
4. **STOP and VALIDATE**: `resources/list` returns 2 resources, `resources/read` works for both
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1+US2 (Session + Breakpoints) → Test → Deploy (MVP!)
3. Add US3 (Threads) → Test → Deploy
4. Add US4 (Source) → Test → Deploy
5. Polish → Final validation → Release

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution Principle III (Test-First) is NON-NEGOTIABLE — tests before implementation
- SDK bug: `ResourceCollection.Changed` sends wrong notification type — manual notifications via `ResourceNotifier`
- All DateTimeOffset per project convention
- Commit after each task or logical group
