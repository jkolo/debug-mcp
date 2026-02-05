# Implementation Plan: Roslyn Code Analysis

**Branch**: `015-roslyn-code-analysis` | **Date**: 2026-02-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/015-roslyn-code-analysis/spec.md`

## Summary

Add Roslyn-based static code analysis to the MCP debugger server, enabling LLM agents to navigate C# codebases without running the debugger. The implementation uses Microsoft.CodeAnalysis.Workspaces (MSBuildWorkspace) to load solutions/projects and provides MCP tools for finding symbol usages, tracking variable assignments, retrieving diagnostics, and navigating to definitions.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: Microsoft.CodeAnalysis.Workspaces.MSBuild, Microsoft.Build.Locator, ModelContextProtocol SDK
**Storage**: N/A (in-memory workspace per session)
**Testing**: xUnit, Reqnroll, FluentAssertions
**Target Platform**: Linux (primary), Windows, macOS
**Project Type**: Single project (extends existing DebugMcp)
**Performance Goals**: Symbol search <2s for <50 files, solution load <30s for <50 projects (per SC-001, SC-002)
**Constraints**: Independent of debug session (FR-010), MCP progress notifications for loading (FR-011)
**Scale/Scope**: Typical solutions up to 100 projects, millions of LOC supported

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ PASS | Roslyn is .NET native; no external IDE/DAP required |
| II. MCP Compliance | ✅ PASS | All tools will follow `noun_verb` naming, JSON responses |
| III. Test-First | ✅ PASS | Tests will be written before implementation |
| IV. Simplicity | ✅ PASS | Single workspace, direct Roslyn APIs, no unnecessary abstractions |
| V. Observability | ✅ PASS | Tool invocation logging, progress notifications |

**Gate Result**: PASS - No violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/015-roslyn-code-analysis/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (MCP tool schemas)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
DebugMcp/
├── Models/
│   └── CodeAnalysis/           # NEW: Roslyn analysis models
│       ├── WorkspaceInfo.cs
│       ├── SymbolUsage.cs
│       ├── SymbolAssignment.cs
│       ├── SymbolDefinition.cs
│       └── DiagnosticInfo.cs
├── Services/
│   └── CodeAnalysis/           # NEW: Roslyn analysis service
│       ├── ICodeAnalysisService.cs
│       └── CodeAnalysisService.cs
├── Tools/
│   ├── CodeLoadTool.cs         # NEW: code_load
│   ├── CodeFindUsagesTool.cs   # NEW: code_find_usages
│   ├── CodeFindAssignmentsTool.cs # NEW: code_find_assignments
│   ├── CodeGetDiagnosticsTool.cs  # NEW: code_get_diagnostics
│   └── CodeGoToDefinitionTool.cs  # NEW: code_goto_definition
└── Program.cs                  # Update: register new services

tests/
├── DebugMcp.Tests/
│   └── Unit/
│       └── CodeAnalysis/       # NEW: Unit tests for models/services
└── DebugMcp.E2E/
    └── Features/
        └── CodeAnalysis/       # NEW: E2E feature tests
            └── CodeAnalysis.feature
```

**Structure Decision**: Extends existing single-project structure with new `CodeAnalysis` subdirectories in Models, Services, and Tools. Follows established patterns from existing debugging tools.

## Complexity Tracking

> No violations detected. Table left empty per constitution guidance.
