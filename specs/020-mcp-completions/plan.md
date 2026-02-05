# Implementation Plan: MCP Completions for Debugger Expressions

**Branch**: `020-mcp-completions` | **Date**: 2026-02-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/020-mcp-completions/spec.md`

## Summary

Implement MCP `completion/complete` request handling for debugger expression autocompletion. This enables LLM clients to request completions for variable names, object members, and type names when writing evaluation expressions, reducing invalid expression errors.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ModelContextProtocol SDK 0.7.0-preview.1, ClrDebug 0.3.4 (ICorDebug wrappers)
**Storage**: N/A (in-memory, uses existing debugger state)
**Testing**: xUnit, Moq, FluentAssertions
**Target Platform**: Linux (primary), cross-platform via .NET
**Project Type**: Single project (existing DebugMcp structure)
**Performance Goals**: <100ms for typical completions (under 50 variables)
**Constraints**: Must work when paused, return empty when running
**Scale/Scope**: Single debug session, typical <100 variables per scope

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ PASS | Uses ICorDebug APIs via ClrDebug for variable/type enumeration |
| II. MCP Compliance | ✅ PASS | Implements standard `completion/complete` protocol method |
| III. Test-First | ✅ PASS | Tests written before implementation (TDD) |
| IV. Simplicity | ✅ PASS | Direct implementation, no unnecessary abstractions |
| V. Observability | ✅ PASS | Structured logging for completion requests |

## Project Structure

### Documentation (this feature)

```text
specs/020-mcp-completions/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── mcp-completions.md
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
DebugMcp/
├── Services/
│   └── Completions/           # NEW - completion provider
│       ├── ExpressionCompletionProvider.cs
│       └── CompletionContextParser.cs
├── Tools/
│   └── EvaluateTool.cs        # EXISTING - reference for expression context
└── Program.cs                 # MODIFY - register completion handler

tests/DebugMcp.Tests/
└── Unit/
    └── Completions/           # NEW - completion unit tests
        ├── ExpressionCompletionProviderTests.cs
        └── CompletionContextParserTests.cs
```

**Structure Decision**: Follows existing single-project pattern. New `Services/Completions/` directory mirrors `Services/Resources/` from Feature 019.

## Complexity Tracking

> No violations. Design stays within constitution limits.
