# Tasks: Debug Launch

**Input**: Design documents from `/specs/007-debug-launch/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/
**Branch**: `007-debug-launch`

**Tests**: Constitution mandates Test-First (TDD) - tests included in each user story phase.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1-US5)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Verify existing infrastructure supports launch functionality

- [ ] T001 Verify ClrDebug DbgShim wrapper supports CreateProcessForLaunch, RegisterForRuntimeStartup, ResumeProcess APIs
- [ ] T002 Review existing ProcessDebugger.cs structure and identify insertion points for launch logic

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T003 Add private fields to ProcessDebugger.cs for launch state: `_resumeHandle`, `_unregisterToken`, `_startupCallbackDelegate`, `_launchCompletionSource`
- [ ] T004 Implement `BuildCommandLine(string program, string[]? args)` helper in DotnetMcp/Services/ProcessDebugger.cs
- [ ] T005 [P] Implement `BuildEnvironmentBlock(Dictionary<string, string>? env)` helper in DotnetMcp/Services/ProcessDebugger.cs
- [ ] T006 Implement startup callback delegate `OnRuntimeStartup` in DotnetMcp/Services/ProcessDebugger.cs (handles ICorDebug initialization)
- [ ] T007 Update `DetachAsync` to call `UnregisterForRuntimeStartup` if launch token exists in DotnetMcp/Services/ProcessDebugger.cs

**Checkpoint**: Foundation ready - user story implementation can begin

---

## Phase 3: User Story 1 - Launch Application for Debugging (Priority: P1) 🎯 MVP

**Goal**: Launch .NET DLL under debugger control, attach before user code executes

**Independent Test**: Launch TestTargetApp.dll, verify process starts and debugger connects, disconnect

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T008 [US1] Create LaunchIntegrationTests.cs test class in tests/DotnetMcp.Tests/Integration/
- [ ] T009 [US1] Write test: LaunchValidDll_ReturnsSessionWithProcessId in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs
- [ ] T010 [P] [US1] Write test: LaunchNonExistentPath_ThrowsFileNotFoundException in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs
- [ ] T011 [P] [US1] Write test: LaunchWhileSessionActive_ThrowsInvalidOperationException in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs

### Implementation for User Story 1

- [ ] T012 [US1] Implement core `LaunchAsync` method in DotnetMcp/Services/ProcessDebugger.cs:
  - Call `CreateProcessForLaunch` with command line
  - Register for runtime startup with callback
  - Call `ResumeProcess`
  - Wait for callback completion via TaskCompletionSource
  - Return ProcessInfo
- [ ] T013 [US1] Handle startup callback thread synchronization using TaskCompletionSource
- [ ] T014 [US1] Add path validation before launch attempt in DotnetMcp/Services/ProcessDebugger.cs
- [ ] T015 [US1] Add error handling for common launch failures (file not found, invalid assembly, permission denied)
- [ ] T016 [US1] Run T009-T011 tests, verify they pass

**Checkpoint**: Basic launch works - can launch DLL and debug it

---

## Phase 4: User Story 2 - Stop at Entry Point (Priority: P2)

**Goal**: Pause execution at entry point when stopAtEntry=true (default)

**Independent Test**: Launch with stopAtEntry=true, verify paused state with reason "entry"

### Tests for User Story 2 ⚠️

- [ ] T017 [US2] Write test: LaunchWithStopAtEntryTrue_StateIsPausedWithEntryReason in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs
- [ ] T018 [P] [US2] Write test: LaunchWithStopAtEntryFalse_StateIsRunning in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs

### Implementation for User Story 2

- [ ] T019 [US2] Implement stopAtEntry logic in startup callback:
  - If stopAtEntry=true: call `_process.Stop(0)` in callback
  - Set `SessionState.Paused` and `PauseReason.Entry`
- [ ] T020 [US2] If stopAtEntry=false: call `_process.Continue(false)` after debugger setup
- [ ] T021 [US2] Run T017-T018 tests, verify they pass

**Checkpoint**: stopAtEntry functionality complete

---

## Phase 5: User Story 3 - Pass Command Line Arguments (Priority: P3)

**Goal**: Pass command line arguments to launched process

**Independent Test**: Launch with args ["--test", "value"], verify app receives them

### Tests for User Story 3 ⚠️

- [ ] T022 [US3] Write test: LaunchWithArgs_ArgsPassedToProcess in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs
- [ ] T023 [P] [US3] Write test: LaunchWithArgsContainingSpaces_ArgsProperlyEscaped in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs

### Implementation for User Story 3

- [ ] T024 [US3] Enhance `BuildCommandLine` to properly quote/escape arguments with spaces and special characters
- [ ] T025 [US3] Add test app command to echo arguments for verification (update TestTargetApp if needed)
- [ ] T026 [US3] Run T022-T023 tests, verify they pass

**Checkpoint**: Command line arguments work correctly

---

## Phase 6: User Story 4 - Set Working Directory (Priority: P4)

**Goal**: Specify working directory for launched process

**Independent Test**: Launch with cwd="/tmp", verify process working directory

### Tests for User Story 4 ⚠️

- [ ] T027 [US4] Write test: LaunchWithCwd_ProcessUsesSpecifiedDirectory in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs
- [ ] T028 [P] [US4] Write test: LaunchWithInvalidCwd_ThrowsDirectoryNotFoundException in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs
- [ ] T029 [P] [US4] Write test: LaunchWithoutCwd_UsesDefaultDirectory in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs

### Implementation for User Story 4

- [ ] T030 [US4] Add working directory validation in `LaunchAsync` (exists, is directory)
- [ ] T031 [US4] Pass working directory to `CreateProcessForLaunch` call
- [ ] T032 [US4] If cwd not specified, default to program's directory
- [ ] T033 [US4] Run T027-T029 tests, verify they pass

**Checkpoint**: Working directory functionality complete

---

## Phase 7: User Story 5 - Set Environment Variables (Priority: P5)

**Goal**: Set custom environment variables for launched process

**Independent Test**: Launch with env={"MY_VAR": "test"}, verify app can read it

### Tests for User Story 5 ⚠️

- [ ] T034 [US5] Write test: LaunchWithEnvVars_ProcessHasAccessToVariables in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs
- [ ] T035 [P] [US5] Write test: LaunchWithEnvOverride_CustomValueTakesPrecedence in tests/DotnetMcp.Tests/Integration/LaunchIntegrationTests.cs

### Implementation for User Story 5

- [ ] T036 [US5] Implement environment block building with proper null-terminated format
- [ ] T037 [US5] Pass environment block to `CreateProcessForLaunch` call
- [ ] T038 [US5] Add test app command to echo environment variables (update TestTargetApp if needed)
- [ ] T039 [US5] Run T034-T035 tests, verify they pass

**Checkpoint**: Environment variables functionality complete

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final improvements and validation

- [ ] T040 [P] Run full test suite: `dotnet test --filter "FullyQualifiedName~Launch"`
- [ ] T041 [P] Manual verification using quickstart.md scenarios
- [ ] T042 Verify timeout handling doesn't leave orphan processes
- [ ] T043 [P] Add/update XML documentation for all public methods in ProcessDebugger.cs
- [ ] T044 Run full project build and test suite: `dotnet build && dotnet test`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational - MVP
- **User Stories 2-5 (Phases 4-7)**: Depend on US1 completion
- **Polish (Phase 8)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: Core launch - MUST complete first (MVP)
- **US2 (P2)**: stopAtEntry - Depends on US1 core launch working
- **US3 (P3)**: Arguments - Depends on US1, can parallel with US2
- **US4 (P4)**: Working directory - Depends on US1, can parallel with US2/US3
- **US5 (P5)**: Environment - Depends on US1, can parallel with US2/US3/US4

### Within Each User Story

1. Tests MUST be written and FAIL before implementation
2. Implement core functionality
3. Run tests and verify they pass
4. Story complete checkpoint

### Parallel Opportunities

- T005 can parallel with T004 (different helper methods)
- T010, T011 can parallel (independent tests)
- T018 can parallel with T017 implementation
- T023 can parallel with T022
- T028, T029 can parallel with T027
- T035 can parallel with T034
- T040, T041, T043 can parallel in Polish phase

---

## Parallel Example: User Story 1 Tests

```bash
# Run test file creation, then write tests in parallel:
Task: T009 - Write LaunchValidDll test
Task: T010 - Write LaunchNonExistentPath test  # [P]
Task: T011 - Write LaunchWhileSessionActive test  # [P]
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify APIs available)
2. Complete Phase 2: Foundational (add fields, helpers, callback)
3. Complete Phase 3: User Story 1 (core launch)
4. **STOP and VALIDATE**: Launch TestTargetApp, verify debugger attaches
5. Can ship/demo basic launch functionality

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → **MVP - Basic launch works!**
3. Add US2 → Test independently → stopAtEntry works
4. Add US3 → Test independently → Arguments work
5. Add US4 → Test independently → Working directory works
6. Add US5 → Test independently → Environment works
7. Polish → Full feature complete

---

## Notes

- Constitution mandates TDD - tests written and failing before implementation
- Existing MCP tool (DebugLaunchTool.cs) is complete - only ProcessDebugger.LaunchAsync needs implementation
- Keep startup callback delegate alive to prevent GC collection
- Use TaskCompletionSource for async callback handling
- Test with TestTargetApp for all verification
