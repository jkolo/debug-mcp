# Feature Specification: Roslyn Code Analysis

**Feature Branch**: `015-roslyn-code-analysis`
**Created**: 2026-02-04
**Status**: Draft
**Input**: User description: "dodajmy analizę kodu przy pomocy Roslyn. Niech nasz MCP umie nawigować po kodzie. Tak żeby LLM mógł go poprosić o miejsce wszystkich użyć gettera dla danego property. Lub znawigować gdzie dana zmienna jest ustawiana. Lub dostać listę błędów i warningów z analizy."

## Summary

Add Roslyn-based static code analysis capabilities to the MCP debugger server. This enables LLM agents to navigate and understand C# codebases without running the debugger - finding symbol usages, tracking variable assignments, and retrieving compilation diagnostics. This complements the existing runtime debugging by providing static analysis before or instead of live debugging.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find All Usages of a Symbol (Priority: P1)

As an LLM agent debugging a .NET application, I want to find all locations where a specific property getter is used, so I can understand the data flow and identify potential issues without stepping through code.

**Why this priority**: Finding symbol usages is the most fundamental code navigation operation. It enables understanding how code is connected and is essential for debugging, refactoring analysis, and impact assessment.

**Independent Test**: Can be fully tested by loading a solution/project, requesting usages of a known symbol, and verifying all usage locations are returned with file paths and line numbers.

**Acceptance Scenarios**:

1. **Given** a loaded C# project with a class containing a `Customer.Name` property, **When** I request all usages of the `Name` getter, **Then** I receive a list of all locations (file, line, column) where `customer.Name` is read.

2. **Given** a loaded solution with multiple projects, **When** I request usages of a public symbol defined in one project, **Then** I receive usages from all projects that reference it.

3. **Given** a symbol that has no usages, **When** I request its usages, **Then** I receive an empty list with a clear indication that no usages were found.

4. **Given** an invalid or non-existent symbol reference, **When** I request its usages, **Then** I receive a clear error message indicating the symbol could not be resolved.

---

### User Story 2 - Find Where Variable Is Assigned (Priority: P1)

As an LLM agent investigating a bug, I want to find all locations where a specific variable or field is assigned a value, so I can trace where unexpected values might originate.

**Why this priority**: Tracking assignments is critical for debugging state-related bugs. Combined with usage finding, it provides complete data flow analysis.

**Independent Test**: Can be tested by loading a project, requesting assignment locations for a known variable, and verifying all write operations are returned.

**Acceptance Scenarios**:

1. **Given** a loaded C# project with a field `_count`, **When** I request all assignments to `_count`, **Then** I receive locations of all `_count = ...` statements, `_count++`, `_count--`, and compound assignments.

2. **Given** a property with a setter, **When** I request assignments to that property, **Then** I receive locations where the property setter is invoked.

3. **Given** an `out` or `ref` parameter, **When** I request its assignments, **Then** I receive the method call locations where the parameter receives a value.

---

### User Story 3 - Get Compilation Diagnostics (Priority: P2)

As an LLM agent reviewing code quality, I want to retrieve all compilation errors and warnings for a project, so I can identify issues without running a build command externally.

**Why this priority**: Diagnostics provide immediate feedback on code correctness and quality. While essential, it's secondary to navigation as diagnostics can also be obtained from build output.

**Independent Test**: Can be tested by loading a project with known errors/warnings and verifying all expected diagnostics are returned with severity, location, and message.

**Acceptance Scenarios**:

1. **Given** a loaded C# project with syntax errors, **When** I request diagnostics, **Then** I receive all error diagnostics with file, line, column, error code, and message.

2. **Given** a project with no errors but several warnings, **When** I request diagnostics, **Then** I receive all warning diagnostics with appropriate severity levels.

3. **Given** a project that compiles cleanly, **When** I request diagnostics, **Then** I receive an empty list or a success indicator.

4. **Given** a multi-project solution, **When** I request diagnostics for a specific project, **Then** I receive only diagnostics for that project.

---

### User Story 4 - Load and Analyze Solution/Project (Priority: P1)

As an LLM agent starting code analysis, I want to load a solution or project file so that Roslyn can parse and understand the codebase for subsequent queries.

**Why this priority**: Loading is a prerequisite for all other operations. Without a loaded workspace, no analysis is possible.

**Independent Test**: Can be tested by providing a valid .sln or .csproj path and verifying the workspace loads successfully with project/document information available.

**Acceptance Scenarios**:

1. **Given** a valid .sln file path, **When** I request to load the solution, **Then** the workspace loads and I receive a summary of loaded projects and documents.

