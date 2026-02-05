# Tasks: MCP Breakpoint Notifications

**Input**: Design documents from `/specs/016-breakpoint-notifications/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included per constitution (III. Test-First)

**Organization**: Tasks grouped by user story. US1 (Notifications) is foundational for US2-US5 as it establishes the notification infrastructure.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1-US5 maps to User Stories from spec.md
- Exact file paths included

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add models and interfaces for breakpoint notifications

- [x] T001 [P] Create BreakpointType enum in DebugMcp/Models/Breakpoints/BreakpointType.cs
- [x] T002 [P] Create BreakpointNotification record in DebugMcp/Models/Breakpoints/BreakpointNotification.cs
- [x] T003 [P] Create IBreakpointNotifier interface in DebugMcp/Services/Breakpoints/IBreakpointNotifier.cs
- [x] T004 Extend Breakpoint model with Type, LogMessage, HitCountMultiple, MaxNotifications, NotificationsSent in DebugMcp/Models/Breakpoints/Breakpoint.cs

---

## Phase 2: Foundational - User Story 1: Breakpoint Hit Notifications (Priority: P1) 🎯 MVP

**Goal**: Send MCP notification when any blocking breakpoint is hit

**Independent Test**: Set breakpoint, continue execution, verify notification received without calling `breakpoint_wait`

**⚠️ CRITICAL**: Notification infrastructure must be complete before US2-US5 can begin

### Tests for US1

- [x] T005 [P] [US1] Write unit test NotificationTests.SendNotification_BreakpointHit_SendsMcpNotification in tests/DebugMcp.Tests/Unit/Breakpoints/NotificationTests.cs
- [x] T006 [P] [US1] Write unit test NotificationTests.SendNotification_MultipleBreakpoints_SendsOnlyHitBreakpoint in tests/DebugMcp.Tests/Unit/Breakpoints/NotificationTests.cs
- [x] T007 [P] [US1] Write unit test NotificationTests.SendNotification_WithBreakpointWait_BothWork in tests/DebugMcp.Tests/Unit/Breakpoints/NotificationTests.cs
- [ ] ~~T008 [P] [US1] Write E2E test in tests/DebugMcp.E2E/Features/Breakpoints/BreakpointNotification.feature: Scenario "Receive notification on breakpoint hit"~~ — **DESCOPED**: MCP notifications cannot be captured in Reqnroll E2E tests (no client-side notification listener). Validated manually via MCP tools.

### Implementation for US1

- [x] T009 [US1] Implement BreakpointNotifier class with Channel<T> queue in DebugMcp/Infrastructure/BreakpointNotifier.cs
- [x] T010 [US1] Add SendBreakpointHitAsync method using SendNotificationAsync("debugger/breakpointHit", ...) in DebugMcp/Infrastructure/BreakpointNotifier.cs
- [x] T011 [US1] Extend BreakpointManager to call notifier on breakpoint hit in DebugMcp/Services/Breakpoints/BreakpointManager.cs
- [x] T012 [US1] Register IBreakpointNotifier as singleton in DebugMcp/Program.cs
- [x] T013 [US1] Add structured logging for notification events in DebugMcp/Infrastructure/BreakpointNotifier.cs

**Checkpoint**: Blocking breakpoints now send MCP notifications. `breakpoint_wait` still works. US2-US5 can begin.

---

## Phase 3: User Story 2 - Notify-Only Tracepoints (Priority: P1)

**Goal**: Create tracepoints that send notifications without pausing execution

**Independent Test**: Set tracepoint in loop, verify 10 notifications received, execution never paused

### Tests for US2

- [x] T014 [P] [US2] Write unit test TracepointTests.SetTracepoint_ReturnsUniqueId in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [x] T015 [P] [US2] Write unit test TracepointTests.TracepointHit_SendsNotification_ContinuesExecution in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [x] T016 [P] [US2] Write unit test TracepointTests.TracepointInLoop_SendsMultipleNotifications in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [ ] ~~T017 [P] [US2] Write E2E test in tests/DebugMcp.E2E/Features/Breakpoints/Tracepoint.feature: Scenario "Tracepoint sends notification without pausing"~~ — **DESCOPED**: see T008

### Implementation for US2

- [x] T018 [US2] Add SetTracepointAsync method to IBreakpointManager in DebugMcp/Services/Breakpoints/IBreakpointManager.cs
- [x] T019 [US2] Implement SetTracepointAsync in BreakpointManager (creates breakpoint with Type=Tracepoint) in DebugMcp/Services/Breakpoints/BreakpointManager.cs
- [x] T020 [US2] Modify breakpoint callback to check Type and Continue() immediately for tracepoints in DebugMcp/Services/Breakpoints/BreakpointManager.cs
- [x] T021 [US2] Create TracepointSetTool with [McpServerTool(Name = "tracepoint_set")] in DebugMcp/Tools/TracepointSetTool.cs
- [x] T022 [US2] Add tool logging with ToolInvoked/ToolCompleted in DebugMcp/Tools/TracepointSetTool.cs

**Checkpoint**: tracepoint_set tool functional. Tracepoints don't pause execution.

---

## Phase 4: User Story 3 - Log Message with Expressions (Priority: P1)

**Goal**: Tracepoints can include log messages with evaluated expressions

**Independent Test**: Create tracepoint with "Counter is {i}", verify notification contains "Counter is 42"

### Tests for US3

- [x] T023 [P] [US3] Write unit test TracepointTests.LogMessage_SingleExpression_EvaluatesCorrectly in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [x] T024 [P] [US3] Write unit test TracepointTests.LogMessage_MultipleExpressions_EvaluatesAll in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [x] T025 [P] [US3] Write unit test TracepointTests.LogMessage_ExpressionError_IncludesErrorMarker in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [ ] ~~T026 [P] [US3] Write E2E test in tests/DebugMcp.E2E/Features/Breakpoints/Tracepoint.feature: Scenario "Tracepoint evaluates log message expressions"~~ — **DESCOPED**: see T008

### Implementation for US3

- [x] T027 [US3] Create LogMessageEvaluator service with EvaluateLogMessageAsync method in DebugMcp/Services/Breakpoints/LogMessageEvaluator.cs
- [x] T028 [US3] Implement {expression} regex parsing in LogMessageEvaluator in DebugMcp/Services/Breakpoints/LogMessageEvaluator.cs
- [x] T029 [US3] Integrate with existing expression evaluator infrastructure in DebugMcp/Services/Breakpoints/LogMessageEvaluator.cs
- [x] T030 [US3] Add error handling with <error: ExceptionType> format in DebugMcp/Services/Breakpoints/LogMessageEvaluator.cs
- [x] T031 [US3] Call LogMessageEvaluator in tracepoint callback before sending notification in DebugMcp/Services/Breakpoints/BreakpointManager.cs
- [x] T032 [US3] Add log_message parameter to tracepoint_set tool in DebugMcp/Tools/TracepointSetTool.cs

**Checkpoint**: Tracepoints can capture variable values via log messages.

---

## Phase 5: User Story 4 - Tracepoint Lifecycle Management (Priority: P2)

**Goal**: List, enable/disable, and remove tracepoints using existing breakpoint tools

**Independent Test**: Create tracepoint, list shows it with type="tracepoint", disable stops notifications, enable resumes

### Tests for US4

- [x] T033 [P] [US4] Write unit test TracepointTests.BreakpointList_IncludesTracepointsWithType in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [x] T034 [P] [US4] Write unit test TracepointTests.DisableTracepoint_StopsNotifications in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [x] T035 [P] [US4] Write unit test TracepointTests.EnableTracepoint_ResumesNotifications in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [x] T036 [P] [US4] Write unit test TracepointTests.RemoveTracepoint_DeletesAndStopsNotifications in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs

### Implementation for US4

- [x] T037 [US4] Extend BreakpointListTool to include "type" field in output in DebugMcp/Tools/BreakpointListTool.cs
- [x] T038 [US4] Verify breakpoint_enable works for tracepoints (should work via existing ID lookup) in DebugMcp/Services/Breakpoints/BreakpointManager.cs
- [x] T039 [US4] Verify breakpoint_remove works for tracepoints (should work via existing ID lookup) in DebugMcp/Services/Breakpoints/BreakpointManager.cs
- [x] T040 [US4] Add Enabled check before sending tracepoint notifications in DebugMcp/Services/Breakpoints/BreakpointManager.cs

**Checkpoint**: Tracepoints fully manageable via existing breakpoint_* tools.

---

## Phase 6: User Story 5 - Notification Frequency Filtering (Priority: P3)

**Goal**: Limit tracepoint notification frequency for hot code paths

**Independent Test**: Create tracepoint with hit_count_multiple=100, run 500 iterations, verify only 5 notifications

### Tests for US5

- [x] T041 [P] [US5] Write unit test TracepointTests.HitCountMultiple_NotifiesEveryNthHit in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [x] T042 [P] [US5] Write unit test TracepointTests.MaxNotifications_AutoDisablesAfterLimit in tests/DebugMcp.Tests/Unit/Breakpoints/TracepointTests.cs
- [ ] ~~T043 [P] [US5] Write E2E test in tests/DebugMcp.E2E/Features/Breakpoints/Tracepoint.feature: Scenario "Tracepoint filters by hit count"~~ — **DESCOPED**: see T008

### Implementation for US5

- [x] T044 [US5] Implement ShouldNotify logic with HitCountMultiple check in BreakpointManager in DebugMcp/Services/Breakpoints/BreakpointManager.cs
- [x] T045 [US5] Implement MaxNotifications check and auto-disable in BreakpointManager in DebugMcp/Services/Breakpoints/BreakpointManager.cs
- [x] T046 [US5] Add hit_count_multiple and max_notifications parameters to tracepoint_set in DebugMcp/Tools/TracepointSetTool.cs
- [x] T047 [US5] Increment NotificationsSent counter on each sent notification in DebugMcp/Services/Breakpoints/BreakpointManager.cs

**Checkpoint**: Tracepoints can filter high-frequency notifications.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, cleanup, and final validation

- [x] T048 [P] Update website/docs/tools/ with tracepoint_set documentation
- [x] T049 [P] Update website/docs/tools/ with notification documentation
- [x] T050 Run all E2E tests and verify acceptance scenarios from spec.md
- [x] T051 Verify quickstart.md examples work correctly
- [x] T052 Performance validation: verify SC-001 (<100ms notification) and SC-002 (<5ms overhead)
- [x] T053 Backward compatibility: verify all existing breakpoint tests still pass

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
     ↓
Phase 2 (US1: Notifications) ← BLOCKS US2-US5 (notification infrastructure)
     ↓
┌────┴────┬────────┬────────┐
↓         ↓        ↓        ↓
Phase 3   Phase 4  Phase 5  Phase 6
(US2)     (US3)    (US4)    (US5)
  ↓         ↓        ↓        ↓
  └────┬────┴────────┴────────┘
       ↓
   Phase 7 (Polish)
```

