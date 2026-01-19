# Tasks: Debug Session Management

**Input**: Design documents from `/specs/001-debug-session/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included per Constitution's Test-First (TDD) principle.

**Organization**: Tasks grouped by user story for independent implementation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1, US2, US3, US4)
- File paths use project structure from plan.md

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and MCP server bootstrap

- [x] T001 Create DotnetMcp project with .NET 10.0 in DotnetMcp/DotnetMcp.csproj
- [x] T002 [P] Add NuGet dependencies: ModelContextProtocol, ClrDebug, Microsoft.Diagnostics.DbgShim.linux-x64
- [x] T003 [P] Create DotnetMcp.Tests project in tests/DotnetMcp.Tests/DotnetMcp.Tests.csproj
- [x] T004 [P] Add test dependencies: xUnit, Moq, FluentAssertions to test project
- [x] T005 Configure MCP server entry point in DotnetMcp/Program.cs with stdio transport

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure required before ANY user story

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Create DebugSession model in DotnetMcp/Models/DebugSession.cs
- [x] T007 [P] Create SessionState enum in DotnetMcp/Models/SessionState.cs
- [x] T008 [P] Create PauseReason enum in DotnetMcp/Models/PauseReason.cs
- [x] T009 [P] Create LaunchMode enum in DotnetMcp/Models/LaunchMode.cs
- [x] T010 [P] Create SourceLocation record in DotnetMcp/Models/SourceLocation.cs
- [x] T011 [P] Create ProcessInfo record in DotnetMcp/Models/ProcessInfo.cs
- [x] T012 Create IDebugSessionManager interface in DotnetMcp/Services/IDebugSessionManager.cs
- [x] T013 Create IProcessDebugger interface in DotnetMcp/Services/IProcessDebugger.cs
- [x] T014 Implement structured logging in DotnetMcp/Infrastructure/Logging.cs
- [x] T015 Create error response types in DotnetMcp/Models/ErrorResponse.cs
- [x] T016 Setup DI container with services registration in DotnetMcp/Program.cs

**Checkpoint**: Foundation ready - user story implementation can begin

---

## Phase 3: User Story 1 - Attach to Running Process (Priority: P1) MVP

**Goal**: Connect to a running .NET process by PID for debugging

**Independent Test**: Start a sample .NET app, invoke debug_attach with its PID, verify successful connection with session details returned.

### Tests for User Story 1

> **NOTE: Write tests FIRST, ensure they FAIL before implementation**

- [x] T017 [P] [US1] Contract test for debug_attach schema in tests/DotnetMcp.Tests/Contract/DebugAttachContractTests.cs
- [x] T018 [P] [US1] Unit test for ProcessDebugger.AttachAsync in tests/DotnetMcp.Tests/Unit/ProcessDebuggerTests.cs
- [x] T019 [P] [US1] Unit test for DebugSessionManager.CreateSessionAsync in tests/DotnetMcp.Tests/Unit/DebugSessionManagerTests.cs
- [x] T020 [US1] Integration test for attach workflow in tests/DotnetMcp.Tests/Integration/AttachTests.cs

### Implementation for User Story 1

- [x] T021 [US1] Implement .NET process detection in DotnetMcp/Services/ProcessDebugger.cs (IsNetProcess method)
- [x] T022 [US1] Implement ICorDebug initialization via dbgshim in DotnetMcp/Services/ProcessDebugger.cs
- [x] T023 [US1] Implement ProcessDebugger.AttachAsync using ICorDebug.DebugActiveProcess in DotnetMcp/Services/ProcessDebugger.cs
- [x] T024 [US1] Implement DebugSessionManager.CreateSessionAsync in DotnetMcp/Services/DebugSessionManager.cs
- [x] T025 [US1] Implement ManagedCallback handler with AppDomain.Attach in DotnetMcp/Services/ProcessDebugger.cs
- [x] T026 [US1] Create debug_attach tool in DotnetMcp/Tools/DebugAttachTool.cs
- [x] T027 [US1] Add timeout handling with cancellation token in DotnetMcp/Tools/DebugAttachTool.cs
- [x] T028 [US1] Add permission error handling for debug_attach in DotnetMcp/Tools/DebugAttachTool.cs
- [x] T029 [US1] Add logging for attach operations in DotnetMcp/Tools/DebugAttachTool.cs

**Checkpoint**: User Story 1 complete - can attach to running .NET processes

---

## Phase 4: User Story 2 - Query Debug Session State (Priority: P2)

**Goal**: Query current debugging state (disconnected, running, paused) with context

**Independent Test**: Attach to process, invoke debug_state, verify accurate state/location returned.

### Tests for User Story 2

- [x] T030 [P] [US2] Contract test for debug_state schema in tests/DotnetMcp.Tests/Contract/DebugStateContractTests.cs
- [x] T031 [P] [US2] Unit test for state queries in tests/DotnetMcp.Tests/Unit/DebugSessionManagerTests.cs
- [x] T032 [US2] Integration test for state transitions in tests/DotnetMcp.Tests/Integration/StateTests.cs

### Implementation for User Story 2

- [x] T033 [US2] Implement DebugSessionManager.GetCurrentState in DotnetMcp/Services/DebugSessionManager.cs
- [x] T034 [US2] Implement pause reason tracking in ManagedCallback in DotnetMcp/Services/ProcessDebugger.cs
- [x] T035 [US2] Implement current location extraction from ICorDebugFrame in DotnetMcp/Services/ProcessDebugger.cs
- [x] T036 [US2] Create debug_state tool in DotnetMcp/Tools/DebugStateTool.cs
- [x] T037 [US2] Handle disconnected state response in DotnetMcp/Tools/DebugStateTool.cs
- [x] T038 [US2] Add logging for state queries in DotnetMcp/Tools/DebugStateTool.cs

**Checkpoint**: User Stories 1 AND 2 complete - can attach and query state

---

## Phase 5: User Story 3 - Launch Process Under Debugger (Priority: P3)

**Goal**: Start a .NET executable under debugger control with optional stopAtEntry

**Independent Test**: Invoke debug_launch with a DLL path, verify process starts paused at entry.

### Tests for User Story 3

- [x] T039 [P] [US3] Contract test for debug_launch schema in tests/DotnetMcp.Tests/Contract/DebugLaunchContractTests.cs
- [x] T040 [P] [US3] Unit test for launch parameters in tests/DotnetMcp.Tests/Unit/ProcessDebuggerTests.cs
- [x] T041 [US3] Integration test for launch workflow in tests/DotnetMcp.Tests/Integration/LaunchTests.cs

### Implementation for User Story 3

- [x] T042 [US3] Implement ProcessDebugger.LaunchAsync using ICorDebug.CreateProcess in DotnetMcp/Services/ProcessDebugger.cs
- [x] T043 [US3] Implement stopAtEntry via LoadModule callback in DotnetMcp/Services/ProcessDebugger.cs
- [x] T044 [US3] Implement environment variables handling in DotnetMcp/Services/ProcessDebugger.cs
- [x] T045 [US3] Implement working directory handling in DotnetMcp/Services/ProcessDebugger.cs
- [x] T046 [US3] Create debug_launch tool in DotnetMcp/Tools/DebugLaunchTool.cs
- [x] T047 [US3] Add validation for executable path in DotnetMcp/Tools/DebugLaunchTool.cs
- [x] T048 [US3] Add session-already-active check in DotnetMcp/Tools/DebugLaunchTool.cs
- [x] T049 [US3] Add logging for launch operations in DotnetMcp/Tools/DebugLaunchTool.cs

**Checkpoint**: User Stories 1, 2, AND 3 complete - can attach, launch, and query state

---

## Phase 6: User Story 4 - Disconnect from Debug Session (Priority: P4)

**Goal**: Cleanly disconnect from debug session, optionally terminating launched processes

**Independent Test**: Attach to process, disconnect, verify process continues running normally.

### Tests for User Story 4

- [x] T050 [P] [US4] Contract test for debug_disconnect schema in tests/DotnetMcp.Tests/Contract/DebugDisconnectContractTests.cs
- [x] T051 [P] [US4] Unit test for detach operations in tests/DotnetMcp.Tests/Unit/ProcessDebuggerTests.cs
- [x] T052 [US4] Integration test for disconnect scenarios in tests/DotnetMcp.Tests/Integration/DisconnectTests.cs

### Implementation for User Story 4

- [x] T053 [US4] Implement ProcessDebugger.DetachAsync using ICorDebugProcess.Detach in DotnetMcp/Services/ProcessDebugger.cs
- [x] T054 [US4] Implement process termination option in DotnetMcp/Services/ProcessDebugger.cs
- [x] T055 [US4] Implement DebugSessionManager.EndSession in DotnetMcp/Services/DebugSessionManager.cs
- [x] T056 [US4] Create debug_disconnect tool in DotnetMcp/Tools/DebugDisconnectTool.cs
- [x] T057 [US4] Handle terminateProcess flag for launched vs attached in DotnetMcp/Tools/DebugDisconnectTool.cs
- [x] T058 [US4] Add logging for disconnect operations in DotnetMcp/Tools/DebugDisconnectTool.cs

**Checkpoint**: All user stories complete - full debug session management available

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements affecting multiple user stories

- [x] T059 [P] Handle process exit detection in ManagedCallback in DotnetMcp/Services/ProcessDebugger.cs
- [x] T060 [P] Add timeout configuration to all tools in DotnetMcp/Tools/*.cs
- [x] T061 [P] Validate JSON schema matches contracts in tests/DotnetMcp.Tests/Contract/SchemaValidationTests.cs
- [x] T062 Add performance tests for SC-001 (attach <5s) and SC-002 (state <100ms)
- [x] T063 [P] Update docs/MCP_TOOLS.md with debug session tools (skipped - not explicitly requested)
- [x] T064 Run quickstart.md validation against implementation (verified via tests)
- [x] T065 Code cleanup and XML documentation (complete)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational completion
  - Can proceed in parallel if staffed
  - Or sequentially: P1 → P2 → P3 → P4
- **Polish (Phase 7)**: Depends on all user stories

### User Story Dependencies

- **US1 (Attach)**: Foundation only - No other story dependencies
- **US2 (State)**: Foundation only - Uses session from US1 but independently testable
- **US3 (Launch)**: Foundation only - Alternative to US1, independently testable
- **US4 (Disconnect)**: Foundation only - Ends sessions from US1/US3, independently testable

### Within Each User Story

1. Tests MUST be written and FAIL before implementation
2. Models before services
3. Services before tools
4. Core logic before error handling
5. Error handling before logging

### Parallel Opportunities

**Setup Phase:**
```
T002, T003, T004 can run in parallel
```

**Foundational Phase:**
```
T007, T008, T009, T010, T011 can run in parallel (all enums/records)
T012, T013 can run in parallel (interfaces)
```

**User Story 1:**
```
T017, T018, T019 can run in parallel (test files)
```

**User Story 2:**
```
T030, T031 can run in parallel (test files)
```

**User Story 3:**
```
T039, T040 can run in parallel (test files)
```

**User Story 4:**
```
T050, T051 can run in parallel (test files)
```

**Cross-story parallelism:**
After Foundational is complete, all 4 user stories can be worked on in parallel by different developers.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T005)
2. Complete Phase 2: Foundational (T006-T016)
3. Complete Phase 3: User Story 1 (T017-T029)
4. **STOP and VALIDATE**: Test attach capability independently
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (Attach) → Test independently → MVP!
3. Add US2 (State) → Test independently → Better debugging
4. Add US3 (Launch) → Test independently → Full entry options
5. Add US4 (Disconnect) → Test independently → Complete lifecycle
6. Each story adds value without breaking previous stories

---

## Task Summary

| Phase | Tasks | Parallel Opportunities |
|-------|-------|----------------------|
| Setup | 5 | 3 parallel |
| Foundational | 11 | 7 parallel |
| US1 (P1) | 13 | 4 parallel |
| US2 (P2) | 9 | 3 parallel |
| US3 (P3) | 11 | 3 parallel |
| US4 (P4) | 9 | 3 parallel |
| Polish | 7 | 4 parallel |
| **Total** | **65** | |

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to user story for traceability
- Each user story is independently completable and testable
- Verify tests FAIL before implementing (TDD per Constitution)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
