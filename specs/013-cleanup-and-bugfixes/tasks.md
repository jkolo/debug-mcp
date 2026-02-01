# Tasks: Cleanup, Bug Fixes, and Remaining Work

**Purpose**: Consolidate all remaining open tasks from previous specs, fix known bugs, and complete blocked work.

---

## Phase 1: Bug Fixes

### BUG-1: TerminateLaunchedProcessTests crashes test host when run in full suite

**Symptom**: `TerminateLaunchedProcessTests` passes when run alone but crashes the xUnit test host with `FailFast` in `ProcessWaitState.TryReapChild` when run as part of the full `DebugMcp.Tests` suite. Error: `Error while reaping child. errno = 10`. This kills the test runner and aborts remaining tests.

**Location**: `tests/DebugMcp.Tests/Integration/DisconnectTests.cs`

**Root cause hypothesis**: The test launches a child process under debugger control and terminates it. When combined with other tests that also spawn/kill processes, .NET's `ProcessWaitState` encounters a race condition on Linux (SIGCHLD handler conflicts between ICorDebug-managed and .NET-managed child processes).

- [ ] T001 Diagnose the FailFast crash — reproduce with `dotnet test tests/DebugMcp.Tests/` and capture the exact failure sequence. Determine if the issue is test isolation (Dispose ordering) or a runtime bug in child process reaping.
- [ ] T002 Fix or mitigate the crash — either fix process cleanup in `TerminateLaunchedProcessTests.Dispose()`, add proper process wait before dispose, or isolate the test to prevent host crash.
- [ ] T003 Verify full unit test suite passes: `dotnet test tests/DebugMcp.Tests/` — all 817 tests should complete without test host crash.

### BUG-2: Variable names not resolved from PDB (local_0, local_1 instead of source names)

**Symptom**: `GetVariables()` returns variables named `local_0`, `local_1`, etc. instead of source names like `testEnum`, `nullableHolder`. This affects variable inspection usability.

**Location**: `DebugMcp/Services/ProcessDebugger.cs` (GetVariables / variable enumeration logic)

**Root cause hypothesis**: The PDB reader doesn't resolve local variable names from the method's debug info sequence points. ICorDebug returns IL slot indices but the code doesn't look up names from PDB metadata.

- [ ] T004 Diagnose variable name resolution — check if `PdbSymbolReader` has local variable name lookup capability, or if `System.Reflection.Metadata` needs to be used to read `LocalVariable` table from PDB.
- [ ] T005 Implement PDB-based local variable name resolution in the variable enumeration code path.
- [ ] T006 Add E2E test: verify `a variable named "testEnum" should exist` at the enums breakpoint (line 112 of Program.cs). Update ComplexTypeInspection.feature scenarios.
- [ ] T007 Verify all E2E tests still pass after the fix: `dotnet test tests/DebugMcp.E2E/`

---

## Phase 2: Deferred Tasks (from previous specs)

### From 002-breakpoint-ops: ICorDebugEval condition evaluation

- [ ] T008 [from 002/T063] Implement ICorDebugEval-based condition evaluation in `DebugMcp/Services/Breakpoints/DebuggerConditionEvaluator.cs` — evaluate C# expressions as breakpoint conditions using the debugger's FuncEval capability. Currently only `hitCount` conditions and boolean literals work via `SimpleConditionEvaluator`.

### From 012-docs-improvement: Asciinema recordings (previously blocked by bugs, now unblocked)

- [ ] T009 [from 012/T027] Record `website/static/casts/variable-inspection.cast` asciinema recording showing variable inspection workflow.
- [ ] T010 [from 012/T028] Record `website/static/casts/full-debug-session.cast` asciinema recording showing a complete debug session.
- [ ] T011 [from 012/T031] Embed asciinema player in `website/docs/tools/inspection.md` — reference variable-inspection.cast.

---

## Phase 3: Verification & Closure

- [ ] T012 Run full E2E suite Debug: `dotnet test tests/DebugMcp.E2E/` — all 104+ scenarios pass
- [ ] T013 Run full E2E suite Release: `dotnet test tests/DebugMcp.E2E/ -c Release`
- [ ] T014 Run full unit test suite: `dotnet test tests/DebugMcp.Tests/` — no crashes, no failures
- [ ] T015 Verify no orphaned TestTargetApp processes after test runs
- [ ] T016 Update BUGS.md with any new findings

---

## Task Origin Tracking

| New Task | Origin | Original Spec |
|----------|--------|---------------|
| T001-T003 | New bug | TerminateLaunchedProcessTests crash |
| T004-T007 | New bug | Variable names not resolved from PDB |
| T008 | 002/T063 | ICorDebugEval condition evaluation (DEFERRED) |
| T009 | 012/T027 | Asciinema variable-inspection recording (was BLOCKED) |
| T010 | 012/T028 | Asciinema full-debug-session recording (was BLOCKED) |
| T011 | 012/T031 | Embed asciinema in inspection docs (was BLOCKED) |
| T012-T016 | New | Final verification |
