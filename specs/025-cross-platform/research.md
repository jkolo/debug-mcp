# Research: Cross-Platform Support

## R1: Multi-RID .NET Tool Packaging Strategy

**Decision**: RID-agnostic tool with all native libraries bundled

**Rationale**: .NET tools with `PackAsTool=true` can be either RID-specific (single `RuntimeIdentifier`) or RID-agnostic (no `RuntimeIdentifier`). When RID-agnostic, the tool runs under the user's installed .NET runtime, and NuGet's `runtimes/{rid}/native/` convention handles native library discovery. This is the recommended approach for tools that need to work on multiple platforms.

**Alternatives considered**:
- Per-platform NuGet packages (`debug-mcp-win-x64`, etc.) — rejected: fragments install experience, requires platform-aware documentation
- Multiple `RuntimeIdentifiers` property — rejected: .NET tools don't support multi-RID packing with `PackAsTool=true`
- Self-contained tool per platform — rejected: bloats package size (~60 MB per platform × 6 = 360 MB)

## R2: DbgShim Package Availability

**Decision**: All 6 target RIDs have published DbgShim packages

**Verification**: Microsoft.Diagnostics.DbgShim packages exist on NuGet.org for:
- `Microsoft.Diagnostics.DbgShim.linux-x64` (9.0.661903)
- `Microsoft.Diagnostics.DbgShim.linux-arm64` (9.0.661903)
- `Microsoft.Diagnostics.DbgShim.win-x64` (9.0.661903)
- `Microsoft.Diagnostics.DbgShim.win-arm64` (9.0.661903)
- `Microsoft.Diagnostics.DbgShim.osx-x64` (9.0.661903)
- `Microsoft.Diagnostics.DbgShim.osx-arm64` (9.0.661903)

Each package contains a single native library under `runtimes/{rid}/native/`:
- Windows: `dbgshim.dll` (~4 MB)
- Linux: `libdbgshim.so` (~5 MB)
- macOS: `libdbgshim.dylib` (~4 MB)

Total estimated package size with all 6: ~25-30 MB (within 50 MB budget).

## R3: Architecture Detection at Runtime

**Decision**: Use `RuntimeInformation.ProcessArchitecture` to determine x64 vs arm64

**Rationale**: `RuntimeInformation.ProcessArchitecture` returns the architecture of the current process, which is what matters for native library loading. Under Rosetta 2 on Apple Silicon, it correctly returns `Architecture.X64` (since the process runs as x64). Under native ARM64, it returns `Architecture.Arm64`.

**Alternative considered**: `RuntimeInformation.OSArchitecture` — rejected: returns the OS architecture (always arm64 on Apple Silicon), not the process architecture. Would select wrong native lib under Rosetta.

## R4: macOS Debugging Compatibility (waitpid / CreateProcessForLaunch)

**Decision**: `waitpid` P/Invoke works identically on macOS via libc

**Rationale**: macOS provides POSIX-compliant `waitpid` in `/usr/lib/libSystem.B.dylib` (aliased as `libc`). The `WNOHANG` constant is 1 on both Linux and macOS. DbgShim's `CreateProcessForLaunch` uses the same `fork()+ptrace` pattern on macOS as on Linux, so the same zombie-reaping logic applies.

**Risk**: On macOS, `ptrace` behavior differs slightly (macOS uses Mach-based debugging under the hood). However, DbgShim abstracts this — ICorDebug callbacks should fire identically. If macOS ptrace restrictions cause issues (SIP, DevToolsSecurity), these will surface during integration testing and require documented workarounds (e.g., `DevToolsSecurity -enable`).

## R5: CI Matrix Design

**Decision**: 3-OS matrix for CI, single runner for release

**CI matrix**:

| Runner | OS | Architecture |
|--------|----|-------------|
| `ubuntu-latest` | Linux | x64 |
| `windows-latest` | Windows | x64 |
| `macos-latest` | macOS | arm64 (M-series) |

**Release**: Stays on `ubuntu-latest` since the package is RID-agnostic. The `dotnet pack` command produces a single .nupkg containing all native libraries from all 6 DbgShim packages.

**Why not arm64 CI for Linux/Windows**: GitHub Actions doesn't provide free arm64 runners for Linux or Windows. ARM64 is validated via macOS arm64 runner. Native arm64 lib selection is architecture-detection logic (same code path), not OS-specific.

## R6: ClrDebug Wrapper Cross-Platform Compatibility

**Decision**: ClrDebug 0.3.4 is expected to work on all platforms without modification

**Rationale**: ClrDebug wraps COM interfaces (ICorDebug family) that .NET exposes on all platforms via its hosting APIs. The wrapper generates P/Invoke calls against the loaded runtime, not platform-specific libraries. The only platform-specific aspect is loading `dbgshim` (which we handle) and the COM interface vtable layout (which is identical across platforms for .NET's implementation).

**Risk**: ClrDebug was primarily developed and tested on Windows (its COM heritage). If it has implicit Windows assumptions (e.g., path separators in module names), these would surface during integration testing. The existing code already handles this for module path comparisons via `Path.DirectorySeparatorChar`.
