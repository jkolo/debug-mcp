# Implementation Plan: Comprehensive E2E Test Coverage

**Branch**: `009-comprehensive-e2e-coverage` | **Date**: 2026-01-28 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-comprehensive-e2e-coverage/spec.md`

## Summary

Expand the Reqnroll E2E test suite from 28 to ~100 scenarios covering all 25 MCP tools with >80% code coverage. Test scenario code is distributed across 10 categorized library projects (BaseTypes, Collections, Exceptions, Recursion, Expressions, Threading, AsyncOps, MemoryStructs, ComplexObjects, Scenarios). New feature files and step definitions are added incrementally, reusing existing infrastructure.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: Reqnroll 3.3.3, Reqnroll.xUnit, FluentAssertions 8.0.0, xUnit
**Storage**: N/A (test-only feature)
**Testing**: Reqnroll/Gherkin BDD with xUnit runner, serial execution
**Target Platform**: Linux x64
**Project Type**: Single project (tests/DotnetMcp.E2E)
**Performance Goals**: Full E2E suite completes within 5 minutes
**Constraints**: ICorDebug requires serial test execution; process must be stopped for inspection operations
**Scale/Scope**: ~100 scenarios across ~12 feature files, ~10 step definition files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ Pass | Tests exercise ICorDebug APIs directly via ProcessDebugger |
| II. MCP Compliance | ✅ Pass | Tests validate all 25 MCP tools |
| III. Test-First | ✅ Pass | This IS the test feature — tests written first, then test target code as needed |
| IV. Simplicity | ✅ Pass | Reuses existing infrastructure; no new abstractions |
| V. Observability | ✅ Pass | Tests verify error messages and structured responses |

All gates pass. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/009-comprehensive-e2e-coverage/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── spec.md              # Feature specification
├── checklists/
│   └── requirements.md  # Quality checklist
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
tests/DotnetMcp.E2E/
├── Features/
│   ├── SessionLifecycle.feature        # existing (4 scenarios)
│   ├── Breakpoints.feature             # existing (4 → ~12 scenarios)
│   ├── Stepping.feature                # existing (6 → ~12 scenarios)
│   ├── StackTrace.feature              # existing (2 → ~6 scenarios)
│   ├── VariableInspection.feature      # existing (11 → ~18 scenarios)
│   ├── ModuleEnumeration.feature       # existing (1 → ~6 scenarios)
│   ├── ExpressionEvaluation.feature    # NEW (~8 scenarios)
│   ├── SessionEdgeCases.feature        # NEW (~6 scenarios)
│   ├── ThreadInspection.feature        # NEW (~4 scenarios)
│   ├── ModuleTypeOperations.feature    # NEW (~8 scenarios)
│   ├── MemoryInspection.feature        # NEW (~6 scenarios)
│   └── DebugWorkflows.feature          # NEW (~6 scenarios)
├── StepDefinitions/
│   ├── SessionSteps.cs                 # existing + new steps
│   ├── BreakpointSteps.cs              # existing + new steps
│   ├── SteppingSteps.cs                # existing + new steps
│   ├── StackTraceSteps.cs              # existing + new steps
│   ├── InspectionSteps.cs              # existing + new steps
│   ├── ModuleSteps.cs                  # existing + new steps
│   ├── ExpressionSteps.cs              # NEW
│   ├── ThreadSteps.cs                  # NEW
│   └── ModuleTypeSteps.cs              # NEW
├── Hooks/
│   └── DebuggerHooks.cs                # existing (may need minor updates)
└── Support/
    └── DebuggerContext.cs               # existing (add new state properties)

tests/TestTargetApp/
├── Program.cs                           # existing (add new commands)
├── MethodTarget.cs                      # existing
├── LoopTarget.cs                        # existing
├── NestedTarget.cs                      # existing
├── ObjectTarget.cs                      # existing
├── DeepNestingTarget.cs                 # existing
├── ExpressionTarget.cs                  # NEW - expression eval targets
├── RecursionTarget.cs                   # NEW - deep recursion for stack trace
├── ThreadTarget.cs                      # NEW - multi-thread scenarios
└── Libs/
    ├── BaseTypes/                       # ADD: enums, structs, nullable types
    ├── Collections/                     # ADD: List, Dictionary, array scenarios
    ├── Exceptions/                      # ADD: try/catch/throw, custom exceptions
    ├── Recursion/                       # ADD: recursive methods
    ├── Expressions/                     # ADD: expression evaluation targets
    ├── Threading/                       # ADD: thread creation code
    ├── AsyncOps/                        # ADD: async method targets
    ├── MemoryStructs/                   # ADD: structs with known layout
    ├── ComplexObjects/                  # ADD: deeply nested objects
    └── Scenarios/                       # ADD: top-level integration scenarios
```

