# Feature Specification: Cross-Platform Support (Windows & macOS)

**Feature Branch**: `025-cross-platform`
**Created**: 2026-02-11
**Status**: Draft
**Input**: User description: "wsparcie dla windows i osx"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Install and Run on Windows (Priority: P1)

A developer on Windows installs debug-mcp as a .NET global tool and uses it to debug a .NET application. The tool launches the target process, sets breakpoints, pauses execution, inspects variables, and evaluates expressions — the same 34 tools that work on Linux today must work identically on Windows.

**Why this priority**: Windows is the most widely used .NET development platform. Without Windows support, the majority of .NET developers cannot use the tool.

**Independent Test**: Install `debug-mcp` on a Windows x64 machine via `dotnet tool install -g debug-mcp`, launch a test .NET application, set a breakpoint, hit it, inspect locals, and disconnect — all via MCP client.

**Acceptance Scenarios**:

1. **Given** a Windows x64 machine with .NET 10 installed, **When** the user runs `dotnet tool install -g debug-mcp`, **Then** the tool installs successfully and `debug-mcp --version` prints the version.
2. **Given** debug-mcp is running on Windows, **When** the user launches a .NET console app via `debug_launch`, **Then** the process starts and the session enters "running" state.
3. **Given** a running debug session on Windows, **When** the user sets a breakpoint and the execution reaches it, **Then** the session pauses and the breakpoint hit is reported with correct source location.
4. **Given** a paused debug session on Windows, **When** the user inspects variables and evaluates expressions, **Then** results match those produced on Linux for the same target application.

---

### User Story 2 - Install and Run on macOS (Priority: P1)

A developer on macOS (Intel or Apple Silicon) installs debug-mcp and uses it to debug a .NET application. All 34 tools work identically to Linux.

**Why this priority**: macOS is the second most common development platform for .NET (especially in cross-platform teams). Intel and Apple Silicon must both be supported.

**Independent Test**: Install `debug-mcp` on macOS (x64 or arm64) via `dotnet tool install -g debug-mcp`, launch a test .NET application, set a breakpoint, hit it, inspect locals, and disconnect.

**Acceptance Scenarios**:

1. **Given** a macOS machine (Intel or Apple Silicon) with .NET 10 installed, **When** the user runs `dotnet tool install -g debug-mcp`, **Then** the tool installs successfully.
2. **Given** debug-mcp is running on macOS, **When** the user launches a .NET console app via `debug_launch`, **Then** the process starts and the session enters "running" state.
3. **Given** a running debug session on macOS, **When** the user sets a breakpoint and the execution reaches it, **Then** the session pauses with correct source location.

---

### User Story 3 - ARM64 Architecture Support (Priority: P2)

Users on ARM64 machines (Apple Silicon Macs, Windows ARM64 laptops, Linux ARM64 servers) can install and use the tool natively without Rosetta or x86 emulation.

**Why this priority**: Apple Silicon is now the dominant Mac platform. ARM64 Windows devices are growing. Native ARM64 avoids emulation overhead and compatibility issues.

**Independent Test**: Install `debug-mcp` on an Apple Silicon Mac (arm64) without Rosetta, launch a debug session, and verify all core debugging operations work.

**Acceptance Scenarios**:

1. **Given** an Apple Silicon Mac running natively (no Rosetta), **When** the user installs debug-mcp, **Then** the arm64-native binary is installed.
2. **Given** a Windows ARM64 laptop with .NET 10, **When** the user installs debug-mcp, **Then** the arm64-native binary is installed and debugging works.
3. **Given** a Linux ARM64 server, **When** the user installs debug-mcp, **Then** the arm64-native binary is installed and debugging works.

---

### User Story 4 - CI/CD Builds and Tests All Platforms (Priority: P2)

The project's CI pipeline automatically builds and tests on all supported platforms, catching platform-specific regressions before merge.

**Why this priority**: Without CI coverage, platform-specific bugs will only be discovered by users. Automated cross-platform testing prevents regressions.

**Independent Test**: Push a commit and verify that CI runs builds and unit/contract tests on Linux, Windows, and macOS.

