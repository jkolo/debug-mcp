# Tasks: Unified Debugging Timeline

**Input**: Design documents from `specs/032-unified-debugging-timeline/`
**Branch**: `032-unified-debugging-timeline`

**Organization**: Tasks grouped by user story. Constitution requires TDD — test tasks appear before implementation tasks within each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no blocking dependencies)
- **[Story]**: User story label (US1/US2/US3)

---

## Phase 1: Setup

**Purpose**: Create model files needed by all stories.

- [X] T001 [P] Create `DebugMcp/Models/Timeline/TimelineEventType.cs` — enum with 11 values: `SessionStarted`, `BreakpointHit`, `TracepointHit`, `ExceptionFirstChance`, `ExceptionUserUnhandled`, `ModuleLoaded`, `ThreadStarted`, `ThreadExited`, `StdoutWritten`, `StderrWritten`, `SessionEnded`
- [X] T002 [P] Create `DebugMcp/Models/Timeline/TimelineEventPayload.cs` — abstract record + 9 concrete subtypes: `SessionStartedPayload(string SessionType, int Pid)`, `BreakpointHitPayload(string BreakpointId, string File, int Line)`, `TracepointHitPayload(string TracepointId, string File, int Line)`, `ExceptionPayload(string ExceptionType, string Message, bool IsUserUnhandled)`, `ModuleLoadedPayload(string ModuleName, string? AssemblyPath, bool HasSymbols)`, `ThreadStartedPayload(string? ThreadName)`, `ThreadExitedPayload(string? ThreadName)`, `OutputPayload(string Content, bool Truncated, string Stream)`, `SessionEndedPayload(string Reason)`
- [X] T003 [P] Create `DebugMcp/Models/Timeline/TimelineEvent.cs` — positional record: `TimelineEvent(int EventId, DateTimeOffset Timestamp, TimelineEventType EventType, int? ThreadId, TimelineEventPayload Payload)`
- [X] T004 [P] Create `DebugMcp/Models/Timeline/TimelineFilter.cs` — positional record: `TimelineFilter(string[]? EventTypes, int? ThreadId, int? FromEventId, int MaxEvents)` with default MaxEvents=200
- [X] T005 [P] Create `DebugMcp/Models/Timeline/TimelineResponse.cs` — positional record: `TimelineResponse(IReadOnlyList<TimelineEvent> Events, int TotalEvents, int EventsDropped, string? SessionId)`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add new events and interface stubs required by all user stories.

**⚠️ CRITICAL**: Phase 3+ cannot begin until T006–T010 are complete.

- [X] T006 Add `ThreadCreated` and `ThreadExited` events to `DebugMcp/Services/IProcessDebugger.cs`: add `event EventHandler<ThreadCreatedEventArgs>? ThreadCreated` and `event EventHandler<ThreadExitedEventArgs>? ThreadExited`; add `sealed class ThreadCreatedEventArgs : EventArgs { public required int ThreadId { get; init; } }` and `sealed class ThreadExitedEventArgs : EventArgs { public required int ThreadId { get; init; } }` at the bottom of `IProcessDebugger.cs`
- [X] T007 Fire `ThreadCreated` and `ThreadExited` from `DebugMcp/Services/ProcessDebugger.cs`: in `OnCreateThread` callback (line ~3086), after `Continue(false)`, add `ThreadCreated?.Invoke(this, new ThreadCreatedEventArgs { ThreadId = (int)e.Thread.Id });`; do the same for `OnExitThread` callback (~line 3093) with `ThreadExited`
- [X] T008 Add `OutputReceived` event to `DebugMcp/Services/ProcessIoManager.cs`: add `public event Action<string, string>? OutputReceived;` field; in `PumpStreamAsync`, after appending each line to the buffer, fire `OutputReceived?.Invoke(truncatedContent, streamName)` where `streamName` is `"stdout"` or `"stderr"` and content is truncated at 1024 chars with `truncated` flag tracked
- [X] T009 Create `DebugMcp/Services/Timeline/ITimelineStore.cs` — interface with methods: `void Record(TimelineEvent e)`, `TimelineResponse GetAll()`, `TimelineResponse GetFiltered(TimelineFilter filter)`, `void Clear()`, properties `int TotalRecorded`, `int EventsDropped`
- [X] T010 Create stub `DebugMcp/Services/Timeline/TimelineStore.cs` implementing `ITimelineStore` — all methods throw `NotImplementedException`; constructor signature: `TimelineStore(BreakpointManager? breakpointManager, IProcessDebugger? processDebugger, ProcessIoManager? ioManager, ILogger<TimelineStore> logger)`

