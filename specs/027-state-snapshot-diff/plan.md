# Implementation Plan: State Snapshot & Diff

**Branch**: `027-state-snapshot-diff` | **Date**: 2026-02-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/027-state-snapshot-diff/spec.md`

## Summary

Add snapshot capture and diff capabilities to the debugger. When paused, agents can capture all variables in the current frame as a named snapshot (`snapshot_create`), then compare two snapshots to see what changed (`snapshot_diff`). This replaces N individual `variables_get` calls + manual comparison with a 2-call workflow. Implemented as a `SnapshotStore` (ConcurrentDictionary storage) + 4 MCP tools, following the BreakpointRegistry/Manager pattern.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ModelContextProtocol SDK 0.7.0-preview.1, ClrDebug 0.3.4 (ICorDebug)
**Storage**: In-memory only (ConcurrentDictionary), session-scoped
**Testing**: xUnit + FluentAssertions + Moq
**Target Platform**: linux-x64 (primary), win-x64, osx-arm64, osx-x64
**Project Type**: Single project (.NET tool)
**Performance Goals**: Snapshot capture <2s for 50 variables at depth 0; diff <1s for 200 variables
**Constraints**: Snapshots store string representations (same as `variables_get`), max 100 soft limit
**Scale/Scope**: 4 new MCP tools, 4 new model records, 1 new service, ~15 new files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | Uses ICorDebug via existing `ProcessDebugger.GetVariables()` for capture |
| II. MCP Compliance | PASS | 4 tools with `noun_verb` naming (`snapshot_create`, `snapshot_diff`, `snapshot_list`, `snapshot_delete`), structured JSON responses, JSON Schema parameters |
| III. Test-First | PASS | TDD workflow: unit tests for SnapshotStore, SnapshotService, each tool; contract tests for schemas |
| IV. Simplicity | PASS | No new abstractions — follows BreakpointRegistry pattern. Store + Service + Tools. Max 2 levels of indirection (Tool → Service → Store) |
| V. Observability | PASS | Structured logging for snapshot create/delete/diff operations via ILogger |

**Gate result**: All principles satisfied. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/027-state-snapshot-diff/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
DebugMcp/
├── Models/Snapshots/
│   ├── Snapshot.cs                # Snapshot record (ID, label, timestamp, variables)
│   ├── SnapshotVariable.cs       # Captured variable (name, type, value, scope, children)
│   ├── SnapshotDiff.cs           # Diff result (added, removed, modified)
│   └── DiffEntry.cs              # Single change entry (name, type, old/new value)
├── Services/Snapshots/
│   ├── ISnapshotStore.cs         # Store interface
│   ├── SnapshotStore.cs          # ConcurrentDictionary-based storage
│   ├── ISnapshotService.cs       # Service interface (create, diff, list, delete)
│   └── SnapshotService.cs        # Orchestration (capture variables, build diffs)
└── Tools/
    ├── SnapshotCreateTool.cs     # snapshot_create MCP tool
    ├── SnapshotDiffTool.cs       # snapshot_diff MCP tool
    ├── SnapshotListTool.cs       # snapshot_list MCP tool
    └── SnapshotDeleteTool.cs     # snapshot_delete MCP tool

tests/DebugMcp.Tests/
├── Unit/Snapshots/
│   ├── SnapshotStoreTests.cs     # Store CRUD + thread safety
│   ├── SnapshotServiceTests.cs   # Capture, diff logic, edge cases
│   ├── SnapshotCreateToolTests.cs
│   ├── SnapshotDiffToolTests.cs
│   ├── SnapshotListToolTests.cs
│   └── SnapshotDeleteToolTests.cs
└── Contract/
    └── SnapshotToolContractTests.cs  # Schema validation for all 4 tools
```

**Structure Decision**: Follows existing project structure — Models in subdirectory (`Models/Snapshots/`), Services in subdirectory (`Services/Snapshots/`), Tools at top level of `Tools/`. Tests mirror the service structure under `Unit/Snapshots/`.

## Design Decisions

### D1: Storage Pattern — SnapshotStore (Registry Pattern)

Follow the `BreakpointRegistry` pattern: `ConcurrentDictionary<string, Snapshot>` with simple CRUD. No separate Manager class needed — snapshot operations don't require event-driven orchestration (no ICorDebug interaction beyond initial capture).

**Service split**: `SnapshotStore` (CRUD storage) + `SnapshotService` (capture logic using ProcessDebugger, diff algorithm). The service subscribes to `IProcessDebugger.StateChanged` for session cleanup.

### D2: Variable Capture — Reuse ProcessDebugger.GetVariables()

Snapshots capture variables by calling the existing `IDebugSessionManager.GetVariables()` path, which returns `List<Variable>`. This ensures consistency with `variables_get` tool output. For nested expansion (depth > 0), iterate `Variable.HasChildren` and expand recursively using the existing expand mechanism.

### D3: Diff Algorithm — Simple Set Comparison

Compare two snapshots by variable path (fully qualified name). Build three lists:
- **Added**: variables in snapshot B not in A (by path)
- **Removed**: variables in A not in B (by path)
- **Modified**: variables in both A and B with different `Value` strings

Use `Dictionary<string, SnapshotVariable>` keyed by path for O(n) comparison.

### D4: Tool Naming & Parameters

| Tool | Parameters | Response |
|------|-----------|----------|
| `snapshot_create` | `label?`, `thread_id?`, `frame_index=0`, `depth=0` | `{success, snapshot: {id, label, timestamp, threadId, frameIndex, functionName, variableCount}}` |
| `snapshot_diff` | `snapshot_id_1`, `snapshot_id_2` | `{success, diff: {added[], removed[], modified[], threadMismatch, timeDelta}}` |
| `snapshot_list` | (none) | `{success, snapshots: [{id, label, timestamp, threadId, functionName, variableCount}]}` |
| `snapshot_delete` | `snapshot_id?` | `{success, deleted}` — if no ID, clears all |

### D5: Session Cleanup

`SnapshotService` subscribes to `IProcessDebugger.StateChanged`. On `Disconnected`, calls `SnapshotStore.Clear()`. Fire-and-forget, matching `BreakpointManager` pattern.

## Complexity Tracking

> No constitution violations — this section is empty.
