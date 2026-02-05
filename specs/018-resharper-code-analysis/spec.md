# Feature Specification: ReSharper Code Analysis via JetBrains MCP

**Feature Branch**: `018-resharper-code-analysis`
**Created**: 2026-02-05
**Status**: Draft
**Input**: User description: "Dodajmy ficzer ktory wystawia narzedzia z Rider.Backend w MCP. Jezeli juz mielismy cos co dziala tak samo przez Roslyn, to powinno to byc zastapione resharperowa wersja. Dodatkowo powinien byc przelacznik wylaczajacy szukanie i uzycie Rider.Backend i zostajemy tylko przy Roslyn."

## Summary

Replace the existing Roslyn-based code analysis tools (feature 015) with a dual-backend architecture that prefers JetBrains Rider's code analysis capabilities when available. When DebugMcp detects a running JetBrains MCP server (exposed by Rider IDE), it delegates code analysis operations to it — getting access to ReSharper's superior inspections, code navigation, and diagnostics. When Rider is not available, or when the user explicitly disables it via a CLI flag (`--no-rider`), the system falls back to the existing Roslyn-based implementation seamlessly.

This is implemented as a **provider pattern**: a common `ICodeAnalysisService` interface backed by either `RiderCodeAnalysisProvider` (proxying to JetBrains MCP) or `RoslynCodeAnalysisProvider` (existing Roslyn implementation), selected at startup based on availability and configuration.

## Context

### Current State (Feature 015)

DebugMcp currently has 5 Roslyn-based code analysis tools:
- `code_load` — Load a .sln or .csproj into a Roslyn workspace
- `code_goto_definition` — Navigate to symbol definition
- `code_find_usages` — Find all references to a symbol
- `code_find_assignments` — Find all write locations for a variable/field/property
- `code_get_diagnostics` — Get compilation errors and warnings

These use Microsoft.CodeAnalysis (Roslyn) with MSBuildWorkspace. They work but are limited compared to what a full IDE backend provides:
- No ReSharper inspections (only compiler diagnostics)
- Slower solution loading (no caching)
- No support for advanced refactoring analysis
- No integration with .editorconfig/ReSharper settings for code style diagnostics

### JetBrains MCP Server

Rider 2025.3+ exposes a JetBrains MCP server that provides rich code analysis tools:
- `search_in_files_by_text` / `search_in_files_by_regex` — Full-text and regex search
- `get_file_problems` — ReSharper inspections (errors, warnings, suggestions, hints)
- `get_symbol_info` — Symbol info at location (documentation, declaration)
- `find_files_by_name_keyword` / `find_files_by_glob` — File discovery
- `build_project` — Build with diagnostics
- `get_file_text_by_path` — Read file content
- `rename_refactoring` — Rename symbol across project

These tools are richer than our Roslyn equivalents but require a running Rider instance.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Transparent Backend Selection (Priority: P1)

As an LLM agent using DebugMcp, I want code analysis tools to automatically use the best available backend, so I get the richest analysis possible without needing to know which provider is active.

**Why this priority**: This is the core value proposition — seamless switching between backends. Without this, users must manually manage which tools to call.

**Independent Test**: Can be tested by starting DebugMcp with and without a running Rider instance and verifying the same MCP tool names work in both cases, selecting the appropriate backend.

**Acceptance Scenarios**:

1. **Given** DebugMcp starts with a reachable JetBrains MCP server, **When** an LLM calls `code_get_diagnostics`, **Then** the result comes from ReSharper inspections (richer than pure Roslyn).

2. **Given** DebugMcp starts without a JetBrains MCP server, **When** an LLM calls `code_get_diagnostics`, **Then** the result comes from the Roslyn backend, same as feature 015 behavior.

3. **Given** DebugMcp starts with `--no-rider` flag, **When** a JetBrains MCP server is available, **Then** it is ignored and Roslyn is used exclusively.

4. **Given** the active backend is Rider, **When** the tool response is returned, **Then** it includes a `provider` field indicating "rider" or "roslyn" so the LLM knows which backend served the request.

---

### User Story 2 - ReSharper Diagnostics (Priority: P1)

As an LLM agent reviewing code quality, I want to get ReSharper-level diagnostics (not just compiler errors/warnings), so I can identify code smells, style issues, and potential bugs that the compiler wouldn't catch.

**Why this priority**: ReSharper inspections are the primary advantage over Roslyn — they provide 2500+ inspections vs ~200 compiler diagnostics.

**Independent Test**: Can be tested by loading a file with a ReSharper-detectable issue (e.g., unused using statement, possible null reference) and verifying the diagnostic is returned.

**Acceptance Scenarios**:

1. **Given** a file with ReSharper-detectable issues, **When** I call `code_get_diagnostics` for that file, **Then** I receive ReSharper inspection results including severity, description, and location.

2. **Given** a file that compiles cleanly but has style issues, **When** I call `code_get_diagnostics`, **Then** I receive warnings/suggestions from ReSharper that Roslyn alone would not detect.

3. **Given** a request for diagnostics with `errorsOnly: true`, **When** the request is processed, **Then** only error-level diagnostics are returned.

---

### User Story 3 - Symbol Navigation via Rider (Priority: P2)

As an LLM agent exploring unfamiliar code, I want to get detailed symbol information (documentation, signature, type hierarchy) from Rider's backend, so I get richer context than Roslyn alone provides.

**Why this priority**: Symbol info from Rider includes rendered documentation and contextual information that aids understanding. Useful but not critical for basic code analysis.

**Independent Test**: Can be tested by requesting symbol info at a known location and verifying Rider's response includes documentation and declaration.

