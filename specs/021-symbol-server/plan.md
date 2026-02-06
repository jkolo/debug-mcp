# Implementation Plan: Symbol Server Integration

**Branch**: `021-symbol-server` | **Date**: 2026-02-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/021-symbol-server/spec.md`

## Summary

Automatically download PDB symbol files from public symbol servers (Microsoft, NuGet) so that stack traces, variable names, and source mapping work for third-party library code. Implements the SSQP (Simple Symbol Query Protocol) using in-box `System.Reflection.PortableExecutable.PEReader` APIs — zero new NuGet dependencies. Adds a persistent local symbol cache (`~/.debug-mcp/symbols/`), embedded PDB extraction, and per-module symbol status reporting through the existing `modules_list` tool.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: System.Reflection.Metadata (in-box), System.Reflection.PortableExecutable (in-box), System.Net.Http (in-box), System.IO.Compression (in-box for DeflateStream)
**Storage**: Persistent file-based symbol cache at `~/.debug-mcp/symbols/{pdbFileName}/{signature}/{pdbFileName}`
**Testing**: xUnit + FluentAssertions (existing), unit tests for symbol resolution logic, contract tests for tool schema
**Target Platform**: linux-x64
**Project Type**: Single project (existing structure)
**Performance Goals**: PDB resolution within 10s of module load (SC-001); cached PDBs instant (SC-002); no startup delay when servers unreachable (SC-003)
**Constraints**: Max 4 parallel downloads; 30s timeout per download; 100MB max PDB size; non-blocking async downloads
**Scale/Scope**: Typical .NET app loads 50-200 modules; top 50 NuGet packages should resolve (SC-004)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | Uses ICorDebug module events to trigger resolution; PEReader is in-box .NET API, not an external debugger |
| II. MCP Compliance | PASS | No new MCP tools added; extends existing `modules_list` output with `symbolStatus` field; structured JSON responses |
| III. Test-First | PASS | Unit tests for each component (PeDebugInfoReader, SymbolServerClient, SymbolCache, SymbolResolver); contract tests for updated tool schema |
| IV. Simplicity | PASS | No external NuGet packages; SSQP is a single HTTP GET; direct implementation ~300 lines vs pulling non-public packages |
| V. Observability | PASS | FR-010 requires logging at Debug (cache hits), Info (downloads), Warning (failures); all symbol resolution activity logged |

No violations. Gate passes.

## Project Structure

### Documentation (this feature)

```text
specs/021-symbol-server/
├── plan.md              # This file
├── research.md          # Phase 0: SSQP protocol, PEReader APIs, design decisions
├── data-model.md        # Phase 1: entity definitions
├── quickstart.md        # Phase 1: implementation guide
├── contracts/           # Phase 1: updated tool contracts
│   └── modules-list.json
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
DebugMcp/
├── Models/
│   └── Modules/
│       ├── ModuleInfo.cs              # MODIFY: add SymbolStatus field
│       └── SymbolStatus.cs            # NEW: enum (Loaded, NotFound, Downloading, Failed, PendingDownload)
├── Services/
│   ├── Breakpoints/
│   │   ├── PdbSymbolCache.cs          # MODIFY: extend FindPdbPath → use ISymbolResolver
│   │   ├── PdbSymbolReader.cs         # MODIFY: support MetadataReaderProvider from non-file sources
│   │   └── IPdbSymbolReader.cs        # NO CHANGE
│   ├── Symbols/
│   │   ├── ISymbolResolver.cs         # NEW: orchestrates resolution chain
│   │   ├── SymbolResolver.cs          # NEW: local → embedded → cache → server chain
│   │   ├── PeDebugInfoReader.cs       # NEW: reads PE debug directory (CodeView, checksum, embedded)
│   │   ├── SymbolServerClient.cs      # NEW: HTTP SSQP client with retry/timeout
│   │   ├── PersistentSymbolCache.cs   # NEW: file-based cache organized by signature
│   │   └── SymbolServerOptions.cs     # NEW: configuration record (URLs, cache dir, limits)
│   └── ProcessDebugger.cs             # MODIFY: trigger symbol resolution on module load; update CheckHasSymbols
├── Program.cs                         # MODIFY: register new services, add CLI options

tests/DebugMcp.Tests/
├── Unit/
│   └── Symbols/
│       ├── PeDebugInfoReaderTests.cs  # NEW: CodeView extraction, embedded PDB detection
│       ├── SymbolServerClientTests.cs # NEW: URL construction, HTTP mocking
│       ├── PersistentSymbolCacheTests.cs # NEW: store/retrieve/directory layout
│       └── SymbolResolverTests.cs     # NEW: resolution chain ordering, async behavior
└── Contract/
    └── ModulesListContractTests.cs    # MODIFY: verify symbolStatus in response
```

**Structure Decision**: Extends the existing single-project structure. New symbol services go under `DebugMcp/Services/Symbols/` to keep them organized separately from breakpoint-specific code. Models extend `DebugMcp/Models/Modules/`.

## Complexity Tracking

No Constitution violations — table not needed.
