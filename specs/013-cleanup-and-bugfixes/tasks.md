# Tasks: Cleanup, Bug Fixes, and Remaining Work

**Input**: Design documents from `/specs/013-cleanup-and-bugfixes/`
**Prerequisites**: plan.md, spec.md, research.md

**Tests**: Test tasks included — Constitution III (Test-First) mandates tests before implementation.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1, US2, US3, US4)
- Exact file paths included

---

## Phase 1: User Story 1 — Fix Test Host Crash (Priority: P1) 🎯 MVP

**Goal**: Full unit test suite (`dotnet test tests/DebugMcp.Tests/`) completes without test host crash, including `TerminateLaunchedProcessTests`.

**Independent Test**: Run `dotnet test tests/DebugMcp.Tests/` three consecutive times — all must complete with exit code 0.

- [ ] T001 [US1] Diagnose the FailFast crash — reproduce with `dotnet test tests/DebugMcp.Tests/`, capture exact failure sequence. Determine if the issue is test Dispose ordering or SIGCHLD race in `tests/DebugMcp.Tests/Integration/DisconnectTests.cs`
- [ ] T002 [US1] Fix or mitigate the crash — ensure proper process lifecycle ordering in test cleanup. Target file: `tests/DebugMcp.Tests/Integration/DisconnectTests.cs` (and possibly `DebugMcp/Services/ProcessDebugger.cs` if runtime fix needed)
- [ ] T003 [US1] Verify fix: run `dotnet test tests/DebugMcp.Tests/` three consecutive times — all 817+ tests complete, no FailFast, exit code 0

**Checkpoint**: Unit test suite is stable. CI/CD pipelines unblocked.

---

## Phase 2: User Story 2 — Resolve Local Variable Names from PDB (Priority: P1)

**Goal**: `GetVariables()` returns source-level variable names from PDB metadata instead of `local_N`.

**Independent Test**: Set breakpoint at TestTargetApp "enums" handler (Program.cs line 112), hit it, call `GetVariables()`, verify variables named `testEnum` and `nullableHolder`.

### Tests for User Story 2

- [ ] T004 [US2] Write E2E tests: add scenarios to `tests/DebugMcp.E2E/Features/ComplexTypeInspection.feature` — (1) `a variable named "testEnum" should exist` at the enums breakpoint, (2) a method argument should display its parameter name (not `arg_0`). Verify tests fail (currently shows `local_0`)
- [ ] T005 [P] [US2] Write E2E test: add scenario to `tests/DebugMcp.E2E/Features/ComplexTypeInspection.feature` — inspect variables in a frame from a system library (no PDB), verify variables display as `local_N` without crash (FR-003 fallback)
- [ ] T006 [US2] Write unit test: add test in `tests/DebugMcp.Tests/` for PDB local variable name lookup — given a method token and IL offset, resolve local slot index to source name. Verify test fails

### Implementation for User Story 2

- [ ] T007 [US2] Implement PDB local variable name resolution — extend `DebugMcp/Services/Breakpoints/PdbSymbolReader.cs` (or add new method) to read `LocalVariable` table via `System.Reflection.Metadata`: `MetadataReader` → `MethodDebugInformation` → `GetLocalScopes()` → `LocalScope.GetLocalVariables()` → `LocalVariable.Name`
- [ ] T008 [US2] Integrate name resolution into variable enumeration — update `DebugMcp/Services/ProcessDebugger.cs` `GetVariables()` to look up slot index in PDB. Fall back to `local_N` when: no PDB, no entry for slot, or compiler-generated name (starts with `CS$` or `<`)
- [ ] T009 [US2] Ensure method arguments display parameter names from metadata in `DebugMcp/Services/ProcessDebugger.cs`
- [ ] T010 [US2] Verify all tests pass: `dotnet test tests/DebugMcp.Tests/` and `dotnet test tests/DebugMcp.E2E/`

**Checkpoint**: Variables display source names. E2E test asserts `testEnum` exists.

---

## Phase 3: User Story 3 — ICorDebugEval Condition Evaluation (Priority: P2)

**Goal**: Conditional breakpoints evaluate C# expressions (comparisons, property access, method calls) using FuncEval, chained after `SimpleConditionEvaluator`.

**Independent Test**: Set breakpoint with condition `x > 5`, run target with varying `x` values, verify breakpoint only triggers when condition is true.

### Tests for User Story 3

- [ ] T011 [US3] Write unit tests for expression parsing in `tests/DebugMcp.Tests/` — test parsing `x > 5`, `obj.Name == "test"`, `obj.ToString()`, invalid expressions. Verify tests fail
- [ ] T012 [US3] Write E2E test: add scenario to `tests/DebugMcp.E2E/Features/` for conditional breakpoint with expression — set breakpoint with condition, verify it fires selectively. Verify test fails

### Implementation for User Story 3

