# Implementation Plan: Cleanup, Bug Fixes, and Remaining Work

**Branch**: `013-cleanup-and-bugfixes` | **Date**: 2026-02-01 | **Spec**: `specs/013-cleanup-and-bugfixes/spec.md`
**Input**: Feature specification from `/specs/013-cleanup-and-bugfixes/spec.md`

## Summary

Consolidate remaining open tasks from specs 001–012: fix test host crash in full unit suite (SIGCHLD race), resolve local variable names from PDB metadata, implement ICorDebugEval-based condition evaluator (chained after SimpleConditionEvaluator), and record/embed remaining asciinema demos.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ClrDebug 0.3.4 (ICorDebug wrappers), System.Reflection.Metadata (PDB reading), ModelContextProtocol SDK 0.1.0-preview.13
**Storage**: N/A (in-memory debug session state)
**Testing**: xUnit (unit), Reqnroll + xUnit (E2E), FluentAssertions
**Target Platform**: Linux (x64)
**Project Type**: Single .NET solution
**Performance Goals**: N/A (bug fixes and extensions)
**Constraints**: ICorDebug requires serial test execution; FuncEval requires thread at GC-safe point; asciinema recordings are manual
**Scale/Scope**: ~815 unit tests, ~104 E2E scenarios

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | All debugging uses ICorDebug directly |
| II. MCP Compliance | PASS | No new MCP tools — extends existing behavior |
| III. Test-First | PASS | Each implementation task has preceding test task in tasks.md |
| IV. Simplicity | PASS | Chaining evaluators (Simple → FuncEval) is minimal indirection |
| V. Observability | PASS | Existing structured logging; FuncEval failures logged |

## Project Structure

### Documentation (this feature)

```text
specs/013-cleanup-and-bugfixes/
├── spec.md
├── plan.md              # This file
├── research.md          # Phase 0: technical research
├── tasks.md             # Phase 2 output (/speckit.tasks)
└── checklists/
    └── requirements.md
```

### Source Code (affected files)

```text
DebugMcp/
├── Services/
│   ├── ProcessDebugger.cs           # BUG-2: Add PDB local variable name resolution
│   └── Breakpoints/
│       ├── DebuggerConditionEvaluator.cs  # NEW: FuncEval condition evaluator
│       ├── SimpleConditionEvaluator.cs    # MODIFY: chain to DebuggerConditionEvaluator
│       └── IConditionEvaluator.cs         # Existing interface
tests/
├── DebugMcp.Tests/
│   └── Integration/
│       └── DisconnectTests.cs       # BUG-1: Fix TerminateLaunchedProcessTests crash
├── DebugMcp.E2E/
│   └── Features/
│       └── ComplexTypeInspection.feature  # BUG-2: Add variable name assertions
website/
├── static/casts/
│   ├── variable-inspection.cast     # MANUAL recording
│   └── full-debug-session.cast      # MANUAL recording
└── docs/tools/
    └── inspection.md                # Embed asciinema player
```

**Structure Decision**: No new projects or directories. All changes within existing solution structure.

## Phases

### Phase 1: BUG-1 — Test Host Crash Fix (Priority: P0)

**Problem**: `TerminateLaunchedProcessTests` crashes xUnit host with `FailFast` in `ProcessWaitState.TryReapChild(errno=10)` when run in full suite.

**Root Cause**: On Linux, .NET's `ProcessWaitState` registers a SIGCHLD handler to reap child processes. When ICorDebug also manages child processes (via ptrace), both .NET runtime and ICorDebug compete to reap the same child PID. When .NET's handler calls `waitpid()` on an already-reaped process, it gets `ECHILD` (errno=10) and calls `FailFast`.

**Approach**:
1. Investigate if adding `Process.WaitForExit()` before ICorDebug `Terminate()` prevents the race
2. If not, isolate `TerminateLaunchedProcessTests` to run in a separate process (xUnit `[Collection]` with custom runner, or move to separate test assembly)
3. Fallback: use `[assembly: CollectionBehavior(DisableTestParallelization = true)]` if ordering alone fixes it

### Phase 2: BUG-2 — PDB Variable Name Resolution (Priority: P0)

**Problem**: `GetVariables()` returns `local_0`, `local_1` instead of source names.

**Approach**:
1. Use `System.Reflection.Metadata` to read `LocalVariable` table from portable PDB
2. In the variable enumeration code path (`ProcessDebugger.GetVariables`), after getting `ICorDebugValue` for each local slot, look up the slot index in the PDB's `LocalVariableTable` to get the source name
3. Fall back to `local_N` when: no PDB loaded, no entry for slot index, or compiler-generated name (starts with `CS$` or `<`)

**Key API**: `MetadataReader.GetLocalVariable(handle)` → `LocalVariable.Name`, mapped via `MethodDebugInformation.GetLocalScopes()` → `LocalScope.GetLocalVariables()`

### Phase 3: ICorDebugEval Condition Evaluator (Priority: P1)

**Problem**: Only `hitCount` and boolean literal conditions work. Need expression evaluation.

**Architecture**: Chain pattern — `SimpleConditionEvaluator` handles trivial cases first; unrecognized expressions forwarded to `DebuggerConditionEvaluator`.

**Approach**:
1. Create `DebuggerConditionEvaluator` implementing `IConditionEvaluator`
2. Parse condition expression into: LHS (variable/property path), operator, RHS (literal/variable/property path)
3. Resolve LHS/RHS values using existing `ProcessDebugger` variable inspection
4. For property access: use `ICorDebugEval.CallFunction()` on the getter method
5. Compare resolved values
6. Handle failures: not at GC-safe point → log + return true (fail-open); timeout (5s) → log + return true

**Expression subset**: `<expr> <op> <literal>` where:
- `<expr>`: local variable name, `obj.Property`, `obj.Method()`
- `<op>`: `>`, `<`, `==`, `!=`, `>=`, `<=`
- `<literal>`: integer, string (`"..."`), `true`, `false`, `null`

### Phase 4: Documentation — Asciinema (Priority: P2)

**Approach**: Manual recording using `asciinema rec`. Embed using existing `AsciinemaPlayer` component.

### Phase 5: Verification (Priority: P0)

Full test suite validation after all code changes.

## Technical Constraints

- ICorDebug requires serial test execution (single debug session per process)
- FuncEval requires thread at GC-safe point; not all breakpoint hits are at safe points
- FuncEval can deadlock if target method blocks — 5s timeout mandatory
- Portable PDB format only (not Windows PDB) — .NET Core/5+ always uses portable
- asciinema recordings require interactive terminal (cannot automate in CI)

## Complexity Tracking

No constitution violations requiring justification.
