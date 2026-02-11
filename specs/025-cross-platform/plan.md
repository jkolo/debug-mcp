# Implementation Plan: Cross-Platform Support (Windows & macOS)

**Branch**: `025-cross-platform` | **Date**: 2026-02-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/025-cross-platform/spec.md`

## Summary

Add Windows (x64, arm64), macOS (x64, arm64), and Linux arm64 support to debug-mcp. The codebase already has partial cross-platform support in `FindDbgShim` and `IsNetProcess`. The primary work is: (1) remove the hardcoded `linux-x64` RuntimeIdentifier, (2) add all 6 DbgShim native packages, (3) extend architecture detection from x64-only to x64+arm64, (4) fix the Linux-only `ReapLaunchedChild` guard to include macOS, and (5) update CI/CD for matrix builds.

## Technical Context

**Language/Version**: C# / .NET 10.0 (global.json pins 10.0.102)
**Primary Dependencies**: ClrDebug 0.3.4, ModelContextProtocol 0.7.0-preview.1, Microsoft.Diagnostics.DbgShim 9.0.x (6 RID variants)
**Storage**: N/A
**Testing**: xUnit + FluentAssertions + Moq; unit + contract tests run on all platforms
**Target Platform**: Windows x64/arm64, macOS x64/arm64, Linux x64/arm64
**Project Type**: Single .NET tool (PackAsTool)
**Performance Goals**: N/A (no performance-sensitive changes)
**Constraints**: NuGet package size < 50 MB with all native binaries; CI time < 10 min across matrix
**Scale/Scope**: 6 RIDs, ~5 files changed, ~2 files added/modified in CI

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | No change — still uses ICorDebug directly via ClrDebug |
| II. MCP Compliance | PASS | No tool interface changes — same 34 tools, same parameters, same responses |
| III. Test-First | PASS | Platform detection tests written before code changes; CI matrix validates |
| IV. Simplicity | PASS | Removing hardcoded RID is simpler than current state; adding DbgShim refs is mechanical |
| V. Observability | PASS | Existing structured logging in FindDbgShim covers new platforms; error messages improved for unsupported platforms |

No violations. No complexity justifications needed.

## Project Structure

### Documentation (this feature)

```text
specs/025-cross-platform/
├── spec.md
├── plan.md              # This file
├── research.md          # Phase 0: DbgShim packaging, multi-RID tool strategy
├── quickstart.md        # Phase 1: Verification steps
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (files changed)

```text
DebugMcp/
├── DebugMcp.csproj                    # Remove RuntimeIdentifier, add 6 DbgShim refs
└── Services/
    └── ProcessDebugger.cs             # FindDbgShim: add arm64; ReapLaunchedChild: add macOS

tests/DebugMcp.Tests/
└── Contract/
    └── PlatformDetectionTests.cs      # NEW: verify FindDbgShim logic per platform/arch

.github/workflows/
├── ci.yml                             # Add OS matrix (ubuntu, windows, macos)
└── release.yml                        # Remove RID-specific build, pack RID-agnostic
```

**Structure Decision**: Existing single-project layout unchanged. No new projects, directories, or abstractions. Changes are surgical edits to 5 existing files plus 1 new test file.

## Design Decisions

### D1: RID-Agnostic Tool Packaging (vs per-platform packages)

**Decision**: Remove `<RuntimeIdentifier>linux-x64</RuntimeIdentifier>` and include all 6 DbgShim NuGet packages. The .NET tool becomes RID-agnostic — NuGet's `runtimes/{rid}/native/` convention handles native library selection at install time.

**Rationale**: A single `dotnet tool install -g debug-mcp` works on every platform. No need for users to choose a platform-specific package. The alternative (per-platform packages like `debug-mcp-win-x64`) fragments the install experience and complicates documentation.

**Trade-off**: Package size increases (6 native libs instead of 1). Each DbgShim is ~3-5 MB, so total ~20-30 MB — within the 50 MB budget.

### D2: Architecture Detection via RuntimeInformation.ProcessArchitecture

**Decision**: Extend `FindDbgShim` to check `RuntimeInformation.ProcessArchitecture` for `Arm64` vs `X64`, selecting the appropriate `runtimes/{os}-{arch}/native/` path.

**Rationale**: `RuntimeInformation.ProcessArchitecture` correctly reports the actual running architecture (not the OS architecture), so it works correctly under Rosetta 2 (reports X64) and native ARM64 (reports Arm64).

### D3: ReapLaunchedChild Extended to macOS

**Decision**: Change the guard from `!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)` to `!(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))`. The `waitpid` P/Invoke works on both platforms (libc is available on macOS).

**Rationale**: macOS uses the same `fork()+ptrace` pattern as Linux for `CreateProcessForLaunch`. Without reaping, the same SIGCHLD/FailFast crash would occur.

### D4: CI Matrix Strategy

**Decision**: Add `strategy.matrix` with `os: [ubuntu-latest, windows-latest, macos-latest]` to CI. Use `macos-14` for arm64 testing. Release workflow stays on single runner but builds RID-agnostic.

**Rationale**: GitHub Actions provides free runners for all three OS families. Matrix ensures platform-specific regressions are caught before merge.

## File Change Summary

| File | Change Type | Description |
|------|------------|-------------|
| `DebugMcp/DebugMcp.csproj` | Modify | Remove RuntimeIdentifier; add 5 new DbgShim packages; update description |
| `DebugMcp/Services/ProcessDebugger.cs` | Modify | FindDbgShim: add arm64 arch detection; ReapLaunchedChild: include macOS |
| `tests/DebugMcp.Tests/Contract/PlatformDetectionTests.cs` | New | Tests for FindDbgShim platform/arch logic, unsupported platform error |
| `.github/workflows/ci.yml` | Modify | Add OS matrix strategy |
| `.github/workflows/release.yml` | Modify | Remove hardcoded RID, build/pack RID-agnostic |