- [ ] T013 [US3] Implement expression parser — create `DebugMcp/Services/Breakpoints/ConditionExpressionParser.cs` that parses `<expr> <op> <literal>` where expr is variable/property path, op is comparison, literal is int/string/bool/null
- [ ] T014 [US3] Implement `DebuggerConditionEvaluator` in `DebugMcp/Services/Breakpoints/DebuggerConditionEvaluator.cs` — resolve LHS/RHS values using existing variable inspection, use `ICorDebugEval.CallFunction()` for property getters, compare resolved values
- [ ] T015 [US3] Add fail-safe handling in `DebugMcp/Services/Breakpoints/DebuggerConditionEvaluator.cs` — not at GC-safe point → log + return true (fail-open); FuncEval timeout (5s) → log + return true
- [ ] T016 [US3] Chain evaluators — modify `DebugMcp/Services/Breakpoints/SimpleConditionEvaluator.cs` to forward unrecognized expressions to `DebuggerConditionEvaluator`
- [ ] T017 [US3] Verify all tests pass: `dotnet test tests/DebugMcp.Tests/` and `dotnet test tests/DebugMcp.E2E/`

**Checkpoint**: Conditional breakpoints with expressions work. Evaluator chain: Simple → FuncEval.

---

## Phase 4: User Story 4 — Asciinema Recordings (Priority: P3)

**Goal**: Two asciinema recordings created and embedded in documentation website.

**Independent Test**: Run `cd website && npm run build` — site builds with embedded asciinema players.

- [ ] T018 ⚠️ MANUAL [US4] Record `website/static/casts/variable-inspection.cast` — show: attach → set breakpoint → hit → inspect variables (with source names from US2 fix) → disconnect. Use `asciinema rec --idle-time-limit 2`
- [ ] T019 ⚠️ MANUAL [US4] Record `website/static/casts/full-debug-session.cast` — show: launch → set breakpoints → step through code → inspect → continue → disconnect. Use `asciinema rec --idle-time-limit 2`
- [ ] T020 [US4] Embed asciinema player in `website/docs/tools/inspection.md` — add `<AsciinemaPlayer src="/casts/variable-inspection.cast" />` with appropriate props
- [ ] T021 [US4] Verify build: `cd website && npm run build` — all players embedded, no errors

**Checkpoint**: Documentation site builds with all recordings embedded.

---

## Phase 5: Verification & Closure

**Purpose**: Full validation after all code changes

- [ ] T022 Run full E2E suite Debug: `dotnet test tests/DebugMcp.E2E/` — all 104+ scenarios pass
- [ ] T023 Run full E2E suite Release: `dotnet test tests/DebugMcp.E2E/ -c Release` — all pass
- [ ] T024 Run full unit test suite: `dotnet test tests/DebugMcp.Tests/` — no crashes, no failures
- [ ] T025 Verify no orphaned TestTargetApp processes after test runs: `pgrep -f TestTargetApp` returns empty
- [ ] T026 Update `BUGS.md` with any new findings

---

## Dependencies & Execution Order

### Phase Dependencies

- **US1 (Phase 1)**: No dependencies — start immediately
- **US2 (Phase 2)**: No dependencies on US1 — can start in parallel
- **US3 (Phase 3)**: No dependencies on US1/US2 — can start in parallel
- **US4 (Phase 4)**: Depends on US2 (variable names must work for recording to show them)
- **Verification (Phase 5)**: Depends on US1, US2, US3 complete

### User Story Dependencies

- **US1**: Independent — test infrastructure fix
- **US2**: Independent — PDB reading in ProcessDebugger
- **US3**: Independent — new evaluator in Breakpoints/
- **US4**: Depends on US2 (recordings show named variables)

### Parallel Opportunities

- T004, T005, T006: US2 test tasks can run in parallel (different test projects/files)
- T011, T012: Both US3 test tasks can run in parallel
- T018, T019: Both recordings can be done in parallel (MANUAL)
- US1, US2, US3: All three code phases can proceed in parallel (different files)

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: US1 — Fix test host crash
2. Complete Phase 2: US2 — PDB variable names
3. **STOP and VALIDATE**: Full test suite stable, variables show source names
4. This alone delivers major quality improvement

### Incremental Delivery

1. US1 (test stability) → Deploy — CI/CD unblocked
2. US2 (variable names) → Deploy — core usability improvement
3. US3 (FuncEval conditions) → Deploy — advanced feature
4. US4 (recordings) → Deploy — documentation complete
5. Verification → Final validation

---

## Task Origin Tracking

| Task | Origin | Original Spec |
|------|--------|---------------|
| T001-T003 | New bug | TerminateLaunchedProcessTests crash |
| T004-T010 | New bug | Variable names not resolved from PDB |
| T011-T017 | 002/T063 | ICorDebugEval condition evaluation (DEFERRED) |
| T018-T019 | 012/T027,T028 | Asciinema recordings (was BLOCKED) |
| T020 | 012/T031 | Embed asciinema in inspection docs (was BLOCKED) |
| T021-T026 | New | Verification & closure |

## Notes

- US1/US2/US3 touch different files — safe to parallelize
- MANUAL tasks (T017, T018) require interactive terminal — cannot be automated
- Constitution III satisfied: test tasks (T004-T005, T010-T011) precede implementation
- Portable PDB format only — .NET Core/5+ always uses portable PDBs
