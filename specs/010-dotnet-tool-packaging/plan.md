# Implementation Plan: .NET Tool Packaging

**Branch**: `010-dotnet-tool-packaging` | **Date**: 2026-01-28 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/010-dotnet-tool-packaging/spec.md`

## Summary

Package DotnetMcp as a .NET global/local tool so users can install it via `dotnet tool install -g dotnet-mcp` and run it immediately. The project needs PackAsTool configuration, NuGet metadata, `--version`/`--help` CLI flags, and documentation updates.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ModelContextProtocol SDK 0.1.0-preview.13, ClrDebug 0.3.4, Microsoft.Diagnostics.DbgShim.linux-x64 9.0.661903
**Storage**: N/A
**Testing**: xUnit, Reqnroll (E2E)
**Target Platform**: Linux x64 (framework-dependent, requires .NET 10 runtime)
**Project Type**: Single CLI application (MCP server over stdio)
**Performance Goals**: Tool startup under 5 seconds
**Constraints**: DbgShim native dependency must be bundled correctly in NuGet package
**Scale/Scope**: Single tool package, single platform (linux-x64)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ Pass | No changes to debugging architecture; packaging only |
| II. MCP Compliance | ✅ Pass | No tool API changes; packaging only |
| III. Test-First | ✅ Pass | Will add tests for CLI flags (--version, --help) and tool packaging |
| IV. Simplicity | ✅ Pass | Minimal changes: .csproj properties + CLI argument handling |
| V. Observability | ✅ Pass | No changes to logging; existing structured logging preserved |

All gates pass. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/010-dotnet-tool-packaging/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
DotnetMcp/
├── DotnetMcp.csproj     # Add PackAsTool, ToolCommandName, NuGet metadata
├── Program.cs           # Add --version, --help argument handling
└── ...                  # Existing source unchanged

tests/
├── DotnetMcp.Tests/     # Add CLI argument tests
└── DotnetMcp.E2E/       # Existing E2E tests unchanged
```

**Structure Decision**: No new projects needed. Changes are confined to the existing DotnetMcp.csproj (packaging properties) and Program.cs (CLI flags). This follows the Simplicity principle.
