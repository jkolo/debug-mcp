# Implementation Plan: Reqnroll E2E Tests

**Branch**: `008-reqnroll-e2e-tests` | **Date**: 2026-01-28 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-reqnroll-e2e-tests/spec.md`

## Summary

Add BDD-style end-to-end tests using Reqnroll (Gherkin) covering all major debugger scenarios: session lifecycle, breakpoints, stepping, variable inspection, and stack traces. Tests are written in natural English and execute against the existing TestTargetApp via the debugger API.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: Reqnroll 3.3.2, Reqnroll.xUnit, Reqnroll.Tools.MsBuild.Generation, FluentAssertions 8.0.0
**Storage**: N/A (test-only feature)
**Testing**: Reqnroll + xUnit (BDD feature files with step definitions)
**Target Platform**: Linux (same as existing tests)
**Project Type**: Single (new test project added to existing solution)
**Performance Goals**: Each scenario completes within 30 seconds
**Constraints**: ICorDebug requires serial test execution (no parallel scenarios)
**Scale/Scope**: 5 feature files, ~16 scenarios, ~30 step definitions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ PASS | Tests exercise ICorDebug-based debugger — no external debuggers |
| II. MCP Compliance | ✅ N/A | Test-only feature, no new MCP tools |
| III. Test-First | ✅ PASS | This IS the test feature — writing tests for existing functionality |
| IV. Simplicity | ✅ PASS | Separate project, reuses existing infrastructure, no new abstractions |
| V. Observability | ✅ PASS | Reqnroll provides built-in scenario reporting; existing logging untouched |

**Post-Phase 1 Re-check**: No violations. Feature is purely additive (new test project) with no changes to production code.

## Project Structure

### Documentation (this feature)

```text
specs/008-reqnroll-e2e-tests/
├── spec.md
├── plan.md              # This file
├── research.md          # Phase 0: Reqnroll framework research
├── data-model.md        # Phase 1: Shared test context model
├── quickstart.md        # Phase 1: Setup and usage guide
├── contracts/
│   └── gherkin-vocabulary.md  # Reusable Gherkin step definitions
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
tests/DotnetMcp.E2E/
├── DotnetMcp.E2E.csproj
├── Features/
│   ├── SessionLifecycle.feature
│   ├── Breakpoints.feature
│   ├── Stepping.feature
│   ├── VariableInspection.feature
│   └── StackTrace.feature
├── StepDefinitions/
│   ├── SessionSteps.cs
│   ├── BreakpointSteps.cs
│   ├── SteppingSteps.cs
│   ├── InspectionSteps.cs
│   └── StackTraceSteps.cs
├── Hooks/
│   └── DebuggerHooks.cs
└── Support/
    └── DebuggerContext.cs
```

**Structure Decision**: New test project `DotnetMcp.E2E` alongside existing `DotnetMcp.Tests`. Separated because Reqnroll requires MSBuild code generation targets and has distinct BDD concerns. References the main `DotnetMcp` project and reuses `TestTargetProcess` from `DotnetMcp.Tests`.

## Migration Plan: Existing E2E Tests → Reqnroll

There are 22 existing E2E tests (marked `[Trait("Category", "E2E")]`) across 7 files that should be migrated to Reqnroll feature files. After migration, remove the original E2E tests from `DotnetMcp.Tests` to avoid duplication.

### Tests to Migrate

| Source File | E2E Tests | Target Feature File |
|------------|-----------|-------------------|
| ExecutionControlTests.cs | 6 (step-over, step-into, step-out, continue, pause, stepping at breakpoint) | Stepping.feature, SessionLifecycle.feature |
| BreakpointIntegrationTests.cs | 3 (set+hit, wait timeout, conditional) | Breakpoints.feature |
| LaunchTests.cs | 3 (launch+attach, stopAtEntry, continue after entry) | SessionLifecycle.feature |
| ObjectInspectionTests.cs | 3 (inspect fields, nested objects, null fields) | VariableInspection.feature |
| LayoutInspectionTests.cs | 3 (type layout, field offsets, padding) | VariableInspection.feature |
| MemoryReadTests.cs | 2 (read memory, hex format) | VariableInspection.feature |
| ReferenceAnalysisTests.cs | 2 (outbound refs, array refs) | VariableInspection.feature |

### Migration Strategy

1. Write Reqnroll feature files covering the same scenarios
2. Implement step definitions that use the same debugger API calls
3. Verify all Reqnroll scenarios pass
4. Remove `[Trait("Category", "E2E")]` tests from original files (keep unit/contract tests)
5. Verify remaining `DotnetMcp.Tests` still pass

## Complexity Tracking

No violations to justify.
