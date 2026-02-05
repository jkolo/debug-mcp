# Implementation Plan: MCP Resources for Debugger State

**Branch**: `019-mcp-resources` | **Date**: 2026-02-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/019-mcp-resources/spec.md`

## Summary

Expose debugger state as MCP Resources — read-only data views that LLM clients can browse via `resources/list` and `resources/read` without calling tools. Four resources: session state (`debugger://session`), breakpoints (`debugger://breakpoints`), threads (`debugger://threads`), and source code (`debugger://source/{file}`). Resources are dynamically available only during active debug sessions, with debounced change notifications via MCP subscriptions.

Technical approach: Use attribute-based resource registration (`[McpServerResourceType]`/`[McpServerResource]`) with custom list/read handlers that gate access on session state. Manual notification dispatch (workaround for SDK bug). Event-driven change detection via existing `IProcessDebugger` events + new `BreakpointRegistry.Changed` event.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ModelContextProtocol SDK 0.1.0-preview.13, ClrDebug 0.3.4 (ICorDebug wrappers), System.Reflection.Metadata (PDB reading)
**Storage**: N/A (in-memory state within debug session)
**Testing**: xUnit, Moq, FluentAssertions
**Target Platform**: Linux (primary), cross-platform
**Project Type**: Single project (MCP server CLI tool)
**Performance Goals**: Resource reads < 500ms, notifications delivered within 1s of state change
**Constraints**: Debounce notifications at 300ms per-resource, source files restricted to PDB-referenced paths only
**Scale/Scope**: < 100 breakpoints, < 200 threads typical; single-client stdio transport

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | Resources read state from existing ICorDebug-based services. No external debugger dependency. |
| II. MCP Compliance | PASS | Uses standard MCP Resources protocol (resources/list, resources/read, resources/subscribe). Structured JSON responses. Error objects with actionable messages. |
| III. Test-First | PASS | TDD workflow planned. Unit tests for each resource provider method, notifier, and cache. Contract tests for MCP resource schemas. |
| IV. Simplicity | PASS | 4 new files in a new Services/Resources/ directory. No new abstractions beyond what MCP protocol requires. Direct service injection, no repository pattern. |
| V. Observability | PASS | Resource reads logged with URI and duration. Notification sends logged. Subscription changes logged. |

**Post-Phase 1 Re-check**: All gates still pass. No new projects, no unnecessary abstractions. Resource handlers follow same DI injection pattern as existing tools.

## Project Structure

### Documentation (this feature)

```text
specs/019-mcp-resources/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0: SDK API research, design decisions
├── data-model.md        # Phase 1: Resource JSON schemas
├── quickstart.md        # Phase 1: Architecture overview
├── contracts/
│   └── mcp-resources.md # Phase 1: MCP protocol contracts
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
DebugMcp/
├── Services/
│   ├── Resources/                          # NEW — resource infrastructure
│   │   ├── DebuggerResourceProvider.cs     # [McpServerResourceType] handlers
│   │   ├── ResourceNotifier.cs             # Debounced notifications + subscriptions
│   │   ├── AllowedSourcePaths.cs           # PDB path security boundary
│   │   └── ThreadSnapshotCache.cs          # Stale thread snapshot cache
│   └── Breakpoints/
│       └── BreakpointRegistry.cs           # MODIFIED — add Changed event
├── Program.cs                              # MODIFIED — register resources + capabilities

tests/DebugMcp.Tests/
└── Unit/
    └── Resources/                          # NEW — unit tests
        ├── DebuggerResourceProviderTests.cs
        ├── ResourceNotifierTests.cs
        ├── AllowedSourcePathsTests.cs
        └── ThreadSnapshotCacheTests.cs
```

**Structure Decision**: Follows existing project layout. New `Services/Resources/` directory mirrors the pattern of `Services/Breakpoints/` and `Services/CodeAnalysis/`. Tests in `tests/DebugMcp.Tests/Unit/Resources/` mirror the `Unit/Breakpoints/` pattern.
