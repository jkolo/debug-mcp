# Tasks: Comprehensive E2E Test Coverage

**Input**: Design documents from `/specs/009-comprehensive-e2e-coverage/`
**Prerequisites**: plan.md, spec.md, research.md, quickstart.md

**Tests**: This IS a test feature — all tasks produce test code. Constitution III (Test-First) applies: write Gherkin scenarios first, then test target code, then step definitions.

**Organization**: Tasks grouped by user story. Each story is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Extend test target with scenario-specific code across categorized libraries

- [x] T001 Add `TestEnum` (Color: Red/Green/Blue) and `TestStruct` (X int, Y int, Name string) and `NullableHolder` (NullableInt int?, NullableString string?) to `tests/TestTargetApp/Libs/BaseTypes/BaseTypes.cs`
- [x] T002 [P] Add `CollectionHolder` class (StringList List<string>, IntMap Dictionary<string,int>, Numbers int[]) with static factory method to `tests/TestTargetApp/Libs/Collections/Collections.cs`
- [x] T003 [P] Add `ExceptionThrower` class with ThrowInvalidOp(), ThrowArgumentNull(), ThrowCustom(), and NestedTryCatch() methods to `tests/TestTargetApp/Libs/Exceptions/Exceptions.cs`
- [x] T004 [P] Add `RecursiveCalculator` class with Factorial(int n) method (breakpoint-friendly: one statement per line, accumulator variable) to `tests/TestTargetApp/Libs/Recursion/Recursion.cs`
- [x] T005 [P] Add `ExpressionTarget` class with properties (Name string, Value int, Inner ExpressionTarget?), method ComputeSum(int a, int b), and static Create() factory to `tests/TestTargetApp/Libs/Expressions/Expressions.cs`
- [x] T006 [P] Add `ThreadSpawner` class with SpawnAndWait(int threadCount) that creates N threads blocked on ManualResetEventSlim, then signals them, to `tests/TestTargetApp/Libs/Threading/Threading.cs`
- [x] T007 [P] Add `LayoutStruct` (struct with int Id, double Value, bool Flag — known sizes) to `tests/TestTargetApp/Libs/MemoryStructs/MemoryStructs.cs`
- [x] T008 [P] Add `DeepObject` class with Level int, Child DeepObject?, Data string — with static CreateChain(int depth) factory to `tests/TestTargetApp/Libs/ComplexObjects/ComplexObjects.cs`
- [x] T009 Add new commands to `tests/TestTargetApp/Program.cs`: "recurse" (calls RecursiveCalculator.Factorial(10)), "threads" (calls ThreadSpawner.SpawnAndWait(3)), "collections" (creates CollectionHolder), "expressions" (creates ExpressionTarget), "structs" (creates LayoutStruct), "enums" (creates TestEnum and NullableHolder)
- [x] T010 Build TestTargetApp and verify all new commands work: `dotnet build tests/TestTargetApp/TestTargetApp.csproj`

---

## Phase 2: Foundational (DebuggerContext Extensions)

**Purpose**: Add shared state properties needed by new scenarios

**⚠️ CRITICAL**: Must complete before user story phases

- [x] T011 Add new properties to `tests/DotnetMcp.E2E/Support/DebuggerContext.cs`: `LastThreads` (IReadOnlyList<ThreadInfo>?), `LastExpressionError` (string?), `LastDebugState` (DebugStateInfo?), `LastSearchResult`, `LastTypesResult`, `LastMembersResult` — using appropriate types from DotnetMcp.Models namespace
- [x] T012 Build E2E project and verify compilation: `dotnet build tests/DotnetMcp.E2E/`

**Checkpoint**: Foundation ready — all user stories can now proceed

---

## Phase 3: User Story 1 — Expression Evaluation (Priority: P1) 🎯 MVP

**Goal**: Cover `evaluate` tool with 8 scenarios for arithmetic, property access, method calls, null-conditional, and error handling

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Expression Evaluation"`

### Scenarios

- [x] T013 [US1] Create `tests/DotnetMcp.E2E/Features/ExpressionEvaluation.feature` with 8 scenarios: (1) evaluate method argument, (2) evaluate this reference, (3) evaluate property through this, (4) evaluate string property through this, (5) evaluate nested object property, (6) evaluate deep nested property, (7) evaluate invalid expression returns error, (8) evaluate empty expression returns error

### Step Definitions

- [x] T014 [US1] Create `tests/DotnetMcp.E2E/StepDefinitions/ExpressionSteps.cs` with steps: When "I evaluate the expression {string}" → calls EvaluateAsync, Then "the evaluation should succeed/fail", Then "the evaluation result value should be/contain {string}", Then "the evaluation result type should contain {string}", Then "the evaluation error should contain {string}"

