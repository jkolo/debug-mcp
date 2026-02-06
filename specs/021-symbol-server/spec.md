# Feature Specification: Symbol Server Integration

**Feature Branch**: `021-symbol-server`
**Created**: 2026-02-06
**Status**: Draft
**Input**: User description: "Symbol Server"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automatic PDB Download for Third-Party Libraries (Priority: P1)

When an LLM agent debugs a .NET application that uses third-party NuGet packages (e.g., Newtonsoft.Json, Entity Framework, ASP.NET Core), the debugger automatically downloads PDB symbol files from public symbol servers so that stack traces, source mapping, variable names, and breakpoint placement work for library code — not just the user's own code.

**Why this priority**: This is the core value proposition. Without symbols, the debugger is blind when execution enters library code — stack frames show raw addresses, variables have no names, and source stepping is impossible. This affects the majority of real-world debugging sessions.

**Independent Test**: Can be tested by attaching to any .NET app that references a NuGet package, inspecting a stack trace that includes library frames, and verifying that function names, source file paths, and line numbers are resolved.

**Acceptance Scenarios**:

1. **Given** a debugger attached to a process using a NuGet library, **When** the agent requests a stack trace that includes library frames, **Then** the stack trace shows resolved function names and source locations instead of raw IL offsets.
2. **Given** a debugger attached to a process with no locally available PDBs for a loaded assembly, **When** the assembly is loaded, **Then** the system attempts to download the PDB from configured symbol servers.
3. **Given** a PDB has been previously downloaded for an assembly, **When** the same assembly is encountered in a subsequent session, **Then** the cached PDB is used without re-downloading.

---

### User Story 2 - Symbol Server Configuration (Priority: P2)

The user can configure which symbol servers to use, including Microsoft's public symbol server, NuGet.org symbol server, and custom/private symbol servers. Configuration can include enabling/disabling specific servers and setting a persistent cache directory.

**Why this priority**: Users in corporate environments may need private symbol servers or may want to disable public servers. A sensible default (Microsoft + NuGet public servers) covers most cases out of the box.

**Independent Test**: Can be tested by starting the debugger with custom symbol server settings and verifying that PDB downloads use the configured servers.

**Acceptance Scenarios**:

1. **Given** no explicit configuration, **When** the debugger starts, **Then** it uses Microsoft and NuGet public symbol servers by default.
2. **Given** a custom symbol server URL is configured, **When** a PDB is needed, **Then** the system queries the custom server in addition to (or instead of) defaults.
3. **Given** symbol servers are disabled, **When** a PDB is not found locally, **Then** no network requests are made and the debugger gracefully degrades.

---

### User Story 3 - Symbol Download Status Visibility (Priority: P3)

The LLM agent can see which modules have symbols loaded, which are pending download, and which failed — enabling it to make informed decisions about what can be inspected in detail versus what is opaque.

**Why this priority**: Transparency helps the agent decide whether to attempt source-level debugging of a library or fall back to other inspection methods. Without visibility, the agent wastes time on dead-end debugging paths.

**Independent Test**: Can be tested by loading a process with a mix of symbolized and unsymbolized modules, then querying symbol status.

**Acceptance Scenarios**:

1. **Given** a debugger attached to a process with multiple loaded modules, **When** the agent queries module information, **Then** each module shows its symbol status (loaded, not found, downloading, failed).
2. **Given** a PDB download fails (network error, not found on server), **When** the agent queries the module, **Then** the failure reason is visible.

---

### User Story 4 - Embedded PDB Support (Priority: P3)

The debugger can read symbols from assemblies that embed their PDB data directly inside the DLL, which is a common publishing pattern for modern .NET libraries.

**Why this priority**: Many NuGet packages ship with embedded PDBs. Without this, the debugger misses symbols that are already available inside the assembly file itself.

**Independent Test**: Can be tested by loading an assembly known to have embedded PDB data and verifying that symbols are resolved without any external PDB file.

**Acceptance Scenarios**:

1. **Given** an assembly with embedded PDB data, **When** the debugger loads the module, **Then** symbols are extracted and used for source mapping and variable names.
2. **Given** an assembly with both embedded PDB and an external PDB file, **When** the debugger loads the module, **Then** the external PDB takes precedence.

---