**Checkpoint**: `dotnet build` passes 0 errors, 0 warnings. Existing tests green.

---

## Phase 3: User Story 1 — Read Unified Event Stream (P1) 🎯 MVP

**Goal**: Agent reads `debugger://timeline` and sees all 11 event types in chronological order.

**Independent Test**: See `quickstart.md` Scenario 1 — start session, hit breakpoint, throw exception, print stdout, read timeline.

### Tests for User Story 1 (TDD — write first, verify they FAIL)

- [X] T011 [P] [US1] Unit test — `TimelineStore` records `SessionStarted` event (with session_type + pid) when `StateChanged(Running)` fires for first time, and `SessionEnded` + `Clear()` when `StateChanged(Disconnected)` fires, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreEventTests.cs`
- [X] T012 [P] [US1] Unit test — `TimelineStore` records `BreakpointHit` event (with breakpoint_id, file, line, thread_id) from `BreakpointManager.BreakpointResolved` when ID starts with `"bp-"`, and `TracepointHit` when ID starts with `"tp-"`, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreEventTests.cs`
- [X] T013 [P] [US1] Unit test — `TimelineStore` records `ExceptionFirstChance` / `ExceptionUserUnhandled` events (with exception_type, message, thread_id) from `IProcessDebugger.ExceptionHit`, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreEventTests.cs`
- [X] T014 [P] [US1] Unit test — `TimelineStore` records `ModuleLoaded`, `ThreadStarted`, `ThreadExited`, `StdoutWritten`, `StderrWritten` events from `ModuleLoaded`, `ThreadCreated`, `ThreadExited`, `OutputReceived` respectively, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreEventTests.cs`
- [X] T015 [P] [US1] Unit test — `TimelineStore.GetAll()` returns events with monotonically increasing `EventId` starting at 1; `TotalEvents` reflects total recorded; `EventsDropped` is 0 when under cap, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreCapTests.cs`
- [X] T016 [P] [US1] Unit test — cap: when 10,001 events recorded, oldest is evicted, `EventsDropped == 1`, `Events.Count == 10_000`; new events always visible, oldest dropped, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreCapTests.cs`

### Implementation for User Story 1

- [X] T017 [US1] Implement `TimelineStore` constructor: subscribe to `IProcessDebugger.StateChanged`, `BreakpointHit`, `ModuleLoaded`, `ExceptionHit`, `ThreadCreated`, `ThreadExited`; subscribe to `BreakpointManager.BreakpointResolved`; subscribe to `ProcessIoManager.OutputReceived`; all sources nullable (graceful degradation when not provided) in `DebugMcp/Services/Timeline/TimelineStore.cs`
- [X] T018 [US1] Implement `TimelineStore` event handler for `StateChanged`: on `Running` (when previous state was not `Running`), record `SessionStarted`; on `Disconnected`, record `SessionEnded` with reason, then call `Clear()` in `DebugMcp/Services/Timeline/TimelineStore.cs`
- [X] T019 [US1] Implement `TimelineStore` event handlers for `BreakpointResolved` (determine type from ID prefix `bp-`/`tp-`), `ExceptionHit` (determine `IsUserUnhandled` from `IsFirstChance`), `ModuleLoaded` (extract module name from path), `ThreadCreated`, `ThreadExited`, `OutputReceived` (truncate content at 1024 chars), in `DebugMcp/Services/Timeline/TimelineStore.cs`
- [X] T020 [US1] Implement `TimelineStore.Append(TimelineEvent)`: `Interlocked.Increment` on `_nextEventId`, cap check — if `_events.Count >= MaxCapacity` then `TryDequeue` + `Interlocked.Increment(_eventsDropped)`, then `Enqueue`; implement `GetAll()` returning snapshot of queue as `TimelineResponse`; implement `Clear()` resetting queue and dropped counter in `DebugMcp/Services/Timeline/TimelineStore.cs`
- [X] T021 [US1] Add `debugger://timeline` resource method to `DebugMcp/Services/Resources/DebuggerResourceProvider.cs`: inject `ITimelineStore? timelineStore` as optional constructor parameter; add `[McpServerResource(UriTemplate = "debugger://timeline", Name = "Debugging Timeline", MimeType = "application/json")]` method `GetTimelineJson()` that calls `_timelineStore?.GetAll()` and serializes result (returns empty response if no store)
- [X] T022 [US1] Register `TimelineStore` singleton in `DebugMcp/Program.cs`: `builder.Services.AddSingleton<ITimelineStore>(sp => new TimelineStore(sp.GetService<BreakpointManager>(), sp.GetService<IProcessDebugger>(), sp.GetService<ProcessIoManager>(), sp.GetRequiredService<ILogger<TimelineStore>>()))` ; pass `ITimelineStore` to `DebuggerResourceProvider` registration

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~Timeline"` green; `quickstart.md` Scenario 1 passes manually.

