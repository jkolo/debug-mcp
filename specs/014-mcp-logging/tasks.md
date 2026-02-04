# Tasks: MCP Protocol Logging

**Input**: Design documents from `/specs/014-mcp-logging/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required per constitution (III. Test-First principle)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Ensure project structure supports new logging components

- [x] T001 Verify MCP SDK version supports logging capability in DebugMcp/DebugMcp.csproj
- [x] T002 [P] Create Infrastructure directory structure for new logging classes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 Create McpLogLevel enum mapping .NET LogLevel to MCP levels in DebugMcp/Infrastructure/McpLogLevel.cs
- [x] T004 [P] Create LoggingOptions configuration class in DebugMcp/Infrastructure/LoggingOptions.cs
- [x] T005 Declare logging capability in MCP server configuration in DebugMcp/Program.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - MCP Client Receives Debug Logs (Priority: P1) 🎯 MVP

**Goal**: MCP clients receive structured log messages via `notifications/message` when debugger operations occur

**Independent Test**: Connect MCP client, perform debug operations, verify log notifications with correct levels and data

### Tests for User Story 1 (TDD - Red Phase)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T006 [P] [US1] Unit test for McpLogger.Log() sends notification in tests/DebugMcp.Tests/Infrastructure/McpLoggerTests.cs
- [x] T007 [P] [US1] Unit test for McpLoggerProvider.CreateLogger() returns McpLogger in tests/DebugMcp.Tests/Infrastructure/McpLoggerProviderTests.cs
- [x] T008 [P] [US1] Unit test for log level mapping (LogLevel → McpLogLevel) in tests/DebugMcp.Tests/Infrastructure/McpLogLevelTests.cs
- [x] T009 [P] [US1] Unit test for payload truncation at 64KB in tests/DebugMcp.Tests/Infrastructure/McpLoggerTests.cs
- [ ] T010 [US1] E2E test: client receives info log on process attach in tests/DebugMcp.E2E/Features/Logging.feature

### Implementation for User Story 1 (TDD - Green Phase)

- [x] T011 [US1] Implement McpLogger class (ILogger) in DebugMcp/Infrastructure/McpLogger.cs
- [x] T012 [US1] Implement McpLoggerProvider class (ILoggerProvider) in DebugMcp/Infrastructure/McpLoggerProvider.cs
- [x] T013 [US1] Implement payload truncation with [truncated] indicator in McpLogger
- [x] T014 [US1] Implement fire-and-forget async delivery in McpLogger.Log()
- [x] T015 [US1] Register McpLoggerProvider in DI container in DebugMcp/Program.cs
- [x] T016 [US1] Remove or conditionally disable ConsoleLoggerProvider in DebugMcp/Program.cs

**Checkpoint**: User Story 1 complete - MCP clients receive log notifications

---

## Phase 4: User Story 2 - Client Controls Log Verbosity (Priority: P2)

**Goal**: MCP clients can send `logging/setLevel` to filter log messages by severity

**Independent Test**: Set level to "error", perform operations generating debug/info logs, verify only error logs received

### Tests for User Story 2 (TDD - Red Phase)

- [x] T017 [P] [US2] Unit test for log level filtering respects McpServer.LoggingLevel in tests/DebugMcp.Tests/Infrastructure/McpLoggerTests.cs
- [x] T018 [P] [US2] Unit test for default minimum level is "info" in tests/DebugMcp.Tests/Infrastructure/McpLoggerTests.cs
- [ ] T019 [US2] E2E test: setLevel to error filters out info logs in tests/DebugMcp.E2E/Features/Logging.feature

### Implementation for User Story 2 (TDD - Green Phase)

- [x] T020 [US2] Implement log level filtering using McpServer.LoggingLevel in McpLogger.IsEnabled()
- [x] T021 [US2] Set default minimum level to "info" in McpLoggerProvider

**Checkpoint**: User Story 2 complete - clients can control log verbosity

---

## Phase 5: User Story 3 - Backward Compatibility with Stderr (Priority: P3)

**Goal**: CLI flag enables stderr logging alongside or instead of MCP logging

**Independent Test**: Run with `--stderr-logging` flag, verify logs appear on stderr

### Tests for User Story 3 (TDD - Red Phase)

- [x] T022 [P] [US3] Unit test for --stderr-logging CLI option parsing in tests/DebugMcp.Tests/Unit/CliArgumentTests.cs
- [ ] T023 [US3] E2E test: stderr logging with -s flag in tests/DebugMcp.E2E/Features/Logging.feature

### Implementation for User Story 3 (TDD - Green Phase)

- [x] T024 [US3] Add --stderr-logging / -s CLI option using System.CommandLine in DebugMcp/Program.cs
- [x] T025 [US3] Conditionally register ConsoleLoggerProvider based on CLI flag in DebugMcp/Program.cs
- [x] T026 [US3] Ensure both MCP and stderr logging can work simultaneously

**Checkpoint**: User Story 3 complete - stderr fallback available via CLI flag

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T027 [P] Verify all existing log points (15 in Logging.cs) flow through MCP
- [x] T028 [P] Add XML documentation to McpLoggerProvider and McpLogger
- [ ] T029 Run quickstart.md validation with actual MCP client (manual)
- [x] T030 Update CLAUDE.md with logging configuration notes if needed (auto-generated)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - US1 must complete before US2 (level filtering needs logging to work)
  - US3 is independent of US1/US2
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

```
Phase 1: Setup
    ↓
Phase 2: Foundational (T003-T005)
    ↓
Phase 3: US1 - Core Logging (T006-T016) 🎯 MVP
    ↓
Phase 4: US2 - Level Filtering (T017-T021)
    │
Phase 5: US3 - Stderr Fallback (T022-T026) [can start after Phase 2]
    ↓
Phase 6: Polish (T027-T030)
```

### Within Each User Story

1. Tests MUST be written and FAIL before implementation (TDD Red Phase)
2. Implementation makes tests pass (TDD Green Phase)
3. Refactor if needed (TDD Refactor Phase)

### Parallel Opportunities

**Phase 2** (after T003):
- T004 can run parallel to T005

**Phase 3 Tests** (all parallel):
- T006, T007, T008, T009 can all run in parallel

**Phase 4 Tests**:
- T017, T018 can run in parallel

**Phase 5 Tests**:
- T022 can start immediately after Phase 2

**Phase 6**:
- T027, T028 can run in parallel

---

## Parallel Example: User Story 1 Tests

```bash
# Launch all US1 tests together (TDD Red Phase):
Task: "Unit test for McpLogger.Log() in tests/DebugMcp.Tests/Infrastructure/McpLoggerTests.cs"
Task: "Unit test for McpLoggerProvider.CreateLogger() in tests/DebugMcp.Tests/Infrastructure/McpLoggerProviderTests.cs"
Task: "Unit test for log level mapping in tests/DebugMcp.Tests/Infrastructure/McpLogLevelTests.cs"
Task: "Unit test for payload truncation in tests/DebugMcp.Tests/Infrastructure/McpLoggerTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (Tests → Implementation)
4. **STOP and VALIDATE**: Test with real MCP client (e.g., Claude Desktop)
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy (MVP!)
3. Add User Story 2 → Test independently → Deploy
4. Add User Story 3 → Test independently → Deploy
5. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- TDD workflow: Red (failing test) → Green (make pass) → Refactor
- Constitution requires Test-First - all implementation follows tests
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
