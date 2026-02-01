# Feature Specification: Cleanup, Bug Fixes, and Remaining Work

**Feature Branch**: `013-cleanup-and-bugfixes`
**Created**: 2026-02-01
**Status**: Draft
**Input**: User description: "Consolidate remaining open tasks from specs 001–012, fix discovered bugs, complete blocked work"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fix test host crash in full unit suite (Priority: P1)

A developer runs the full unit test suite (`dotnet test tests/DebugMcp.Tests/`) as part of CI/CD or local validation. Currently, when `TerminateLaunchedProcessTests` runs alongside other ICorDebug tests, the xUnit test host crashes with `FailFast` in `ProcessWaitState.TryReapChild(errno=10)`, aborting all remaining tests. The developer needs the full suite to complete reliably.

**Why this priority**: Test suite stability is foundational — a crashing test host blocks CI/CD pipelines and makes it impossible to validate any other changes. This must be fixed first.

**Independent Test**: Run `dotnet test tests/DebugMcp.Tests/` and confirm all tests complete (no `FailFast`, no aborted run, exit code 0).

**Acceptance Scenarios**:

1. **Given** the full unit test suite, **When** all tests run together including `TerminateLaunchedProcessTests`, **Then** the test host completes without crash and reports pass/fail for all tests.
2. **Given** `TerminateLaunchedProcessTests` run in isolation, **When** it executes, **Then** it still passes (no regression from fix).
3. **Given** repeated full suite runs (3 consecutive), **When** executed, **Then** all complete without intermittent crashes.

---

### User Story 2 - Resolve local variable names from PDB (Priority: P1)

A debugger user pauses at a breakpoint and inspects local variables. Currently, variables display as `local_0`, `local_1` instead of their source-level names (`testEnum`, `nullableHolder`). The user needs meaningful names to understand program state without cross-referencing source code manually.

**Why this priority**: Variable inspection is a core debugger feature used in every debug session. Unnamed variables severely degrade usability and make the `variables_get` MCP tool output confusing for AI assistants.

**Independent Test**: Set a breakpoint in TestTargetApp at the "enums" command handler (Program.cs line 112), hit it, call `GetVariables()`, and verify variables are named `testEnum` and `nullableHolder` (not `local_N`).

**Acceptance Scenarios**:

1. **Given** a breakpoint at a line with local variables, **When** the user inspects variables, **Then** each variable displays its source-level name from the PDB.
2. **Given** a method with both named locals and compiler-generated temporaries, **When** inspecting, **Then** named locals show source names and compiler-generated ones fall back to `local_N`.
3. **Given** a module without a PDB (e.g., system library), **When** inspecting variables in that frame, **Then** variables display as `local_N` (graceful fallback, no crash).
4. **Given** a method with arguments, **When** inspecting, **Then** arguments also show their parameter names from metadata.

---

### User Story 3 - Evaluate C# expressions as breakpoint conditions (Priority: P2)

A debugger user sets a conditional breakpoint with a C# expression like `x > 5` or `obj.Name == "test"`. Currently, only `hitCount` conditions and boolean literals work. The user needs the debugger to evaluate the condition expression at runtime and only break when it's true.

**Why this priority**: Conditional breakpoints are a frequently used debugging feature, but the current limitation makes them impractical for real-world use. However, the existing `hitCount` + `true`/`false` conditions provide a basic workaround.

**Independent Test**: Set a breakpoint with condition `x > 5` on MethodTarget.cs line 14, run the target, and verify the breakpoint only triggers when the condition is true.

**Acceptance Scenarios**:

1. **Given** a breakpoint with condition `x > 5`, **When** the variable `x` is 3, **Then** execution continues without breaking.
2. **Given** a breakpoint with condition `x > 5`, **When** the variable `x` is 10, **Then** execution breaks.
3. **Given** a breakpoint with condition `obj.Name == "test"`, **When** `obj.Name` is `"test"`, **Then** execution breaks.
4. **Given** a breakpoint with an invalid condition expression, **When** the breakpoint is hit, **Then** the condition evaluates to an error, and the debugger reports the error without crashing the debuggee.
5. **Given** a breakpoint with a condition, **When** the thread is not at a FuncEval-safe point, **Then** the condition evaluation reports a clear error and the breakpoint fires unconditionally (fail-open).

---

### User Story 4 - Record and embed asciinema debug session demos (Priority: P3)

A documentation reader visits the DebugMcp website and wants to see the debugger in action before installing it. Two asciinema recordings need to be created and embedded: one showing variable inspection, another showing a full debug session workflow.

