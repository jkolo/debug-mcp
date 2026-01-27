# Implementation Plan: Debug Launch

**Branch**: `007-debug-launch` | **Date**: 2026-01-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/007-debug-launch/spec.md`

## Summary

Implement `debug_launch` MCP tool to start .NET processes under debugger control using DbgShim's `CreateProcessForLaunch` and `RegisterForRuntimeStartup` APIs. This allows debugging from the very first instruction, supports stopAtEntry, command-line arguments, working directory, and environment variables.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ClrDebug 0.3.4 (ICorDebug wrappers), ModelContextProtocol SDK 0.1.0-preview.13, Microsoft.Diagnostics.DbgShim.linux-x64 9.0.661903
**Storage**: N/A (in-memory debug session state)
**Testing**: xUnit, FluentAssertions, Moq (contract + integration tests)
**Target Platform**: Linux x64 (primary), Windows x64 (future)
**Project Type**: Single - MCP server application
**Performance Goals**: Launch completes in under 5 seconds
**Constraints**: Process must be paused before user code executes (when stopAtEntry=true)
**Scale/Scope**: Single debug session at a time

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | Uses ICorDebug APIs via ClrDebug + DbgShim directly |
| II. MCP Compliance | PASS | Tool already defined as `debug_launch` with proper parameters |
| III. Test-First | GATE | Must write tests before implementation |
| IV. Simplicity | PASS | Direct approach using existing DbgShim APIs |
| V. Observability | PASS | Logging infrastructure already in place |

## Project Structure

### Documentation (this feature)

```text
specs/007-debug-launch/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
DotnetMcp/
├── Models/
│   ├── ProcessInfo.cs           # Already exists
│   ├── DebugSession.cs          # Already exists
│   └── LaunchMode.cs            # Already exists
├── Services/
│   ├── ProcessDebugger.cs       # LaunchAsync implementation needed
│   ├── IProcessDebugger.cs      # Interface already defined
│   └── DebugSessionManager.cs   # Already calls ProcessDebugger.LaunchAsync
├── Tools/
│   └── DebugLaunchTool.cs       # MCP tool already defined
└── Infrastructure/
    └── Logging.cs               # Launch logging already defined

tests/
├── DotnetMcp.Tests/
│   ├── Contract/
│   │   └── DebugLaunchToolTests.cs  # Contract tests for MCP tool
│   └── Integration/
│       └── LaunchIntegrationTests.cs # End-to-end launch tests
└── TestTargetApp/                    # Test application to launch
    └── Program.cs
```

**Structure Decision**: Existing single-project structure. Implementation goes into `ProcessDebugger.cs`, tests into dedicated test files.

## Complexity Tracking

No constitution violations - simple implementation using existing infrastructure.
