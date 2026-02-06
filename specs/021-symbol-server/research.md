# Research: Symbol Server Integration

**Feature**: 021-symbol-server
**Date**: 2026-02-06

## Decision 1: SSQP Protocol Implementation

**Decision**: Implement SSQP client from scratch using `HttpClient`.

**Rationale**: The SSQP protocol is trivially simple — a single HTTP GET with a well-defined URL pattern. The official `Microsoft.SymbolStore` package is NOT published to nuget.org (only on an internal Azure DevOps feed) and drags in `Microsoft.FileFormats` which duplicates in-box `PEReader` functionality. Implementing from scratch adds zero dependencies and is ~50 lines of code.

**Alternatives considered**:
- `Microsoft.SymbolStore` NuGet package — rejected: not on nuget.org, requires Azure DevOps feed, adds unnecessary dependencies
- `dotnet-symbol` tool invocation — rejected: external process dependency, not embeddable

### SSQP URL Format

```
{server_base_url}/{pdb_filename}/{signature}/{pdb_filename}
```

**For Portable PDB** (the common case on .NET 10 / Linux):
```
{server}/{filename.pdb}/{guid_N}FFFFFFFF/{filename.pdb}
```

**For Windows PDB** (rare on Linux, but supported):
```
{server}/{filename.pdb}/{guid_N}{age_hex}/{filename.pdb}
```

Where:
- `guid_N` = `Guid.ToString("N")` — 32 lowercase hex chars, no dashes
- `FFFFFFFF` literal for Portable PDB (age is always 1, but convention uses FFFFFFFF to avoid key collisions)
- `age_hex` = `Age.ToString("x")` for Windows PDB (minimal lowercase hex digits)
- Filename is **lowercased** in URL path

### Default Symbol Servers

| Server | Base URL |
|--------|----------|
| Microsoft | `https://msdl.microsoft.com/download/symbols` |
| NuGet | `https://symbols.nuget.org/download/symbols` |

### HTTP Behavior
- Standard GET request, response is `application/octet-stream`
- Must follow 302/304 redirects (HttpClient does this by default)
- 404 = PDB not available on this server
- No authentication for public servers; private servers use standard HTTP headers

## Decision 2: PE Debug Directory Reading

**Decision**: Use in-box `System.Reflection.PortableExecutable.PEReader` APIs.

**Rationale**: `PEReader` is in-box for .NET 10, provides all required APIs (`ReadDebugDirectory()`, `ReadCodeViewDebugDirectoryData()`, `ReadEmbeddedPortablePdbDebugDirectoryData()`, `ReadPdbChecksumDebugDirectoryData()`), and is already implicitly available via `System.Reflection.Metadata` which the project uses.

**Alternatives considered**:
- `Microsoft.FileFormats` — rejected: not on nuget.org, duplicates in-box functionality
- Manual PE parsing — rejected: unnecessary when `PEReader` provides structured APIs

### Key API Usage

```csharp
using var peStream = File.OpenRead(assemblyPath);
using var peReader = new PEReader(peStream);
var entries = peReader.ReadDebugDirectory();

// CodeView entry → PDB GUID + Age + original path
var cvEntry = entries.First(e => e.Type == DebugDirectoryEntryType.CodeView);
var cv = peReader.ReadCodeViewDebugDirectoryData(cvEntry);
// cv.Guid, cv.Age, cv.Path
// cvEntry.IsPortableCodeView → true for Portable PDB

// PDB Checksum entry → algorithm + hash for validation
var csEntry = entries.FirstOrDefault(e => e.Type == DebugDirectoryEntryType.PdbChecksum);
var cs = peReader.ReadPdbChecksumDebugDirectoryData(csEntry);
// cs.AlgorithmName ("SHA256"), cs.Checksum (ImmutableArray<byte>)

// Embedded PDB entry → extract without separate file
var embEntry = entries.FirstOrDefault(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
var provider = peReader.ReadEmbeddedPortablePdbDebugDirectoryData(embEntry);
// Handles Deflate decompression automatically
```

### Portable PDB Detection

`DebugDirectoryEntry.IsPortableCodeView` — true when `MinorVersion == 0x504D` ("PM").

## Decision 3: Embedded PDB Extraction

**Decision**: Use `PEReader.ReadEmbeddedPortablePdbDebugDirectoryData()` for in-memory access; save to persistent cache for reuse.

**Rationale**: The API handles Deflate decompression automatically. Embedded PDBs are stored in the PE debug directory as `MPDB` signature + 4-byte uncompressed size + raw Deflate data. The in-box API abstracts all of this.

