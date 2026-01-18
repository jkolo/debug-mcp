# Implementation Plan: Breakpoint Operations

**Branch**: `002-breakpoint-ops` | **Date**: 2026-01-18 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-breakpoint-ops/spec.md`
**Depends On**: 001-debug-session (requires active debug session infrastructure)

## Summary

Implement MCP tools for breakpoint operations in the .NET debugger: `breakpoint_set` (create at file/line with optional column for lambda targeting), `breakpoint_remove` (delete by ID), `breakpoint_list` (enumerate all), `breakpoint_wait` (block until hit), `breakpoint_enable` (toggle), and `breakpoint_set_exception` (break on exception types). Uses ICorDebugCode.CreateBreakpoint for IL-level breakpoints, System.Reflection.Metadata for source-to-IL mapping via Portable PDB sequence points, and ICorDebugManagedCallback2.Exception for exception breakpoints.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ClrDebug (ICorDebug wrappers), ModelContextProtocol SDK, System.Reflection.Metadata (in-box for PDB reading)
**Storage**: N/A (in-memory breakpoint registry within session)
**Testing**: xUnit, Moq, FluentAssertions
**Target Platform**: Linux (primary), Windows (secondary)
**Project Type**: Single project (MCP server)
**Performance Goals**: Set breakpoint <2s (SC-001), Wait response <100ms after hit (SC-002)
**Constraints**: Must support pending breakpoints for unloaded modules
**Scale/Scope**: Single debug session, up to 100s of breakpoints

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Design Check (PASSED)

| Principle | Status | Evidence |
|-----------|--------|----------|
| **Native First** | PASS | Uses ICorDebug directly via ClrDebug, Portable PDB via System.Reflection.Metadata |
| **MCP Compliance** | PASS | All 6 tools follow MCP JSON schema patterns, structured error responses |
| **Test-First (TDD)** | PASS | Tasks include tests before implementation in each user story phase |
| **Simplicity** | PASS | Single project, no external databases, no abstraction layers beyond ICorDebug |
| **Observability** | PASS | Structured logging for all breakpoint operations, error codes for all failure modes |

### Post-Design Check (PASSED)

| Principle | Status | Evidence |
|-----------|--------|----------|
| **Native First** | PASS | ICorDebugCode.CreateBreakpoint, ICorDebugFunctionBreakpoint.Activate, ICorDebugManagedCallback2.Exception - all native APIs |
| **MCP Compliance** | PASS | contracts/breakpoint-tools.json defines JSON Schema for all tools with proper error codes |
| **Test-First (TDD)** | PASS | 92 tasks organized with tests before implementation per user story |
| **Simplicity** | PASS | BreakpointManager + PdbSymbolReader + Registry - minimal abstractions needed |
| **Observability** | PASS | Each tool includes logging task, error codes enumerated in data-model.md |

## Project Structure

### Documentation (this feature)

```text
specs/002-breakpoint-ops/
├── plan.md              # This file
├── spec.md              # Feature specification with user stories
├── research.md          # ICorDebug breakpoint APIs, PDB sequence points
├── data-model.md        # Breakpoint, BreakpointLocation, BreakpointHit entities
├── quickstart.md        # Usage examples for all breakpoint tools
├── contracts/
│   └── breakpoint-tools.json  # JSON Schema for MCP tools
└── tasks.md             # 92 implementation tasks
```

### Source Code (repository root)

```text
DotnetMcp/
├── DotnetMcp.csproj
├── Program.cs                          # MCP server entry, DI registration
├── Models/
│   ├── Breakpoints/
│   │   ├── Breakpoint.cs               # Core breakpoint model
│   │   ├── BreakpointState.cs          # Pending/Bound/Disabled enum
│   │   ├── BreakpointLocation.cs       # File/line/column record
│   │   ├── BreakpointHit.cs            # Hit event record
│   │   ├── ExceptionBreakpoint.cs      # Exception breakpoint model
│   │   └── ExceptionInfo.cs            # Exception details record
│   └── [existing from 001-debug-session]
├── Services/
│   ├── Breakpoints/
│   │   ├── IBreakpointManager.cs       # Breakpoint operations interface
│   │   ├── BreakpointManager.cs        # Main implementation
│   │   ├── BreakpointRegistry.cs       # Breakpoint storage/lookup
│   │   ├── ExceptionBreakpointRegistry.cs
│   │   ├── BreakpointHitQueue.cs       # Hit event queue
│   │   ├── IPdbSymbolReader.cs         # PDB reading interface
│   │   ├── PdbSymbolReader.cs          # Source-to-IL mapping
│   │   ├── PdbSymbolCache.cs           # MetadataReaderProvider cache
│   │   ├── IConditionEvaluator.cs      # Condition evaluation interface
│   │   ├── SimpleConditionEvaluator.cs # Local evaluation (literals, hit counts)
│   │   └── DebuggerConditionEvaluator.cs # ICorDebugEval-based evaluation
│   └── [existing from 001-debug-session]
└── Tools/
    ├── BreakpointSetTool.cs            # breakpoint_set MCP tool
    ├── BreakpointSetExceptionTool.cs   # breakpoint_set_exception MCP tool
    ├── BreakpointRemoveTool.cs         # breakpoint_remove MCP tool
    ├── BreakpointListTool.cs           # breakpoint_list MCP tool
    ├── BreakpointWaitTool.cs           # breakpoint_wait MCP tool
    ├── BreakpointEnableTool.cs         # breakpoint_enable MCP tool
    └── [existing from 001-debug-session]

tests/DotnetMcp.Tests/
├── Contract/
│   ├── BreakpointSetContractTests.cs
│   ├── BreakpointSetExceptionContractTests.cs
│   ├── BreakpointRemoveContractTests.cs
│   ├── BreakpointListContractTests.cs
│   ├── BreakpointWaitContractTests.cs
│   ├── BreakpointEnableContractTests.cs
│   └── SchemaValidationTests.cs
├── Integration/
│   ├── SetBreakpointTests.cs
│   ├── WaitBreakpointTests.cs
│   ├── ListBreakpointsTests.cs
│   ├── RemoveBreakpointTests.cs
│   ├── ConditionalBreakpointTests.cs
│   └── ExceptionBreakpointTests.cs
└── Unit/
    ├── BreakpointManagerTests.cs
    ├── BreakpointTests.cs
    ├── PdbSymbolReaderTests.cs
    ├── ConditionEvaluatorTests.cs
    └── ExceptionBreakpointTests.cs
```

**Structure Decision**: Single project structure following 001-debug-session pattern. Breakpoint models and services organized in dedicated subdirectories to maintain clear separation from debug session infrastructure.

## Complexity Tracking

> No violations - design passes all Constitution principles.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | - | - |
