# Implementation Plan: Debug Session Management

**Branch**: `001-debug-session` | **Date**: 2026-01-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-debug-session/spec.md`

## Summary

Implement MCP tools for debug session management: `debug_attach`, `debug_launch`,
`debug_disconnect`, and `debug_state`. These tools provide the foundational
debugging capabilities that enable AI assistants to connect to .NET processes,
query session state, and cleanly disconnect. Implementation uses ICorDebug APIs
directly per the Native First principle.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: Microsoft.Diagnostics.Runtime (ClrMD), System.Text.Json,
  ModelContextProtocol SDK (for MCP server implementation)
**Storage**: N/A (in-memory session state only)
**Testing**: xUnit, Moq, FluentAssertions
**Target Platform**: Windows, Linux, macOS (cross-platform via .NET)
**Project Type**: Single project (CLI tool / MCP server)
**Performance Goals**: Attach within 5s, state queries within 100ms (per spec SC-001, SC-002)
**Constraints**: Single active session at a time, local debugging only (initial scope)
**Scale/Scope**: Single-user debugging tool, typical .NET application complexity

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Requirement | Status |
|-----------|-------------|--------|
| I. Native First | Use ICorDebug APIs directly, not DAP | ✅ PASS - Using ClrMD/ICorDebug |
| II. MCP Compliance | `noun_verb` naming, JSON Schema params, structured responses | ✅ PASS - Tools follow naming convention |
| III. Test-First | TDD mandatory, integration tests for attach/detach | ✅ PASS - Test plan includes contract + integration tests |
| IV. Simplicity | Max 3 indirection levels, YAGNI | ✅ PASS - Direct implementation, no premature abstractions |
| V. Observability | Structured logging, tool invocation logging | ✅ PASS - Logging requirements included |

**MCP Tool Standards Check**:
- Naming: `debug_attach`, `debug_launch`, `debug_disconnect`, `debug_state` ✅
- Parameters: PID as int, timeout optional with 30s default ✅
- Responses: Structured JSON with session details or error objects ✅

## Project Structure

### Documentation (this feature)

```text
specs/001-debug-session/
├── plan.md              # This file
├── research.md          # Phase 0: ICorDebug research, ClrMD patterns
├── data-model.md        # Phase 1: DebugSession, SessionState entities
├── quickstart.md        # Phase 1: Usage examples
├── contracts/           # Phase 1: MCP tool schemas
└── tasks.md             # Phase 2: Implementation tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
DotnetMcp/
├── Program.cs           # MCP server entry point
├── Tools/               # MCP tool implementations
│   ├── DebugAttachTool.cs
│   ├── DebugLaunchTool.cs
│   ├── DebugDisconnectTool.cs
│   └── DebugStateTool.cs
├── Services/            # Core debugging services
│   ├── DebugSessionManager.cs
│   └── ProcessDebugger.cs
├── Models/              # Domain models
│   ├── DebugSession.cs
│   ├── SessionState.cs
│   └── ProcessInfo.cs
└── Infrastructure/      # Cross-cutting concerns
    └── Logging.cs

DotnetMcp.Tests/
├── Contract/            # MCP schema validation tests
│   └── DebugToolsContractTests.cs
├── Integration/         # End-to-end debugging tests
│   ├── AttachTests.cs
│   ├── LaunchTests.cs
│   └── DisconnectTests.cs
└── Unit/                # Service unit tests
    ├── DebugSessionManagerTests.cs
    └── ProcessDebuggerTests.cs
```

**Structure Decision**: Single project structure with DotnetMcp as the main
executable and DotnetMcp.Tests as the test project. Tools are organized in a
dedicated folder, with Services containing the core debugging logic and Models
holding domain entities.

## Complexity Tracking

> No violations to justify - design follows Simplicity principle.