2. **Given** a valid .csproj file path, **When** I request to load the project, **Then** the single project loads and I can perform analysis on it.

3. **Given** an invalid or non-existent path, **When** I request to load it, **Then** I receive a clear error message.

4. **Given** a solution that was previously loaded, **When** I request to reload or load a different solution, **Then** the previous workspace is replaced with the new one.

---

### User Story 5 - Navigate to Symbol Definition (Priority: P2)

As an LLM agent exploring unfamiliar code, I want to find where a symbol is defined, so I can understand its implementation.

**Why this priority**: Go-to-definition is a common navigation need, but slightly less critical than find-usages for debugging purposes.

**Independent Test**: Can be tested by loading a project, requesting the definition of a used symbol, and verifying the definition location is returned.

**Acceptance Scenarios**:

1. **Given** a loaded project with a method call `foo.Bar()`, **When** I request the definition of `Bar`, **Then** I receive the file and line where `Bar` is defined.

2. **Given** a symbol defined in referenced NuGet package without sources, **When** I request its definition, **Then** I receive metadata-based location or an indication that source is unavailable.

3. **Given** a symbol with multiple definitions (partial classes), **When** I request its definition, **Then** I receive all definition locations.

---

### Edge Cases

- **Unresolved NuGet packages**: System continues loading with warnings, analyzes available code, and reports missing references in the load result.
- **Large solutions (>100 projects)**: System reports progress via MCP notifications during loading; no artificial limits imposed.
- How does the system handle projects targeting unsupported .NET versions?
- What happens when source files have encoding issues?
- How are generated files (e.g., from source generators) handled?
- What happens when the workspace is not loaded and analysis is requested?

## Clarifications

### Session 2026-02-04

- Q: How should users identify symbols for analysis? → A: Both fully qualified name and file location (file + line + column) supported
- Q: How to handle large solutions (>100 projects)? → A: Report progress via MCP progress notifications during loading
- Q: How to handle unresolved NuGet packages? → A: Continue with warnings - analyze available code, report missing references

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a tool to load a .sln or .csproj file into an analysis workspace
- **FR-002**: System MUST provide a tool to find all usages (references) of a symbol identified by either fully qualified name OR file location (path + line + column)
- **FR-003**: System MUST provide a tool to find all assignment locations for a variable, field, or property
- **FR-004**: System MUST provide a tool to retrieve compilation diagnostics (errors, warnings, info) for a loaded project
- **FR-005**: System MUST provide a tool to navigate to a symbol's definition location
- **FR-006**: System MUST return results with file paths, line numbers, and column numbers
- **FR-007**: System MUST handle cross-project references within a solution
- **FR-008**: System MUST provide clear error messages when analysis fails
- **FR-009**: System MUST support loading a new workspace to replace the current one
- **FR-010**: System MUST work independently of the debugger session (no process attach required)
- **FR-011**: System MUST report progress via MCP progress notifications during long-running operations (solution loading)

### Key Entities

- **Workspace**: Represents a loaded solution or project ready for analysis. Contains projects and documents.
- **Symbol**: A named code element (class, method, property, variable, etc.) that can be referenced and analyzed.
- **SymbolLocation**: A specific position in source code (file path, line, column, span).
- **Diagnostic**: A compiler message with severity (error, warning, info), code, message, and location.
- **Usage**: A reference to a symbol from another location in the codebase.
- **Assignment**: A write operation to a variable, field, or property.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Symbol usage search returns results within 2 seconds for typical projects (<50 files)
- **SC-002**: Solution loading completes within 30 seconds for solutions with up to 50 projects
- **SC-003**: All compiler diagnostics are captured with 100% accuracy compared to `dotnet build` output
- **SC-004**: Cross-project symbol resolution works correctly for 100% of public/internal symbols
- **SC-005**: LLM agents can complete code navigation tasks without requiring external IDE tools

## Assumptions

- Roslyn (Microsoft.CodeAnalysis) NuGet packages are available and compatible with .NET 10.0
- Solutions/projects to be analyzed are valid and can be opened by MSBuildWorkspace
- Source files are available on the local filesystem
- The MCP server runs on the same machine where source code is located
- Analysis operates on source code at rest (no live editing/file watching required)

## Dependencies

- Existing MCP server infrastructure from previous features
- Microsoft.CodeAnalysis.Workspaces and related Roslyn packages
- MSBuild tooling for solution/project loading

## Out of Scope

- Live code editing or refactoring operations
- Source code formatting or modification
- Integration with version control systems
- Code completion or IntelliSense-style suggestions
- Analysis of non-C# languages (VB.NET, F#)
- Remote solution loading (only local filesystem supported)