**Alternatives considered**:
- Manual Deflate extraction — rejected: PEReader does it in one call
- `TryOpenAssociatedPortablePdb()` — considered but rejected for our use case: it only checks local path + embedded, doesn't integrate with symbol servers. We need the full chain.

## Decision 4: PDB Validation

**Decision**: Validate using PDB ID matching (GUID + Stamp) and PDB checksum when available.

**Rationale**: Two-level validation ensures correct PDB. GUID matching is fast (in-memory comparison). Checksum validation (SHA256 hash of PDB with zeroed PDB ID) provides cryptographic integrity but is optional — not all assemblies have PdbChecksum entries.

### Validation Steps

1. **PDB ID match** (always): Read `#Pdb` stream header from downloaded PDB. Compare 16-byte GUID with CodeView entry's GUID.
2. **Checksum match** (when PdbChecksum entry exists): Hash downloaded PDB with PDB ID zeroed out. Compare with PE's stored checksum.

## Decision 5: Persistent Symbol Cache Layout

**Decision**: Use SymStore-compatible directory layout: `{cacheDir}/{pdb_filename}/{signature}/{pdb_filename}`.

**Rationale**: Matches the SSQP URL path structure, making cache lookups a simple directory check. Compatible with other tools that use the same convention (Visual Studio, dotnet-symbol).

### Example

```
~/.debug-mcp/symbols/
├── System.Private.CoreLib.pdb/
│   └── a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4FFFFFFFF/
│       └── System.Private.CoreLib.pdb
├── Newtonsoft.Json.pdb/
│   └── ...
```

## Decision 6: Async Download Architecture

**Decision**: Fire-and-forget symbol resolution triggered on `OnLoadModule` callback; module status updated asynchronously; `SemaphoreSlim` for concurrency control.

**Rationale**: Module load callbacks happen on the ICorDebug callback thread and must `Continue(false)` immediately. Symbol resolution is kicked off as a background task. The `modules_list` tool queries current status (which may be "downloading" or "pending").

### Concurrency Control

- `SemaphoreSlim(4)` — max 4 parallel downloads (FR-012)
- Per-module `TaskCompletionSource` tracks download state
- Downloads use `CancellationTokenSource` with 30s timeout per file (FR-011)

## Decision 7: Configuration Mechanism

**Decision**: CLI arguments + environment variables, parsed in `Program.cs`.

**Rationale**: Consistent with existing CLI options (`--stderr-logging`, `--no-roslyn`). Environment variables provide a fallback for container/CI scenarios.

### Configuration Options

| CLI Flag | Env Var | Default | Description |
|----------|---------|---------|-------------|
| `--symbol-servers` | `DEBUG_MCP_SYMBOL_SERVERS` | `https://msdl.microsoft.com/download/symbols;https://symbols.nuget.org/download/symbols` | Semicolon-separated list of server URLs |
| `--symbol-cache` | `DEBUG_MCP_SYMBOL_CACHE` | `~/.debug-mcp/symbols` | Local cache directory |
| `--no-symbols` | `DEBUG_MCP_NO_SYMBOLS` | `false` | Disable all symbol server downloads |
| `--symbol-timeout` | `DEBUG_MCP_SYMBOL_TIMEOUT` | `30` | Per-file download timeout in seconds |
| `--symbol-max-size` | `DEBUG_MCP_SYMBOL_MAX_SIZE` | `100` | Max PDB file size in MB |

## Decision 8: Integration with Existing PdbSymbolCache

**Decision**: Refactor `PdbSymbolCache.FindPdbPath()` to use `ISymbolResolver` for the full resolution chain; `PdbSymbolCache` continues to cache `MetadataReader` instances.

**Rationale**: `PdbSymbolCache` already handles MetadataReader lifecycle (caching, disposal, invalidation). The new `ISymbolResolver` handles finding/downloading the PDB file path. Clean separation of concerns: resolver finds the file, cache manages the reader.

### Modified Flow

```
PdbSymbolCache.GetOrCreateReader(assemblyPath)
  → LoadPdb(assemblyPath)
    → ISymbolResolver.ResolveAsync(assemblyPath)   // NEW: replaces FindPdbPath()
      → 1. Check local path (same directory)
      → 2. Check embedded PDB in PE
      → 3. Check persistent cache (~/.debug-mcp/symbols/)
      → 4. Download from symbol servers (async, with cache-on-success)
    → MetadataReaderProvider.FromPortablePdbStream(resolvedPath)
```