---

## Phase 4: User Story 2 — Cross-Event Correlation (P2)

**Goal**: Verify that timeline event payloads contain all cross-reference fields needed for multi-event correlation.

**Independent Test**: Read timeline, verify breakpoint_hit and exception events share thread_id with thread_started event — see `quickstart.md` Scenario 2.

**Note**: No new production code needed. Phase 4 adds targeted tests proving payload completeness.

### Tests for User Story 2 (TDD — write first, verify they FAIL before Phase 3 implementation)

- [X] T023 [P] [US2] Unit test — `BreakpointHit` payload contains `BreakpointId` (non-empty string), `File` (non-null), `Line > 0`; event `ThreadId` is non-null and matches the thread that fired `BreakpointResolved`, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreEventTests.cs`
- [X] T024 [P] [US2] Unit test — `ExceptionFirstChance` payload contains non-null `ExceptionType` and `Message`; event `ThreadId` matches `ExceptionHitEventArgs.ThreadId`, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreEventTests.cs`
- [X] T025 [P] [US2] Unit test — when `StdoutWritten` and `ExceptionFirstChance` are recorded for the same thread, both events carry the same `ThreadId` value in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreEventTests.cs`

**Checkpoint**: All US2 tests green after Phase 3 implementation; no additional code required.

---

## Phase 5: User Story 3 — Filtered Timeline Reads (P3)

**Goal**: `timeline_query` tool returns only the requested subset of timeline events.

**Independent Test**: See `quickstart.md` Scenario 3 — run `timeline_query` with event_type filter, thread_id filter, cursor.

### Tests for User Story 3 (TDD — write first, verify they FAIL)

- [X] T026 [P] [US3] Unit test — `GetFiltered` with `EventTypes = ["breakpoint_hit"]` returns only `BreakpointHit` events from a mixed timeline, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreFilterTests.cs`
- [X] T027 [P] [US3] Unit test — `GetFiltered` with `ThreadId = 42` returns only events where `event.ThreadId == 42` (events with null ThreadId excluded), in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreFilterTests.cs`
- [X] T028 [P] [US3] Unit test — `GetFiltered` with `FromEventId = 5` returns only events with `EventId >= 5`; `MaxEvents = 3` caps result at 3 events, in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreFilterTests.cs`
- [X] T029 [P] [US3] Unit test — `GetFiltered` with null filters returns same events as `GetAll()` (no filtering applied when all params null/default), in `tests/DebugMcp.Tests/Unit/Timeline/TimelineStoreFilterTests.cs`

### Implementation for User Story 3

