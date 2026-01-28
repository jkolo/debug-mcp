# Tasks: Reqnroll E2E Tests

**Input**: Design documents from `/specs/008-reqnroll-e2e-tests/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/gherkin-vocabulary.md, quickstart.md

**Tests**: This feature IS about tests. The constitution mandates test-first (III). Feature files and step definitions are written together as the primary deliverable.

**Organization**: Tasks grouped by user story. Each story produces a `.feature` file with step definitions, then migrates corresponding E2E tests from `DotnetMcp.Tests`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create Reqnroll test project with all dependencies and shared infrastructure

- [ ] T001 Create `tests/DotnetMcp.E2E/DotnetMcp.E2E.csproj` with Reqnroll.xUnit, Reqnroll.Tools.MsBuild.Generation, FluentAssertions, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio packages; add project references to DotnetMcp.csproj and DotnetMcp.Tests.csproj; add to solution
- [ ] T002 Create shared context class `tests/DotnetMcp.E2E/Support/DebuggerContext.cs` holding SessionManager, ProcessDebugger, BreakpointManager, TargetProcess, and scenario state (last hit, variables, stack trace, eval result)
- [ ] T003 Create Reqnroll hooks `tests/DotnetMcp.E2E/Hooks/DebuggerHooks.cs` with `[BeforeScenario]` to initialize DebuggerContext and `[AfterScenario]` to disconnect/cleanup; `[BeforeFeature]`/`[AfterFeature]` for shared TestTargetProcess lifecycle
- [ ] T004 Verify project builds and empty test run succeeds: `dotnet build tests/DotnetMcp.E2E && dotnet test tests/DotnetMcp.E2E`

**Checkpoint**: Empty Reqnroll project compiles, hooks fire, context injection works

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Common step definitions reused across all features

**⚠️ CRITICAL**: These shared steps must exist before any feature file can execute

- [ ] T005 Create `tests/DotnetMcp.E2E/StepDefinitions/SessionSteps.cs` with shared Given steps: "a running test target process", "the debugger is attached to the test target", "a launched process paused at entry" — using DebuggerContext injection
- [ ] T006 Create `tests/DotnetMcp.E2E/StepDefinitions/CommonSteps.cs` with shared Then steps: "the session state should be {string}", "the target process should still be running"

**Checkpoint**: Foundation ready — feature files can use shared Given/Then steps

---

## Phase 3: User Story 1 — Session Lifecycle (Priority: P1) 🎯 MVP

**Goal**: Gherkin scenarios covering attach, detach, launch with stop-at-entry, and continue after launch

**Independent Test**: `dotnet test tests/DotnetMcp.E2E --filter "Feature:SessionLifecycle"` — all session scenarios pass

### Implementation for User Story 1

- [ ] T007 [US1] Create `tests/DotnetMcp.E2E/Features/SessionLifecycle.feature` with scenarios: "Attach to a running process", "Detach from a debug session", "Launch a process paused at entry", "Continue execution after launch pause"
- [ ] T008 [US1] Add When/Then session steps to `tests/DotnetMcp.E2E/StepDefinitions/SessionSteps.cs`: "I attach the debugger to the test target", "I detach the debugger", "I launch {string} with stop at entry", "I continue execution"
- [ ] T009 [US1] Run and verify all SessionLifecycle.feature scenarios pass
- [ ] T010 [US1] Remove migrated E2E tests from `tests/DotnetMcp.Tests/Integration/LaunchTests.cs` (3 tests: LaunchAsync_RealProcess_StartsAndAttaches, WithStopAtEntry_IsPaused, ContinueAfterEntry_Runs)

**Checkpoint**: Session lifecycle fully covered in Reqnroll, original E2E tests removed

---

## Phase 4: User Story 2 — Breakpoint Management (Priority: P1)

**Goal**: Gherkin scenarios covering set breakpoint, hit breakpoint, conditional breakpoint (hitCount), remove breakpoint

**Independent Test**: `dotnet test tests/DotnetMcp.E2E --filter "Feature:Breakpoints"` — all breakpoint scenarios pass

### Implementation for User Story 2

- [ ] T011 [US2] Create `tests/DotnetMcp.E2E/Features/Breakpoints.feature` with scenarios: "Set a breakpoint at a source location", "Hit a breakpoint during execution", "Conditional breakpoint with hit count", "Remove a breakpoint"
- [ ] T012 [US2] Create `tests/DotnetMcp.E2E/StepDefinitions/BreakpointSteps.cs` with steps: "a breakpoint on {string} line {int}", "a conditional breakpoint on {string} line {int} with condition {string}", "the test target executes the {string} command", "I wait for a breakpoint hit", "I remove the breakpoint", "the debugger should pause at {string} line {int}", "the breakpoint hit count should be {int}", "the debugger should not pause within {int} seconds"
- [ ] T013 [US2] Run and verify all Breakpoints.feature scenarios pass
- [ ] T014 [US2] Remove migrated E2E tests from `tests/DotnetMcp.Tests/Integration/BreakpointIntegrationTests.cs` (3 tests: SetBreakpoint_OnTestTarget_HitsBreakpoint, WaitForBreakpointAsync_Timeout_ReturnsNull, ConditionalBreakpoint_OnlyHitsWhenConditionMet)

**Checkpoint**: Breakpoint management fully covered in Reqnroll, original E2E tests removed

---

## Phase 5: User Story 3 — Stepping Through Code (Priority: P2)

**Goal**: Gherkin scenarios covering step-over, step-into, step-out

**Independent Test**: `dotnet test tests/DotnetMcp.E2E --filter "Feature:Stepping"` — all stepping scenarios pass

### Implementation for User Story 3

- [ ] T015 [US3] Create `tests/DotnetMcp.E2E/Features/Stepping.feature` with scenarios: "Step over a line of code", "Step into a method call", "Step out of a method"
- [ ] T016 [US3] Create `tests/DotnetMcp.E2E/StepDefinitions/SteppingSteps.cs` with steps: "I step over", "I step into", "I step out", "the debugger should be at {string} line {int}", "the debugger should be in method {string}"
- [ ] T017 [US3] Run and verify all Stepping.feature scenarios pass
- [ ] T018 [US3] Remove migrated E2E tests from `tests/DotnetMcp.Tests/Integration/ExecutionControlTests.cs` (6 tests: StepOver, StepInto, StepOut, Continue, Pause, SteppingAtBreakpoint)

**Checkpoint**: Stepping fully covered in Reqnroll, original E2E tests removed

---

## Phase 6: User Story 4 — Variable and Object Inspection (Priority: P2)

**Goal**: Gherkin scenarios covering local variables, object field inspection, expression evaluation

**Independent Test**: `dotnet test tests/DotnetMcp.E2E --filter "Feature:VariableInspection"` — all inspection scenarios pass

### Implementation for User Story 4

- [ ] T019 [US4] Create `tests/DotnetMcp.E2E/Features/VariableInspection.feature` with scenarios: "Inspect local variables", "Inspect object fields", "Evaluate a C# expression", "Read memory at an address", "Inspect type layout", "Analyze object references"
- [ ] T020 [US4] Create `tests/DotnetMcp.E2E/StepDefinitions/InspectionSteps.cs` with steps: "I inspect local variables", "I inspect the object {string}", "I evaluate the expression {string}", "the variables should contain {string} with value {string}", "the variables should contain {string} of type {string}", "the object should have field {string} with value {string}", "the expression result should be {string}", "the expression result type should be {string}"
- [ ] T021 [US4] Run and verify all VariableInspection.feature scenarios pass
- [ ] T022 [US4] Remove migrated E2E tests from `tests/DotnetMcp.Tests/Integration/ObjectInspectionTests.cs` (3 tests), `MemoryReadTests.cs` (2 tests), `LayoutInspectionTests.cs` (3 tests), `ReferenceAnalysisTests.cs` (2 tests)

**Checkpoint**: Variable/object inspection fully covered in Reqnroll, original E2E tests removed

---

## Phase 7: User Story 5 — Stack Trace Inspection (Priority: P3)

**Goal**: Gherkin scenarios covering stack trace retrieval and frame navigation

**Independent Test**: `dotnet test tests/DotnetMcp.E2E --filter "Feature:StackTrace"` — all stack trace scenarios pass

### Implementation for User Story 5

- [ ] T023 [US5] Create `tests/DotnetMcp.E2E/Features/StackTrace.feature` with scenarios: "View the call stack", "Inspect variables in a different stack frame"
- [ ] T024 [US5] Create `tests/DotnetMcp.E2E/StepDefinitions/StackTraceSteps.cs` with steps: "I request the stack trace", "the stack trace should contain {int} frames", "the stack trace should contain method {string}", "I select stack frame {int}", "the variables should be from the selected frame"
- [ ] T025 [US5] Run and verify all StackTrace.feature scenarios pass

**Checkpoint**: Stack trace inspection fully covered in Reqnroll

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, cleanup, and full regression

- [ ] T026 Run full E2E suite: `dotnet test tests/DotnetMcp.E2E` — all scenarios pass
- [ ] T027 Run remaining DotnetMcp.Tests suite: `dotnet test tests/DotnetMcp.Tests` — no regressions from removed E2E tests
- [ ] T028 Run full solution test suite: `dotnet test` — all tests pass across both projects
- [ ] T029 Review feature files for readability (non-developer should understand the scenarios)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup (T001–T004)
- **User Stories (Phase 3–7)**: All depend on Foundational (T005–T006)
  - US1 and US2 (both P1) can proceed in parallel after Foundational
  - US3 and US4 (both P2) can proceed in parallel after Foundational
  - US5 (P3) can proceed after Foundational
- **Polish (Phase 8)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (Session Lifecycle)**: No dependencies on other stories
- **US2 (Breakpoints)**: No dependencies on other stories (uses shared Given steps from T005)
- **US3 (Stepping)**: No dependencies (uses shared Given steps + breakpoint steps from US2 for setup, but breakpoint steps can be reused even if US2 feature file isn't complete)
- **US4 (Variable Inspection)**: No dependencies (uses shared Given steps + breakpoint steps)
- **US5 (Stack Trace)**: No dependencies (uses shared Given steps + breakpoint steps)

### Within Each User Story

1. Create `.feature` file
2. Create/extend step definitions
3. Verify scenarios pass
4. Remove migrated E2E tests from `DotnetMcp.Tests`

### Parallel Opportunities

- T005 and T006 (foundational steps) can run in parallel [P]
- US1 and US2 can run in parallel (different feature files, different step definition files)
- US3 and US4 can run in parallel
- Within US4: the 4 source files for E2E test removal are independent

---

## Parallel Example: User Story 2 (Breakpoints)

```bash
# All in parallel — different files:
Task: "Create Features/Breakpoints.feature"
Task: "Create StepDefinitions/BreakpointSteps.cs"

# Sequential — verify after implementation:
Task: "Run and verify Breakpoints.feature scenarios"
Task: "Remove migrated E2E tests"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T004)
2. Complete Phase 2: Foundational (T005–T006)
3. Complete Phase 3: User Story 1 — Session Lifecycle (T007–T010)
4. **STOP and VALIDATE**: `dotnet test tests/DotnetMcp.E2E` passes, `dotnet test tests/DotnetMcp.Tests` passes

### Incremental Delivery

1. Setup + Foundational → Reqnroll project ready
2. US1 → Session lifecycle covered → Migrate 3 E2E tests
3. US2 → Breakpoints covered → Migrate 3 E2E tests
4. US3 → Stepping covered → Migrate 6 E2E tests
5. US4 → Inspection covered → Migrate 10 E2E tests
6. US5 → Stack traces covered → New scenarios (no existing E2E to migrate)
7. Polish → Full regression pass

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each phase checkpoint
- 22 existing E2E tests will be migrated; after migration, only unit/contract tests remain in DotnetMcp.Tests
- Feature files must be readable by non-developers
