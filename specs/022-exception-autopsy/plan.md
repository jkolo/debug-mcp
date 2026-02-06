# Implementation Plan: Exception Autopsy

**Branch**: `022-exception-autopsy` | **Date**: 2026-02-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/022-exception-autopsy/spec.md`

## Summary

Add a new MCP tool `exception_get_context` that bundles all exception diagnosis data (exception type/message, inner exception chain, stack frames with source locations, and local variables for the throwing frame) into a single response. Also extend `breakpoint_wait` with an optional `include_autopsy` parameter. The tool reuses existing ICorDebug-based inspection APIs (`GetStackFrames`, `GetVariables`, `EvaluateAsync`) and composes their results into a structured bundle.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ClrDebug (ICorDebug wrappers), ModelContextProtocol SDK, System.Text.Json
**Storage**: N/A (in-memory, reads existing debugger state)
**Testing**: xUnit, FluentAssertions, Moq
**Target Platform**: Linux, Windows, macOS (.NET 10 SDK)
**Project Type**: Single project (DebugMcp)
**Performance Goals**: Default autopsy completes within 2 seconds for typical exceptions (≤20 frames, ≤50 locals)
**Constraints**: Read-only tool (no side effects on debugged process), must not break existing `breakpoint_wait` behavior
**Scale/Scope**: Single tool + service + models, ~6 new files, ~2 modified files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | Uses ICorDebug APIs exclusively (via existing ProcessDebugger methods) |
| II. MCP Compliance | PASS | Tool name `exception_get_context` follows `noun_verb` convention. Parameters have JSON Schema descriptions. Responses are structured JSON. Error responses use code/message format. |
| III. Test-First | PASS | Unit tests planned for service layer with mocked IProcessDebugger. Contract tests for tool schema. |
| IV. Simplicity | PASS | Thin tool layer delegates to service. Service composes existing APIs. No new abstractions beyond necessary models. Max 2 levels of indirection (Tool → Service → SessionManager/ProcessDebugger). |
| V. Observability | PASS | Tool logs invocation, parameters, duration, outcome (standard pattern from existing tools). |

**Post-Phase 1 Re-check**: All gates still pass. No new abstractions introduced beyond `IExceptionAutopsyService`. Variable expansion reuses existing `GetVariables` path. Inner exception traversal uses existing `EvaluateAsync`.

## Project Structure

### Documentation (this feature)

```text
specs/022-exception-autopsy/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 research findings
├── data-model.md        # Phase 1 data model
├── quickstart.md        # Phase 1 quickstart guide
├── contracts/           # Phase 1 API contracts
│   ├── exception_get_context.json
│   └── breakpoint_wait_autopsy.json
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
DebugMcp/
├── Models/Inspection/
│   ├── ExceptionAutopsyResult.cs    # NEW: Top-level result
│   ├── ExceptionDetail.cs           # NEW: Exception core info
│   ├── InnerExceptionEntry.cs       # NEW: Inner exception entry
│   ├── AutopsyFrame.cs              # NEW: Frame with variables
│   ├── FrameVariables.cs            # NEW: Variables + errors
│   └── VariableError.cs             # NEW: Error marker
├── Services/
│   ├── IExceptionAutopsyService.cs  # NEW: Interface
│   └── ExceptionAutopsyService.cs   # NEW: Implementation
├── Tools/
│   ├── ExceptionGetContextTool.cs   # NEW: MCP tool
│   └── BreakpointWaitTool.cs        # MODIFIED: add include_autopsy
└── Program.cs                       # MODIFIED: DI registration

tests/DebugMcp.Tests/
└── Unit/Inspection/
    └── ExceptionAutopsyServiceTests.cs  # NEW: Unit tests
```

**Structure Decision**: All new code lives in existing project structure. Models go in `Models/Inspection/` (alongside Variable, StackFrame). Service follows existing pattern (interface + implementation in `Services/`). Tool follows `[McpServerToolType]` convention in `Tools/`.

## Complexity Tracking

No constitution violations to justify. Feature stays within single project, uses existing APIs, introduces no new patterns.