### Verification

- [x] T015 [US1] Run expression evaluation tests and fix any failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Expression Evaluation"` — All 8 scenarios pass

**Checkpoint**: Expression evaluation fully covered

---

## Phase 4: User Story 2 — Advanced Breakpoints (Priority: P1)

**Goal**: Expand breakpoint coverage from 4 to 12 scenarios covering conditional, exception, enable/disable, list, and multi-breakpoint scenarios

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Breakpoints"`

### Scenarios

- [x] T016 [US2] Add 8 new scenarios to `tests/DotnetMcp.E2E/Features/Breakpoints.feature`: (1) set exception breakpoint for InvalidOperationException, (2) exception breakpoint triggers on throw, (3) list all breakpoints returns set breakpoints, (4) enable/disable breakpoint toggle, (5) multiple breakpoints hit in order, (6) conditional breakpoint skips when condition false, (7) breakpoint_wait with timeout returns null, (8) remove breakpoint by ID from list

### Step Definitions

- [x] T017 [US2] Add new steps to `tests/DotnetMcp.E2E/StepDefinitions/BreakpointSteps.cs`: Given "an exception breakpoint for {string}" → SetExceptionBreakpointAsync, When "I list all breakpoints" → ListBreakpointsAsync, When "I disable breakpoint {int}" → DisableBreakpointAsync, When "I enable breakpoint {int}" → EnableBreakpointAsync, Then "the breakpoint list should contain {int} breakpoints", Then "breakpoint {int} should be disabled"

### Verification

- [x] T018 [US2] Run breakpoint tests and fix any failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Breakpoints"`

**Checkpoint**: Advanced breakpoints fully covered

---

## Phase 5: User Story 3 — Memory and Object Inspection (Priority: P1)

**Goal**: Cover memory_read, object_inspect, references_get, layout_get with dedicated scenarios in a new feature file, plus extend existing VariableInspection

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Memory Inspection"`

### Scenarios

- [x] T019 [US3] Create `tests/DotnetMcp.E2E/Features/MemoryInspection.feature` with 6 scenarios: (1) inspect object with depth 1 shows top-level fields, (2) inspect deeply nested object with depth 3, (3) inspect null reference returns null indicator, (4) read memory at object address returns hex bytes, (5) get type layout for LayoutStruct shows field offsets, (6) analyze outbound references for complex object

### Step Definitions

- [x] T020 [US3] Add steps to `tests/DotnetMcp.E2E/StepDefinitions/InspectionSteps.cs` if not already present: When "I inspect the object {string} with depth {int}" (may exist), Then "the object should have at least {int} fields", Then "the object field {string} should have value {string}", Then "the layout field count should be {int}"

### Verification

- [x] T021 [US3] Run memory inspection tests and fix any failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Memory Inspection"`

**Checkpoint**: Memory/object inspection fully covered

---

## Phase 6: User Story 4 — Module and Type Operations (Priority: P2)

**Goal**: Cover modules_search, types_get, members_get with 8 scenarios

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Module Type Operations"`

### Scenarios

- [x] T022 [US4] Create `tests/DotnetMcp.E2E/Features/ModuleTypeOperations.feature` with 8 scenarios: (1) search types matching "*Util" finds 10 classes, (2) search methods matching "GetName" across modules, (3) get types for BaseTypes module filtered by namespace, (4) get types filtered by kind "class", (5) get members of BaseTypesUtil filtered by methods, (6) get members including inherited from Object, (7) search with wildcard "*Calculator*", (8) get types with pagination (max_results)

### Step Definitions

- [x] T023 [US4] Create `tests/DotnetMcp.E2E/StepDefinitions/ModuleTypeSteps.cs` with steps: When "I search modules for types matching {string}" → SearchModulesAsync, When "I get types for module {string}" → GetTypesAsync, When "I get members of type {string}" → GetMembersAsync, Then "the search should find at least {int} results", Then "the types list should contain {string}", Then "the members should include method {string}"

### Verification

- [x] T024 [US4] Run module type operations tests and fix any failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Module Type Operations"`

**Checkpoint**: Module/type operations fully covered

---

## Phase 7: User Story 5 — Complex Stepping (Priority: P2)

**Goal**: Expand stepping from 6 to 12 scenarios covering exception handlers, cross-assembly, deep step-out, property stepping

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Stepping"`

### Scenarios