### User Story Dependencies

- **US1 (P1)**: Foundational - must complete first (establishes notification system)
- **US2 (P1)**: Depends on US1 - needs notification infrastructure to send tracepoint events
- **US3 (P1)**: Depends on US2 - needs tracepoint creation to add log messages
- **US4 (P2)**: Depends on US2 - needs tracepoints to exist for lifecycle management
- **US5 (P3)**: Depends on US2 - needs tracepoints to exist for filtering

### Parallel Opportunities Within Phases

- Phase 1: All T001-T003 can run in parallel
- Phase 2 Tests: T005-T008 can run in parallel
- Phase 3 Tests: T014-T017 can run in parallel
- Phase 4 Tests: T023-T026 can run in parallel
- Phase 5 Tests: T033-T036 can run in parallel
- Phase 6 Tests: T041-T043 can run in parallel
- Phase 7: T048-T049 can run in parallel

---

## Parallel Example: Phase 3 (User Story 2)

```bash
# Launch all tests for US2 together:
Task: "Write unit test TracepointTests.SetTracepoint_ReturnsUniqueId"
Task: "Write unit test TracepointTests.TracepointHit_SendsNotification_ContinuesExecution"
Task: "Write unit test TracepointTests.TracepointInLoop_SendsMultipleNotifications"
Task: "Write E2E test Tracepoint.feature: Scenario Tracepoint sends notification without pausing"
```

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1: Setup models and interfaces
2. Complete Phase 2: US1 - Notification infrastructure
3. Complete Phase 3: US2 - Basic tracepoints
4. **STOP and VALIDATE**: Test notifications work for both breakpoints and tracepoints
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + US1 → Breakpoint notifications working
2. Add US2 → Tracepoints working (basic)
3. Add US3 → Tracepoints with log messages
4. Add US4 → Full lifecycle management
5. Add US5 → Frequency filtering for hot paths

---

## Notes

- Constitution requires Test-First (III): Write tests BEFORE implementation
- All notifications use fire-and-forget pattern (research.md decision)
- Tracepoints reuse existing ICorDebug breakpoint mechanism
- Expression evaluation reuses existing EvaluateTool infrastructure
- Backward compatibility maintained: `breakpoint_wait` continues to work

---

## Known Issues Found During Testing

### FEATURE-001: Breakpoint persistence across sessions
**Status**: Out-of-scope for 016. Tracked separately for future work.
**Summary**: Breakpoints persist across debug sessions but aren't rebound on new session start. Needs `clearBreakpoints` parameter on disconnect + rebinding on session start.

### BUG-002: MCP operations hang when process is in running state
**Status**: RESOLVED by feature 017 (Process I/O Redirection).
**Fix**: `ProcessIoManager` redirects debuggee stdin/stdout/stderr to internal buffers, preventing interference with MCP's JSON-RPC transport.
