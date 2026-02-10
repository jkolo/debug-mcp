# Implementation Plan: MCP Tool Annotations & Best Practices

**Branch**: `024-mcp-best-practices` | **Date**: 2026-02-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/024-mcp-best-practices/spec.md`

## Summary

Add MCP tool annotations (Title, ReadOnly, Destructive, Idempotent, OpenWorld) to all 34 tools, enhance descriptions for 10 key tools with JSON response examples, and add automated annotation verification tests. The SDK (ModelContextProtocol 0.7.0-preview.1) already supports these properties on `[McpServerTool]` — this feature uses them. Annotations are purely additive compile-time metadata with no behavioral changes.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ModelContextProtocol SDK 0.7.0-preview.1, ClrDebug 0.3.4
**Storage**: N/A (annotations are compile-time metadata)
**Testing**: xUnit 2.9.3, FluentAssertions 7.x, Moq 4.20.72
**Target Platform**: Linux (x64)
**Project Type**: Single project (DebugMcp)
**Performance Goals**: N/A (annotations add zero runtime overhead)
**Constraints**: All 34 tools must be annotated; JSON response examples embedded in `[Description]` strings
**Scale/Scope**: 34 tool files to annotate, 10 descriptions to enhance, 1 new test file

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | No changes to debugging APIs; annotations are metadata only |
| II. MCP Compliance | PASS | Annotations follow MCP 2025-11-25 spec; tool descriptions follow Anthropic best practices |
| III. Test-First (NON-NEGOTIABLE) | PASS | Annotation verification tests will be written before any new implementation; existing tests unaffected |
| IV. Simplicity | PASS | No new abstractions; one test file with a data-driven approach |
| V. Observability | PASS | No new operational behavior; tool invocation logging unchanged |

No violations. No complexity tracking needed.

## Project Structure

### Documentation (this feature)

```text
specs/024-mcp-best-practices/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0: SDK API research, test approach
├── quickstart.md        # Phase 1: How to verify the feature
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
DebugMcp/Tools/                          # 34 tool files (already annotated)
├── BreakpointListTool.cs                # ReadOnly=true, Idempotent=true
├── BreakpointSetTool.cs                 # ReadOnly=false, Destructive=false, enhanced description
├── BreakpointWaitTool.cs                # ReadOnly=true, Idempotent=false, enhanced description
├── DebugLaunchTool.cs                   # Destructive=true, enhanced description
├── EvaluateTool.cs                      # ReadOnly=true, Idempotent=false, enhanced description
├── ... (29 more tools)
└── VariablesGetTool.cs                  # ReadOnly=true, Idempotent=true, enhanced description

tests/DebugMcp.Tests/Contract/
└── ToolAnnotationTests.cs               # NEW: annotation verification tests
```

**Structure Decision**: Annotations go inline in existing tool files. One new test file in the existing `Contract/` directory alongside `SchemaValidationTests.cs` and per-tool contract tests. No new projects, directories, or abstractions needed.

## Implementation Details

### Part 1: Tool Annotations (FR-001 through FR-007) — DONE

All 34 tools already have `Title`, `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld` set on their `[McpServerTool]` attributes matching the classification table. Build passes with 0 errors, 895 tests pass.

### Part 2: JSON Response Examples in Descriptions (FR-008, FR-009)

Add concrete JSON response examples to the `[Description]` text of 10 key tools. Each example shows the success response shape with field names and representative values. Examples are embedded directly in the description string so AI clients see them in the tool listing.

**Tools requiring JSON examples** (all already have field-level docs, need actual JSON snippets added):

1. `debug_launch` — session object with processId, processName, state, etc.
2. `breakpoint_set` — breakpoint object with id, location, state, condition, hitCount
3. `breakpoint_wait` — hit result with breakpointId, threadId, timestamp, location
4. `debug_continue` — session state update
5. `debug_step` — session state with new source location
6. `variables_get` — array of variable objects with name, type, value, scope
7. `evaluate` — evaluation result with value, type, has_children
8. `stacktrace_get` — array of frames with index, function, module, source location
9. `exception_get_context` — exception autopsy with type, message, stack, locals
10. `debug_disconnect` — disconnect status with previousSession info

**Format**: JSON examples are appended to the existing description text using the pattern:
```
Example response: {"success": true, "field": "value", ...}
```

This keeps examples concise (single line) and readable within a `[Description("...")]` string attribute.

### Part 3: Annotation Verification Tests (FR-010, FR-011, FR-012)

Create `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs` with:

1. **Expected annotations dictionary** — maps each tool name to its expected `Title`, `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld` values (34 entries matching the spec classification table).

2. **Per-tool assertion test** (`[Theory]` with `[MemberData]`) — discovers all tool methods via reflection on `McpServerToolTypeAttribute` + `McpServerToolAttribute`, then asserts each annotation property matches the expected value. Uses FluentAssertions with `because` messages that identify the tool name and mismatched property (FR-012).

3. **Coverage check test** (`[Fact]`) — discovers all registered tools via reflection and verifies every one has a corresponding entry in the expected annotations dictionary. Fails if a tool is missing (FR-011).

4. **Description content tests** (`[Theory]`) — for the 10 enhanced tools, verifies the description contains a JSON response example (presence of `"success"` pattern).

**Tool discovery approach**: Reflection on `typeof(DebugLaunchTool).Assembly` (any tool class to get the assembly) → find types with `[McpServerToolType]` → find methods with `[McpServerTool]` → read attribute properties. No MCP server startup or mocking needed.

**Test naming convention**: `ToolAnnotationTests` in the `Contract/` directory, following the existing `SchemaValidationTests` pattern.

## Task Order

| # | Task | Depends On | Status |
|---|------|------------|--------|
| 1 | Write annotation verification tests (Part 3) | — | Pending |
| 2 | Run tests — they should pass for annotations (Part 1 already done) | 1 | Pending |
| 3 | Add JSON response examples to 10 enhanced descriptions (Part 2) | — | Pending |
| 4 | Run description content tests — should now pass | 1, 3 | Pending |
| 5 | Final build + full test suite | 2, 4 | Pending |

Note: Tasks 1 and 3 are independent and can be parallelized. Per Test-First constitution principle, annotation tests (Task 1) are written first and should pass immediately since annotations are already applied.