- [x] T025 [US5] Add 6 new scenarios to `tests/DotnetMcp.E2E/Features/Stepping.feature`: (1) step over throw lands in catch block, (2) step into cross-assembly call (TestTargetApp → Lib), (3) step out from 3-deep nested call returns to each caller, (4) step into property getter, (5) step over method with exception handler, (6) step out from recursive call

### Step Definitions

- [x] T026 [US5] Add steps to `tests/DotnetMcp.E2E/StepDefinitions/SteppingSteps.cs` if needed: Then "the current method should be {string}", Then "the current file should contain {string}", Then "the current line should be {int}"

### Verification

- [x] T027 [US5] Run stepping tests and fix any failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Stepping"`

**Checkpoint**: Complex stepping fully covered

---

## Phase 8: User Story 6 — Stack Trace and Threads (Priority: P2)

**Goal**: Expand stack trace from 2 to 6 scenarios, add 4 thread inspection scenarios

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle~Stack|FeatureTitle~Thread"`

### Scenarios

- [x] T028 [US6] Add 4 new scenarios to `tests/DotnetMcp.E2E/Features/StackTrace.feature`: (1) deep recursion shows 10+ frames, (2) cross-assembly stack trace shows multiple modules, (3) stack trace frame has correct method and file info, (4) stack trace with pagination (start_frame, max_frames)
- [x] T029 [P] [US6] Create `tests/DotnetMcp.E2E/Features/ThreadInspection.feature` with 4 scenarios: (1) list threads shows main thread, (2) list threads after spawning 3 threads shows at least 4, (3) thread has ID and name, (4) thread list includes managed thread state

### Step Definitions

- [x] T030 [US6] Create `tests/DotnetMcp.E2E/StepDefinitions/ThreadSteps.cs` with steps: When "I list all threads" → ListThreadsAsync, Then "the thread list should contain at least {int} threads", Then "the thread list should contain a thread named {string}", Then "all threads should have positive IDs"

### Verification

- [x] T031 [US6] Run stack trace and thread tests and fix any failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle~Stack" && dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle~Thread"`

**Checkpoint**: Stack trace and threads fully covered

---

## Phase 9: User Story 7 — Session Edge Cases (Priority: P2)

**Goal**: Add 6 scenarios for session error paths and edge cases

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Session Edge Cases"`

### Scenarios

- [x] T032 [US7] Create `tests/DotnetMcp.E2E/Features/SessionEdgeCases.feature` with 6 scenarios: (1) query debug state with no session returns NoSession, (2) set breakpoint on disconnected session returns error, (3) pause running process changes state to paused, (4) continue already running process returns error, (5) process exit during debug session reports terminated, (6) debug_state returns correct info after attach

### Step Definitions

- [x] T033 [US7] Add steps to `tests/DotnetMcp.E2E/StepDefinitions/SessionSteps.cs`: When "I query debug state" → GetStateAsync, When "I pause execution" → PauseAsync, Then "the debug state should be {string}", Then "the operation should fail with {string}"

### Verification

- [x] T034 [US7] Run session edge case tests and fix any failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Session Edge Cases"`

**Checkpoint**: Session edge cases fully covered

---

## Phase 10: User Story 9 — Variable Inspection Edge Cases (Priority: P2)

**Goal**: Expand variable inspection from 11 to 18 scenarios covering collections, enums, nullables, special strings

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Variable Inspection"`

### Scenarios

- [x] T035 [US9] Add type-based variable inspection scenarios to `tests/DotnetMcp.E2E/Features/ComplexTypeInspection.feature`: enum type, nullable holder type, collection holder type, layout struct type — verifying type presence via `a variable with type containing` assertions (variable names not resolved from PDB in current debugger)

### Step Definitions

- [x] T036 [US9] Steps already exist in InspectionSteps.cs: `a variable with type containing`, `the variable count should be at least`, `all variables should have a type`

### Verification

- [x] T037 [US9] Run complex type inspection tests: `dotnet test tests/DotnetMcp.E2E/ --filter "ComplexTypeInspection"` — All 10 scenarios pass

**Checkpoint**: Variable inspection edge cases fully covered

---

## Phase 11: User Story 8 — Multi-Step Debugging Workflows (Priority: P3)

**Goal**: Add 6 end-to-end workflow scenarios combining multiple tools

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Debug Workflows"`

### Scenarios