**Acceptance Scenarios**:

1. **Given** a push to any branch, **When** CI runs, **Then** the build succeeds on all supported platforms (Linux x64, Windows x64, macOS x64, macOS arm64).
2. **Given** a push to any branch, **When** CI runs, **Then** unit and contract tests pass on all supported platforms.
3. **Given** a CI matrix failure on one platform, **When** the results are reported, **Then** the failing platform is clearly identified.

---

### User Story 5 - Multi-Platform NuGet Package (Priority: P3)

The tool is published as a single NuGet package that automatically resolves the correct native binary for the user's platform, rather than requiring users to choose a platform-specific package.

**Why this priority**: A single package simplifies the install experience. Users should not need to know their RID.

**Independent Test**: Publish the package, install it on each supported platform, and verify the correct native DbgShim binary is selected automatically.

**Acceptance Scenarios**:

1. **Given** a published debug-mcp NuGet package, **When** a user on any supported platform runs `dotnet tool install -g debug-mcp`, **Then** the correct platform-native binary is installed without additional configuration.

---

### Edge Cases

- What happens when a user installs on an unsupported platform (e.g., Linux ARM32)? The tool should fail with a clear error message naming the unsupported platform.
- What happens when the DbgShim native library is missing for the current platform? The tool should report which library file it expected and where it searched.
- What happens when running under Rosetta 2 on Apple Silicon? The tool should work (x64 DbgShim under emulation) but may log a warning suggesting native ARM64 installation.
- What happens when the target .NET application runs under a different architecture than debug-mcp (e.g., x64 app on ARM64 host)? The tool should report an architecture mismatch error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The tool MUST build and run on Windows x64
- **FR-002**: The tool MUST build and run on macOS x64 (Intel)
- **FR-003**: The tool MUST build and run on macOS arm64 (Apple Silicon)
- **FR-004**: The tool MUST build and run on Windows arm64
- **FR-005**: The tool MUST build and run on Linux arm64
- **FR-006**: All 34 existing MCP tools MUST produce identical results across all supported platforms for the same target application
- **FR-007**: The DbgShim native library MUST be automatically discovered on each platform without user configuration
- **FR-008**: The NuGet package MUST include native binaries for all supported platform/architecture combinations
- **FR-009**: Process child reaping (zombie prevention) MUST work on macOS in addition to Linux
- **FR-010**: The CI pipeline MUST build on all supported platforms
- **FR-011**: The CI pipeline MUST run unit and contract tests on all supported platforms
- **FR-012**: When running on an unsupported platform, the tool MUST exit with a clear error message identifying the platform and listing supported ones
- **FR-013**: The tool MUST detect architecture mismatches between itself and the target process and report a clear error

### Assumptions

- .NET 10 SDK is available on all target platforms (Microsoft provides .NET 10 for all listed RIDs)
- Microsoft.Diagnostics.DbgShim NuGet packages exist for all target RIDs (verified: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64 packages are published)
- ICorDebug APIs behave consistently across platforms (same COM interfaces, same callback semantics) — Microsoft's debugging infrastructure abstracts platform differences
- The existing ClrDebug NuGet wrapper (0.3.4) works on Windows and macOS without modification (it wraps COM interfaces that .NET exposes on all platforms)
- GitHub Actions provides runners for ubuntu-latest, windows-latest, macos-latest (x64), and macos-14 (arm64)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 34 tools pass their existing unit and contract tests on every supported platform (Linux x64, Windows x64, macOS x64, macOS arm64, Windows arm64, Linux arm64)
- **SC-002**: A user on any supported platform can install the tool with a single `dotnet tool install -g debug-mcp` command and start debugging within 2 minutes
- **SC-003**: CI build-and-test completes on all platforms within 10 minutes (no more than 2x the current Linux-only CI time)
- **SC-004**: The published NuGet package size remains under 50 MB (all native binaries included)
- **SC-005**: Zero platform-specific behavioral differences in tool output for the same debugging scenario (identical JSON responses across platforms)
