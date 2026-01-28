# Research: Comprehensive E2E Test Coverage

**Date**: 2026-01-28 | **Branch**: `009-comprehensive-e2e-coverage`

## No NEEDS CLARIFICATION Items

All technical context was resolved from existing codebase analysis. No external research required.

## Decisions

### R1: Test Target Code Organization

- **Decision**: Spread test scenario code across the 10 categorized library projects (BaseTypes, Collections, Exceptions, etc.)
- **Rationale**: User requested descriptive library names matching test domains. Each library's purpose aligns with a test coverage area.
- **Alternatives considered**: Single DebugScenarios.cs file (simpler but doesn't leverage multi-module structure); multiple files in TestTargetApp root (doesn't exercise cross-assembly scenarios).

### R2: Existing Step Definition Reuse

- **Decision**: Reuse existing step definitions wherever possible. Only create new step definition files for entirely new tool areas (ExpressionSteps, ThreadSteps, ModuleTypeSteps).
- **Rationale**: 6 step definition files already exist with comprehensive steps for inspection, breakpoints, sessions, stepping, stack traces, and modules. Adding to existing files avoids duplication.
- **Alternatives considered**: One step definition per feature file (more files, more duplication).

### R3: ICorDebug Thread Inspection Approach

- **Decision**: Use ManualResetEventSlim barrier in test target to ensure threads are alive when debugger inspects them.
- **Rationale**: Thread creation and termination is non-deterministic. A barrier ensures the debugger can pause and inspect while all threads are active.
- **Alternatives considered**: Thread.Sleep (unreliable timing); Console.ReadLine per thread (complex coordination).

### R4: Expression Evaluation Scope

- **Decision**: Test basic ICorDebug eval capabilities: arithmetic, property access, static method calls, null-conditional. Do not test complex LINQ or lambda expressions.
- **Rationale**: ICorDebug expression evaluation has known limitations. Testing realistic expressions used in debugging scenarios is more valuable than pushing language edge cases.
- **Alternatives considered**: Full C# expression coverage (impractical with ICorDebug eval constraints).