**Structure Decision**: Extend existing E2E project structure. New feature files for uncovered tool areas, new step definitions only where existing ones can't be reused. Test target code placed in categorized Libs per clarification.

## Design Decisions

### D1: Scenario Distribution Strategy

Target ~100 scenarios distributed as:

| Feature File | Current | Target | Tools Covered |
|-------------|---------|--------|---------------|
| SessionLifecycle | 4 | 4 | debug_attach, debug_launch, debug_disconnect, debug_continue |
| SessionEdgeCases (NEW) | 0 | 6 | debug_state, debug_pause, debug_disconnect (error paths) |
| Breakpoints | 4 | 12 | breakpoint_set, breakpoint_remove, breakpoint_list, breakpoint_enable, breakpoint_wait, breakpoint_set_exception |
| Stepping | 6 | 12 | debug_step (in/over/out), cross-assembly, exception handlers |
| StackTrace | 2 | 6 | stacktrace_get (deep recursion, cross-assembly, frame pagination) |
| VariableInspection | 11 | 18 | variables_get (collections, enums, nullables, generics) |
| ModuleEnumeration | 1 | 6 | modules_list (with/without system, after attach/launch) |
| ExpressionEvaluation (NEW) | 0 | 8 | evaluate (arithmetic, property, method, null-conditional, errors) |
| ThreadInspection (NEW) | 0 | 4 | threads_list |
| ModuleTypeOperations (NEW) | 0 | 8 | modules_search, types_get, members_get |
| MemoryInspection (NEW) | 0 | 6 | memory_read, object_inspect, references_get, layout_get |
| DebugWorkflows (NEW) | 0 | 6 | multi-step workflows combining multiple tools |
| **Total** | **28** | **~96** | **25/25 tools** |

### D2: Test Target Code Additions

Each categorized library gets scenario-specific code:

- **BaseTypes**: `TestEnum`, `TestStruct` (fields with known sizes), `NullableHolder`
- **Collections**: `CollectionHolder` (List<string>, Dictionary<string,int>, int[])
- **Exceptions**: `ExceptionThrower` (typed exceptions, nested try/catch)
- **Recursion**: `RecursiveCalculator` (factorial with breakpoint-friendly structure)
- **Expressions**: `ExpressionTarget` (properties, methods returning values, null refs)
- **Threading**: `ThreadSpawner` (creates N managed threads with barrier sync)
- **AsyncOps**: reserved for future async stack trace tests
- **MemoryStructs**: `LayoutStruct` (packed struct with known field offsets)
- **ComplexObjects**: `DeepObject` (3+ nesting levels for object_inspect depth tests)
- **Scenarios**: orchestration code calling across libraries

### D3: DebuggerContext Extensions

New properties needed:

```
LastThreads: IReadOnlyList<ThreadInfo>?
LastExpressionError: string?
LastDebugState: DebugStateInfo?
LastTypesResult: ...
LastMembersResult: ...
LastSearchResult: ...
LastMemoryResult: MemoryReadResult?
LastObjectInspection: ObjectInspectionResult?
LastReferences: ReferenceAnalysisResult?
LastTypeLayout: TypeLayoutResult?
```

Some of these already exist in the inspection steps — reuse where possible. Only add truly new state properties.

### D4: New TestTargetApp Commands

Add commands to Program.cs command loop:

- `"recurse"` → calls RecursiveCalculator for deep stack traces
- `"threads"` → spawns threads via ThreadSpawner
- `"collections"` → creates collection instances for variable inspection
- `"expressions"` → creates objects for expression evaluation
- `"structs"` → creates struct instances for layout/memory tests
- `"enums"` → creates enum variables for type inspection

### D5: Implementation Order

1. **Test target code first** — add scenario-specific code to libraries and Program.cs commands
2. **Feature files + step definitions** — grouped by tool coverage area, starting with P1 stories
3. **Verify incrementally** — run tests after each feature file addition

## Complexity Tracking

No constitution violations. No complexity justification needed.
