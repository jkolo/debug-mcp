# Feature Specification: .NET Tool Packaging

**Feature Branch**: `010-dotnet-tool-packaging`
**Created**: 2026-01-28
**Status**: Draft
**Input**: User description: "chcemy by aplikacja była łatwo startowalna. Albo przez dotnet dnx, albo przez dotnet tool."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Install DotnetMcp as Global Tool (Priority: P1)

A developer wants to install the .NET MCP debugger as a global tool so they can invoke it from any directory without building from source. They run a single install command and immediately have access to the debugger via a simple CLI command.

**Why this priority**: This is the primary distribution mechanism that provides the simplest installation experience for end users. Global tools are the standard way to distribute .NET CLI applications.

**Independent Test**: Can be fully tested by publishing a NuGet package, installing it via `dotnet tool install`, and verifying the tool runs correctly from any directory.

**Acceptance Scenarios**:

1. **Given** the tool package is published to NuGet (or a local feed), **When** user runs `dotnet tool install -g dotnet-mcp`, **Then** the tool is installed and available as `dotnet-mcp` command globally
2. **Given** the tool is installed globally, **When** user runs `dotnet-mcp --version` from any directory, **Then** the version information is displayed
3. **Given** the tool is installed globally, **When** user runs `dotnet-mcp` without arguments, **Then** the tool starts as an MCP server (stdio transport)

---

### User Story 2 - Install DotnetMcp as Local Tool (Priority: P2)

A developer wants to install the debugger as a project-local tool to ensure version consistency across their team. They add it to a tool manifest and restore it like other project dependencies.

**Why this priority**: Local tools provide better version control for teams and CI/CD pipelines but require slightly more setup than global installation.

**Independent Test**: Can be tested by creating a tool manifest, adding the tool, and running it via `dotnet tool run`.

**Acceptance Scenarios**:

1. **Given** a project with `.config/dotnet-tools.json` manifest, **When** user adds `dotnet-mcp` to the manifest and runs `dotnet tool restore`, **Then** the tool is installed locally
2. **Given** the tool is installed locally, **When** user runs `dotnet tool run dotnet-mcp --version` in the project directory, **Then** the version information is displayed
3. **Given** the tool is installed locally, **When** user runs `dotnet dotnet-mcp` in the project directory, **Then** the tool starts correctly

---

### User Story 3 - Run Without Installation via dotnet run (Priority: P3)

A developer wants to quickly try the debugger without installing it. They can run it directly from source using `dotnet run` for evaluation or development purposes.

**Why this priority**: This is already partially supported but should be documented and ensure a smooth experience for contributors and evaluators.

**Independent Test**: Can be tested by cloning the repository and running `dotnet run --project DotnetMcp/DotnetMcp.csproj`.

**Acceptance Scenarios**:

1. **Given** a clone of the repository, **When** user runs `dotnet run --project DotnetMcp/DotnetMcp.csproj`, **Then** the MCP server starts successfully
2. **Given** a clone of the repository, **When** user runs `dotnet run --project DotnetMcp/DotnetMcp.csproj -- --help`, **Then** usage information is displayed

---

### Edge Cases

- What happens when user tries to install on unsupported platform (Windows, macOS)?
  - Tool should fail gracefully with a clear message that only Linux x64 is currently supported
- What happens when required .NET runtime is not installed?
  - Standard .NET tool behavior applies - user receives error about missing runtime
- What happens when tool is invoked with invalid arguments?
  - Tool should display help message with valid options
- What happens when NuGet package is not found during install?
  - Standard NuGet error is displayed; documentation should clarify package source

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Project MUST be configured as a .NET tool with appropriate PackAsTool settings
- **FR-002**: Tool MUST have a unique, recognizable command name (`dotnet-mcp`)
- **FR-003**: Tool MUST display version information when invoked with `--version` flag
- **FR-004**: Tool MUST display help/usage information when invoked with `--help` flag
- **FR-005**: Tool MUST specify supported runtime identifiers (RID) in package metadata
- **FR-006**: Tool MUST include clear documentation for installation and usage
- **FR-007**: Tool SHOULD fail gracefully on unsupported platforms with a descriptive error message (note: the .NET runtime and NuGet restore handle this natively via RID-specific packaging — no custom code required)
- **FR-008**: Package MUST include appropriate metadata (authors, license, repository URL, description)

### Key Entities

- **NuGet Package**: The distributable package containing the tool, published to NuGet.org or a private feed
- **Tool Manifest**: `.config/dotnet-tools.json` file that tracks local tool versions for a project
- **Runtime Identifier (RID)**: Platform specification (linux-x64) that determines where the tool can run

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can install the tool in under 30 seconds using a single command
- **SC-002**: Tool is discoverable on NuGet.org by searching "mcp debugger" or "dotnet debugger" *(deferred: requires NuGet.org publish, out of scope for this branch)*
- **SC-003**: 100% of installation attempts on supported platforms (Linux x64) succeed without manual intervention
- **SC-004**: Tool startup time from installation to first use is under 5 seconds *(validated: ~450ms)*
- **SC-005**: README provides copy-paste installation command that works on first try

## Assumptions

- The tool will initially only support Linux x64 (current platform constraint due to DbgShim dependency)
- Package will be published to NuGet.org for public availability
- Tool name `dotnet-mcp` is available on NuGet.org
- Users have .NET 10 runtime installed (framework-dependent deployment)
- Package uses framework-dependent deployment model (smaller package, requires runtime)

## Clarifications

### Session 2026-01-28

- Q: Should the tool be framework-dependent or self-contained? → A: Framework-dependent (requires .NET 10 runtime, smaller package)
- Q: What initial version number for the NuGet package? → A: 1.0.0 (signals production-ready, stable API)