- [X] T030 [US3] Implement `TimelineStore.GetFiltered(TimelineFilter filter)` in `DebugMcp/Services/Timeline/TimelineStore.cs`: take snapshot from queue, apply: event_type filter (case-insensitive match on enum name), thread_id filter, from_event_id cursor, max_events limit (default 200, max 1000); return `TimelineResponse` with filtered events + original total/dropped counters
- [X] T031 [US3] Create `DebugMcp/Tools/TimelineQueryTool.cs` — `[McpServerToolType]`, method `TimelineQueryAsync` with `[McpServerTool(Name = "timeline_query", Title = "Query Timeline", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]`; inject `ITimelineStore`; parse `eventTypes` (JSON array string or null), `threadId`, `fromEventId`, `maxEvents` params; build `TimelineFilter`, call `GetFiltered`, serialize and return JSON; description must contain `"timeline"` and include `{"success": true, ...}` example

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~Timeline"` green; `quickstart.md` Scenario 3 passes.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T032 [P] Contract test — add `timeline_query` entry to `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs`: `["timeline_query"] = new("Query Timeline", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false)`; update `ExpectedAnnotations_Covers37Tools` assertion from 36 → 37; add `"timeline_query"` to `EnhancedDescriptionTools` set
- [X] T033 [P] Add structured logging to `DebugMcp/Services/Timeline/TimelineStore.cs`: `LogInformation` on session start (session type, pid); `LogDebug` per event type recorded; `LogWarning` when cap reached and events dropped (include drop count); `LogInformation` on session end
- [X] T034 [P] Update `CLAUDE.md` "Active Technologies" and "Recent Changes" sections: add 032 entry — `TimelineStore`, `ITimelineStore`, `debugger://timeline` resource, `timeline_query` tool, `ThreadCreated`/`ThreadExited` events, `OutputReceived` event; update tool count to 37

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — T001–T005 fully parallel
- **Phase 2 (Foundational)**: Depends on Phase 1 completion — **blocks Phase 3+**
- **Phase 3 (US1)**: Depends on Phase 2; T011–T016 (tests) parallel before T017+
- **Phase 4 (US2)**: T023–T025 can be written in parallel with Phase 3 implementation (different test file lines); GREEN only after Phase 3 implementation
- **Phase 5 (US3)**: Depends on Phase 3 (reuses `TimelineStore`); T026–T029 tests parallel before T030+
- **Phase 6 (Polish)**: T032–T034 fully parallel after Phase 5

### User Story Dependencies

- **US2 (P2)**: No new code — passes after Phase 3 GREEN
- **US3 (P3)**: Needs `TimelineStore` from US1 — start Phase 5 after T022 (registration) is complete

### Within Each Story

- Tests → FAIL verification → Implementation → Checkpoint
- TDD cycle: RED (T011–T016, T026–T029) → GREEN (implementation) → REFACTOR

### Parallel Opportunities

- Phase 1: T001–T005 all parallel (different files)
- Phase 3 tests: T011–T016 all parallel (different test methods, same file OK per xUnit)
- Phase 4 tests: T023–T025 all parallel
- Phase 5 tests: T026–T029 all parallel
- Phase 6: T032–T034 all parallel

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T005) — model files
2. Complete Phase 2: Foundational (T006–T010) — new events + stubs
3. Complete Phase 3: User Story 1 (T011–T022)
4. **STOP and VALIDATE**: Run `quickstart.md` Scenario 1
5. Feature is usable — agents can read unified timeline

### Incremental Delivery

1. Phase 1 + Phase 2 → build passes (foundation ready)
2. Phase 3 → `debugger://timeline` works (Scenario 1) — **ship-worthy MVP**
3. Phase 4 → cross-event payload completeness verified (Scenario 2)
4. Phase 5 → `timeline_query` filtering works (Scenario 3) — **production-ready**
5. Phase 6 → contract tests, logging, docs

---

## Notes

- 34 tasks total: 5 setup, 5 foundational, 12 US1, 3 US2, 6 US3, 3 polish
- TDD mandatory (constitution principle III): tests in each story MUST fail before implementation
- `TimelineStore` takes `BreakpointManager` (concrete) for `BreakpointResolved` event subscription, `IProcessDebugger` (interface) for other events — same pattern as `BatchRunner` in feature 031
- Breakpoint type determined by ID prefix: `bp-` → `BreakpointHit`, `tp-` → `TracepointHit`
- `ModuleLoadedEventArgs` does NOT include `HasSymbols` — always record `false`; agents needing symbol status should read `debugger://modules`
- Each Checkpoint: run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` and verify green
