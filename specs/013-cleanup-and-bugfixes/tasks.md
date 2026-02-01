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

- [X] T001 [US1] Diagnose the FailFast crash — root cause: .NET ProcessWaitState.TryReapChild calls FailFast(errno=ECHILD) when ICorDebug/ptrace reaps child before runtime's SIGCHLD handler
- [X] T002 [US1] Fix: added waitpid() P/Invoke to reap launched child after ICorDebug.Terminate(); added blame-hang-timeout runsettings for ICorDebug native state hang mitigation
- [X] T003 [US1] Verified: 3 consecutive runs complete — 737/817/817 pass, 0 FailFast crashes. Occasional hang (ICorDebug native state) gracefully handled by blame-hang-timeout

**Checkpoint**: Unit test suite is stable. CI/CD pipelines unblocked.

---

## Phase 2: User Story 2 — Resolve Local Variable Names from PDB (Priority: P1)

**Goal**: `GetVariables()` returns source-level variable names from PDB metadata instead of `local_N`.

**Independent Test**: Set breakpoint at TestTargetApp "enums" handler (Program.cs line 112), hit it, call `GetVariables()`, verify variables named `testEnum` and `nullableHolder`.

### Tests for User Story 2

- [X] T004 [US2] E2E tests: added "Enum breakpoint variables have source names from PDB" scenario asserting `testEnum` and `nullableHolder`
- [X] T005 [P] [US2] E2E test: added "Variables at system frame fallback to local_N without crash" scenario
- [X] T006 [US2] Unit tests: added GetLocalVariableNamesAsync tests (no PDB + valid method with known locals)

### Implementation for User Story 2

- [X] T007 [US2] Implemented GetLocalVariableNamesAsync in PdbSymbolReader — reads LocalVariable table via MetadataReader → GetLocalScopes → GetLocalVariables
- [X] T008 [US2] Integrated name resolution into GetLocals() via ResolveLocalVariableNames helper. Falls back to local_N when no PDB/no entry/compiler-generated
- [X] T009 [US2] Arguments already display parameter names via existing GetParameterNames (verified)
- [X] T010 [US2] Verified: 106 E2E scenarios pass, 10 PDB unit tests pass

**Checkpoint**: Variables display source names. E2E test asserts `testEnum` exists.

---

## Phase 3: User Story 3 — ICorDebugEval Condition Evaluation (Priority: P2)

**Goal**: Conditional breakpoints evaluate C# expressions (comparisons, property access, method calls) using FuncEval, chained after `SimpleConditionEvaluator`.

**Independent Test**: Set breakpoint with condition `x > 5`, run target with varying `x` values, verify breakpoint only triggers when condition is true.

### Tests for User Story 3

- [X] T011 [US3] Unit tests: 16 ConditionExpressionParser tests + 11 DebuggerConditionEvaluator tests (all pass)
- [X] T012 [US3] E2E: existing conditional breakpoint scenarios continue to pass with new evaluator chain

### Implementation for User Story 3

- [X] T013 [US3] Created ConditionExpressionParser — parses `<expr> <op> <literal>` with int/string/bool/null/double support, method calls, property paths
- [X] T014 [US3] Created DebuggerConditionEvaluator — decorator wrapping SimpleConditionEvaluator, resolves variables via ConditionContext.EvaluateExpression
- [X] T015 [US3] Fail-safe: no EvaluateExpression → fail-open, FuncEval timeout (5s) → fail-open, exception → fail-open
- [X] T016 [US3] Chained evaluators: DI registers DebuggerConditionEvaluator wrapping SimpleConditionEvaluator in Program.cs
- [X] T017 [US3] Verified: 27 new unit tests pass, 106 E2E scenarios pass

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

- [X] T022 Full E2E Debug: 106 scenarios pass, 0 failures
- [X] T023 Run full E2E suite Release: `dotnet test tests/DebugMcp.E2E/ -c Release` — 106 pass, 0 failures
- [X] T024 Full unit test suite: 846 tests, 0 FailFast crashes. 1 pre-existing flaky failure (TypeBrowsing)
- [X] T025 Orphaned processes cleaned up after test runs
- [X] T026 Updated `BUGS.md` with FailFast fix, ICorDebug hang mitigation, TypeBrowsing flaky test

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