**Why this priority**: Documentation recordings are valuable but non-blocking. The tool works without them, and existing docs already have some recordings. These fill gaps that were previously blocked by bugs (now fixed).

**Independent Test**: Run `cd website && npm run build` and verify the built site includes embedded asciinema players on the inspection tool page.

**Acceptance Scenarios**:

1. **Given** the documentation website, **When** a user visits the variable inspection page, **Then** an asciinema player shows a recorded variable inspection workflow.
2. **Given** the documentation website, **When** a user visits with the full debug session recording, **Then** the asciinema player shows attach → breakpoint → inspect → step → continue workflow.
3. **Given** the website build, **When** `npm run build` runs, **Then** it completes without errors and all asciinema players are functional.

---

### Edge Cases

- Test host crash is ordering-dependent — only manifests when `TerminateLaunchedProcessTests` runs after other ICorDebug-based tests in the same host process.
- PDB variable name resolution must handle: missing PDB files, stripped PDBs, portable vs Windows PDB format, compiler-generated locals (display classes, async state machines).
- FuncEval for condition evaluation requires the thread to be at a GC-safe point. If not at a safe point, the evaluation must fail gracefully (not hang or crash).
- FuncEval timeout: if condition evaluation takes longer than 5 seconds, abort and treat as unconditional break.
- Asciinema recordings are manual — they require an interactive terminal and cannot be automated in CI.

## Clarifications

### Session 2026-02-01

- Q: How should `DebuggerConditionEvaluator` (FuncEval-based) relate to the existing `SimpleConditionEvaluator`? → A: Chain — `SimpleConditionEvaluator` handles trivial cases first (`true`, `false`, hit counts), `DebuggerConditionEvaluator` handles expressions it can't.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The full unit test suite MUST complete without test host crashes when run via `dotnet test tests/DebugMcp.Tests/`.
- **FR-002**: `GetVariables()` MUST return source-level variable names resolved from PDB metadata when available.
- **FR-003**: `GetVariables()` MUST fall back to `local_N` naming when PDB metadata is unavailable or doesn't contain local variable names.
- **FR-004**: Method arguments MUST display their parameter names from method metadata.
- **FR-005**: `DebuggerConditionEvaluator` MUST evaluate breakpoint conditions using ICorDebugEval FuncEval, supporting: comparisons (`>`, `<`, `==`, `!=`, `>=`, `<=`), property/field access (`obj.Property`), and parameterless method calls (`obj.ToString()`). It MUST be chained after `SimpleConditionEvaluator` — trivial conditions (`true`, `false`, hit counts) are handled by `SimpleConditionEvaluator` first; only unrecognized expressions are forwarded to `DebuggerConditionEvaluator`.
- **FR-006**: Condition evaluation MUST fail gracefully when the thread is not at a FuncEval-safe point — report error, break unconditionally (fail-open).
- **FR-007**: Condition evaluation MUST timeout after 5 seconds and treat as unconditional break.
- **FR-008**: An asciinema recording MUST exist at `website/static/casts/variable-inspection.cast` showing variable inspection workflow.
- **FR-009**: An asciinema recording MUST exist at `website/static/casts/full-debug-session.cast` showing a complete debug session.
- **FR-010**: The variable inspection asciinema player MUST be embedded in `website/docs/tools/inspection.md`.

### Assumptions

- The existing `PdbSymbolReader` class can be extended to read local variable names, or `System.Reflection.Metadata` can be used directly to access the `LocalVariable` table in portable PDBs.
- ICorDebugEval is available on the current thread when a breakpoint is hit (thread is typically at a safe point after hitting a breakpoint).
- The expression subset for condition evaluation (comparisons, property access, method calls) covers the vast majority of real-world conditional breakpoint use cases.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Full unit test suite (815+ tests) completes 3 consecutive runs without any test host crash.
- **SC-002**: Variables at breakpoints display source-level names for all named locals in the TestTargetApp test target (verified by E2E test asserting `a variable named "testEnum" should exist`).
- **SC-003**: Conditional breakpoints with expressions like `x > 5` correctly filter breakpoint hits (verified by E2E test with a conditional breakpoint that fires selectively).
- **SC-004**: Full E2E suite (104+ scenarios) passes in both Debug and Release configurations after all changes.
- **SC-005**: Documentation website builds successfully with embedded asciinema players.
- **SC-006**: No orphaned `TestTargetApp` processes remain after test suite completion.