**Acceptance Scenarios**:

1. **Given** a loaded project, **When** I call `code_goto_definition` with file, line, and column, **Then** I receive the symbol's definition location from Rider's backend.

2. **Given** a symbol defined in a NuGet package, **When** I request its definition, **Then** Rider returns decompiled source or assembly metadata.

---

### User Story 4 - Disable Rider Backend via CLI Flag (Priority: P2)

As a user running DebugMcp in a CI/CD environment without Rider, I want a CLI flag to disable Rider backend detection, so startup is fast and there are no connection timeout delays.

**Why this priority**: Essential for headless/CI environments where Rider is never available, and for users who prefer pure Roslyn analysis.

**Independent Test**: Can be tested by starting DebugMcp with `--no-rider` flag and verifying no JetBrains MCP connection attempt is made.

**Acceptance Scenarios**:

1. **Given** DebugMcp started with `--no-rider`, **When** it initializes, **Then** it uses Roslyn directly without any JetBrains MCP connection attempt.

2. **Given** DebugMcp started without `--no-rider` but JetBrains MCP is unavailable, **When** it initializes, **Then** it falls back to Roslyn after a brief timeout (max 2 seconds).

---

### User Story 5 - Project Loading with Rider Backend (Priority: P1)

As an LLM agent, I want the `code_load` tool to work with both backends, loading the workspace in Rider (which already has it open) or via Roslyn MSBuildWorkspace.

**Why this priority**: Loading is a prerequisite for all other analysis operations.

**Independent Test**: Can be tested by calling `code_load` with a valid .sln path and verifying the workspace summary is returned from the appropriate backend.

**Acceptance Scenarios**:

1. **Given** Rider backend is active and the solution is already open in Rider, **When** `code_load` is called, **Then** it confirms the workspace from Rider without re-loading.

2. **Given** Roslyn fallback is active, **When** `code_load` is called, **Then** it behaves exactly as the existing feature 015 implementation.

3. **Given** Rider backend is active, **When** `code_load` is called with a path different from the open solution, **Then** a clear error or fallback to Roslyn occurs.

---

### Edge Cases

- **Rider MCP server goes down mid-session**: DebugMcp should detect the failure and report an error, suggesting restart. No automatic fallback to Roslyn mid-session (would cause inconsistent results).
- **Multiple Rider instances**: DebugMcp connects to the first available JetBrains MCP endpoint. If multiple exist, behavior is undefined (documented limitation).
- **Version mismatch**: If the JetBrains MCP protocol version is incompatible, fall back to Roslyn with a warning.
- **Large files**: Rider's `get_file_problems` may be slow for very large files. Respect the same timeout patterns as the Roslyn backend.
- **Project not open in Rider**: If the user tries to analyze a project not open in Rider, return a clear error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support two code analysis backends: JetBrains Rider (via JetBrains MCP server) and Roslyn (existing feature 015 implementation)
- **FR-002**: System MUST auto-detect JetBrains MCP server availability at startup
- **FR-003**: System MUST provide a `--no-rider` CLI flag to disable JetBrains MCP detection and use Roslyn exclusively
- **FR-004**: System MUST maintain the same MCP tool names (`code_load`, `code_goto_definition`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics`) regardless of active backend
- **FR-005**: System MUST include a `provider` field in tool responses indicating which backend served the request ("rider" or "roslyn")
- **FR-006**: When Rider backend is active, `code_get_diagnostics` MUST delegate to JetBrains MCP `get_file_problems` for ReSharper-level inspections
- **FR-007**: When Rider backend is active, `code_goto_definition` MUST delegate to JetBrains MCP `get_symbol_info`
- **FR-008**: When Rider backend is unavailable and `--no-rider` is not set, system MUST fall back to Roslyn after a connection timeout (max 2 seconds)
- **FR-009**: System MUST log which backend was selected at startup
- **FR-010**: System MUST return structured errors when the Rider backend fails mid-operation

### Key Entities

- **CodeAnalysisProvider**: Abstraction representing a backend (Rider or Roslyn). Selected at startup.
- **RiderMcpClient**: Client that communicates with the JetBrains MCP server to execute code analysis operations.
- **ProviderSelection**: Configuration result containing the selected provider and reason (auto-detected, forced by flag, fallback).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 5 existing code analysis tools continue to work identically when Rider is unavailable (100% backward compatibility)
- **SC-002**: When Rider is available, `code_get_diagnostics` returns ReSharper inspections (more results than Roslyn compiler-only diagnostics)
- **SC-003**: Backend selection at startup completes within 2 seconds when Rider is unavailable
- **SC-004**: `--no-rider` flag prevents any JetBrains MCP connection attempt (verifiable via logs)
- **SC-005**: Tool responses include `provider` field for transparency

## Assumptions

- JetBrains MCP server is reachable via the same transport mechanism used by the IDE plugin
- Rider has the target solution already open when used as backend
- The JetBrains MCP protocol remains stable across Rider 2025.3.x versions
- ReSharper inspections are available through `get_file_problems` and `build_project` tools

## Dependencies

- Feature 015 (Roslyn Code Analysis) — existing implementation used as fallback
- JetBrains MCP server (provided by Rider IDE plugin)
- Existing MCP server infrastructure

## Out of Scope

- Automatic starting/stopping of Rider IDE
- Support for ReSharper standalone (without Rider)
- Exposing Rider-specific tools that have no Roslyn equivalent (e.g., `rename_refactoring`)
- Live file watching or incremental analysis updates
- Support for non-C# languages through Rider