### Edge Cases

- What happens when the symbol server is unreachable (network timeout)?
  - System gracefully degrades: logs a warning, continues without symbols for that module, and does not block the debugging session.
- What happens when a PDB download is corrupted or checksum mismatched?
  - The corrupted file is discarded, a warning is logged, and the module is marked as "symbols not available."
- What happens when disk space for the symbol cache is exhausted?
  - The system logs an error and continues without caching; existing cached symbols remain usable.
- What happens with very large PDB files (hundreds of MB)?
  - Downloads have a configurable size limit (default: 100 MB). Files exceeding the limit are skipped with a warning.
- What happens when multiple modules need PDBs simultaneously (e.g., on process attach)?
  - Downloads are parallelized with a concurrency limit to avoid overwhelming the network or symbol server.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST attempt to locate PDB symbols for every loaded module, checking in order: (1) local path next to assembly, (2) embedded PDB in assembly, (3) persistent symbol cache, (4) configured symbol servers.
- **FR-002**: System MUST support the Simple Symbol Query Protocol (SSQP) used by Microsoft and NuGet symbol servers for downloading PDB files using assembly debug directory information.
- **FR-003**: System MUST support NuGet Symbol Server (symbols.nuget.org) as a default symbol source.
- **FR-004**: System MUST persistently cache downloaded PDBs in a local directory, organized by signature, so they survive across debugging sessions.
- **FR-005**: System MUST support embedded PDB extraction from assemblies that contain Portable PDB data in the PE debug directory.
- **FR-006**: System MUST expose symbol status per module (loaded, not found, downloading, failed) through the existing modules inspection tools.
- **FR-007**: System MUST allow symbol server configuration via command-line arguments or environment variables: symbol server URLs, cache directory path, enable/disable flag.
- **FR-008**: System MUST validate downloaded PDBs by matching the PDB ID (GUID) against the assembly's CodeView debug directory entry before accepting them.
- **FR-009**: System MUST not block the debugging session while downloading symbols — downloads happen asynchronously in the background.
- **FR-010**: System MUST log symbol resolution activity via MCP logging at appropriate levels (Debug for cache hits, Info for downloads, Warning for failures).
- **FR-011**: System MUST support a configurable download timeout per PDB file (default: 30 seconds).
- **FR-012**: System MUST limit concurrent symbol downloads to avoid overwhelming the network (default: 4 parallel downloads).

### Key Entities

- **Symbol Source**: A configured location for PDB files — can be a local path, a symbol server URL, or the assembly itself (embedded PDB). Has a priority order and enable/disable flag.
- **Symbol Cache**: A persistent local directory storing previously downloaded PDBs, organized by assembly signature for quick lookup.
- **Module Symbol Status**: Per-module metadata tracking whether symbols are loaded, pending download, not available, or failed with a reason.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Stack traces for .NET runtime library frames show resolved function names and parameter types within 10 seconds of module load.
- **SC-002**: Previously downloaded PDBs are reused from cache without network access, making symbol resolution instant for repeat sessions.
- **SC-003**: The debugger starts and attaches to a process within the same time as before when symbol servers are unreachable (no blocking on network failures).
- **SC-004**: At least 80% of commonly used NuGet packages (top 50 by download count) have their PDBs successfully resolved from public symbol servers.
- **SC-005**: Symbol download and cache status is visible for every loaded module, enabling informed debugging decisions.

## Assumptions

- The debugger operates on linux-x64 and primarily deals with Portable PDB format (not Windows PDB / .pdb Classic).
- Microsoft's public symbol server (msdl.microsoft.com) and NuGet symbol server (symbols.nuget.org) are the primary sources.
- The default symbol cache location is `~/.debug-mcp/symbols/` unless overridden.
- Embedded PDBs in modern .NET assemblies use the Portable PDB format stored in the PE debug directory.
- Symbol server authentication (for private servers) is handled via standard HTTP headers or environment variables, not interactive login.

## Dependencies

- Existing `PdbSymbolCache` and `IPdbSymbolReader` services provide the foundation for PDB reading.
- `PdbSymbolCache.FindPdbPath()` currently only checks the local directory — this will be extended with the new symbol resolution chain.
- The `modules_list` tool already exposes module information — symbol status will be added to its output.
