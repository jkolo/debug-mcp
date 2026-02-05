# Feature Specification: Disable Roslyn Code Analysis Tools

**Feature Branch**: `018-resharper-code-analysis`
**Created**: 2026-02-05
**Status**: Rejected
**Input**: User description: "Dodajmy przełącznik do wyłączania Roslynowych Analizatorów, tak żeby ktoś kto ma JetBrains MCP nie miał zdublowanych funkcjonalności"

## Summary

Add a `--no-roslyn` CLI flag that disables the 5 Roslyn-based code analysis MCP tools (`code_load`, `code_goto_definition`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics`). Users who have JetBrains MCP server connected (via Rider IDE) get equivalent functionality from the JetBrains tools and don't need duplicates from DebugMcp.

When `--no-roslyn` is set:
- The 5 Code* tools are not registered in MCP (not visible to LLM clients)
- `ICodeAnalysisService` is not instantiated (no MSBuild/Roslyn overhead)
- All other debugging tools work normally

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Disable Roslyn Tools via CLI Flag (Priority: P1)

As a user running DebugMcp alongside Rider's JetBrains MCP server, I want to disable DebugMcp's Roslyn-based code analysis tools so the LLM doesn't see duplicate functionality.

**Why this priority**: This is the entire feature — a single flag toggle.

**Independent Test**: Start DebugMcp with `--no-roslyn`, list available tools, verify Code* tools are absent.

**Acceptance Scenarios**:

1. **Given** DebugMcp started with `--no-roslyn`, **When** an LLM lists available tools, **Then** `code_load`, `code_goto_definition`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics` are NOT present.

2. **Given** DebugMcp started without `--no-roslyn`, **When** an LLM lists available tools, **Then** all 5 code analysis tools ARE present (existing behavior, backward compatible).

3. **Given** DebugMcp started with `--no-roslyn`, **When** debugging tools are used (`debug_attach`, `breakpoint_set`, etc.), **Then** they work normally — only code analysis is disabled.

---

### User Story 2 - Startup Logging (Priority: P2)

As an operator, I want to see in the logs whether Roslyn code analysis is enabled or disabled, so I can verify the configuration.

**Why this priority**: Observability — secondary to the core toggle functionality.

**Independent Test**: Start DebugMcp with and without `--no-roslyn`, check startup logs.

**Acceptance Scenarios**:

1. **Given** DebugMcp started with `--no-roslyn`, **When** it initializes, **Then** a log message at Info level states "Roslyn code analysis tools disabled (--no-roslyn)".

2. **Given** DebugMcp started without `--no-roslyn`, **When** it initializes, **Then** a log message at Info level states "Roslyn code analysis tools enabled".

---

### Edge Cases

- `--no-roslyn` combined with `--stderr-logging`: both flags should work independently.
- Future tools that depend on `ICodeAnalysisService`: if added later, they should also be excluded when `--no-roslyn` is set (documented convention).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `--no-roslyn` CLI flag (with alias `-r`) to disable Roslyn code analysis tools
- **FR-002**: When `--no-roslyn` is set, the 5 Code* MCP tools MUST NOT be registered
- **FR-003**: When `--no-roslyn` is set, `ICodeAnalysisService` and `CodeAnalysisService` MUST NOT be registered in DI
- **FR-004**: When `--no-roslyn` is not set, behavior MUST be identical to current (100% backward compatible)
- **FR-005**: System MUST log the Roslyn code analysis enabled/disabled state at startup

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing tests pass unchanged when `--no-roslyn` is NOT set
- **SC-002**: With `--no-roslyn`, tool listing shows 0 code analysis tools
- **SC-003**: With `--no-roslyn`, startup time does not include MSBuild locator registration

## Dependencies

- Feature 015 (Roslyn Code Analysis) — the tools being toggled
- System.CommandLine — existing CLI infrastructure

## Out of Scope

- Proxying to JetBrains MCP server
- Loading Rider.Backend DLLs
- Any changes to existing Roslyn tool behavior when enabled