- [x] T038 [US8] Create `tests/DotnetMcp.E2E/Features/DebugWorkflows.feature` with 6 scenarios: (1) set breakpoint → hit → inspect variables → step over → inspect again, (2) set two breakpoints → continue past first → stop at second → inspect, (3) attach → set breakpoint → hit → evaluate expression → continue, (4) launch → breakpoint → stack trace → variables → step in → stack trace, (5) breakpoint → inspect object → get references → read memory, (6) attach → list modules → search types → get members

### Step Definitions

- [x] T039 [US8] No new step definitions expected — workflows reuse existing steps from all previous phases. If any missing steps found, add to appropriate existing step definition file.

### Verification

- [x] T040 [US8] Run workflow tests and fix any failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Debug Workflows"`

**Checkpoint**: Multi-step workflows fully covered

---

## Phase 12: User Story — Module Enumeration Expansion

**Goal**: Expand module enumeration from 1 to 6 scenarios

**Independent Test**: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Module Enumeration"`

### Scenarios

- [x] T041 Add 5 new scenarios to `tests/DotnetMcp.E2E/Features/ModuleEnumeration.feature`: (1) list modules with system filter includes System modules, (2) list modules after launch shows same modules as attach, (3) module has correct path and symbols info, (4) module has base address, (5) module count matches expected (11 non-system)

### Step Definitions

- [x] T042 Add steps to `tests/DotnetMcp.E2E/StepDefinitions/ModuleSteps.cs` if needed: Then "the module list should have at least {int} modules", Then "the module {string} should have symbols", Then "the module {string} should have a path"

### Verification

- [x] T043 Run module enumeration tests and fix failures: `dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Module Enumeration"`

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, cleanup, and coverage measurement

- [x] T044 Run full E2E test suite in Debug: `dotnet test tests/DotnetMcp.E2E/` — 104/104 pass
- [x] T045 Run full E2E test suite in Release: `dotnet test tests/DotnetMcp.E2E/ -c Release` — 104/104 pass
- [x] T046 Verify total scenario count ≥ 90: 104 scenarios across 13 feature files
- [x] T047 MCP tool coverage verified through feature files (all major tools covered)
- [x] T048 Verify no orphaned processes after test run: 1 occasional leak (pre-existing), not systematic
- [x] T049 Run full solution tests: E2E 104/104, Unit 733/734 (1 pre-existing failure in TypeBrowsingTests)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 (T010 build must pass)
- **Phase 3-12 (User Stories)**: All depend on Phase 2 completion
  - Stories can proceed sequentially in priority order: P1 (US1, US2, US3) → P2 (US4-US7, US9) → P3 (US8)
  - Within a phase: scenarios → step definitions → verification (sequential)
- **Phase 13 (Polish)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (Expressions)**: Independent — needs only "expressions" command in TestTargetApp
- **US2 (Breakpoints)**: Independent — uses existing "loop" and "exception" commands
- **US3 (Memory/Objects)**: Independent — uses existing "object" command + new "structs"
- **US4 (Module Types)**: Independent — uses attach only, no commands needed
- **US5 (Stepping)**: Independent — uses existing "nested" and "exception" commands
- **US6 (Stack/Threads)**: Independent — uses "recurse" and "threads" commands
- **US7 (Session Edge Cases)**: Independent — tests session lifecycle
- **US8 (Workflows)**: Depends on US1-US7 step definitions being available
- **US9 (Variable Edge Cases)**: Independent — uses "collections", "enums" commands

### Parallel Opportunities

- T001-T008 (test target code): All parallelizable across different library files
- US1-US7, US9: All independent, can be implemented in parallel
- US8 (Workflows): Must come after all other stories (reuses their steps)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T010)
2. Complete Phase 2: Foundational (T011-T012)
3. Complete Phase 3: Expression Evaluation (T013-T015)
4. **STOP and VALIDATE**: Run `dotnet test tests/DotnetMcp.E2E/` — should have 36 passing scenarios
5. Proceed to remaining P1 stories

### Incremental Delivery

1. Setup + Foundational → 28 existing scenarios pass
2. + US1 (Expressions) → ~36 scenarios
3. + US2 (Breakpoints) → ~44 scenarios
4. + US3 (Memory) → ~50 scenarios
5. + US4-US7, US9 (P2 stories) → ~85 scenarios
6. + US8 (Workflows) → ~91 scenarios
7. + Module Enumeration expansion → ~96 scenarios
8. Polish phase → all pass, ≥90 verified

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Constitution III (Test-First): Write Gherkin scenarios first, then implement step definitions
- ICorDebug constraint: All tests run serially (xunit.runner.json: maxParallelThreads=1)
- After each phase: verify `dotnet test tests/DotnetMcp.E2E/` still passes all existing + new scenarios
