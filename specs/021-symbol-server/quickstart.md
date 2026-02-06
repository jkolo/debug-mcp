# Quickstart: Symbol Server Integration

**Feature**: 021-symbol-server
**Date**: 2026-02-06

## Implementation Order

Follow this order to build incrementally with tests passing at each step.

### Step 1: PeDebugInfoReader + Tests

**What**: Read PE debug directory entries from assembly files.

**Key APIs**:
- `PEReader.ReadDebugDirectory()` → list of `DebugDirectoryEntry`
- `PEReader.ReadCodeViewDebugDirectoryData(entry)` → `CodeViewDebugDirectoryData` (Guid, Age, Path)
- `DebugDirectoryEntry.IsPortableCodeView` → true for Portable PDB
- `PEReader.ReadPdbChecksumDebugDirectoryData(entry)` → `PdbChecksumDebugDirectoryData`
- `PEReader.ReadEmbeddedPortablePdbDebugDirectoryData(entry)` → `MetadataReaderProvider`

**Test strategy**: Use real .NET assemblies from the test output (e.g., `DebugTestApp.dll` has a local PDB; system assemblies from runtime have embedded PDBs).

**Symbol server key construction**:
```csharp
// Portable PDB (most .NET assemblies on Linux)
string key = $"{cv.Guid:N}FFFFFFFF";

// Windows PDB (rare on Linux)
string key = $"{cv.Guid:N}{cv.Age:x}";
```

### Step 2: PersistentSymbolCache + Tests

**What**: Store and retrieve PDB files in `~/.debug-mcp/symbols/` using SymStore layout.

**Directory layout**: `{cacheDir}/{pdb_filename}/{signature}/{pdb_filename}`

**Example**: `~/.debug-mcp/symbols/Newtonsoft.Json.pdb/a1b2c3d4...FFFFFFFF/Newtonsoft.Json.pdb`

**Test strategy**: Use temp directories; verify correct path construction and file round-trip.

### Step 3: SymbolServerClient + Tests

**What**: HTTP SSQP client for downloading PDBs.

**URL format**: `{server}/{pdb_filename.ToLower()}/{signature}/{pdb_filename.ToLower()}`

**Behavior**:
- `HttpClient.GetAsync()` with `HttpCompletionOption.ResponseHeadersRead`
- Check `Content-Length` header against `MaxFileSizeMB` before downloading body
- 30s timeout via `CancellationTokenSource`
- Return `false` on 404 (not found), throw on other errors

**Test strategy**: Mock `HttpMessageHandler` for URL construction tests, success/failure/timeout scenarios.

### Step 4: SymbolResolver (resolution chain) + Tests

**What**: Orchestrate the full resolution chain: local → embedded → cache → server.

**Chain logic**:
1. Check local PDB (same directory as assembly, same name + `.pdb`)
2. Check embedded PDB in PE (extract to cache if found)
3. Check persistent symbol cache
4. Download from configured symbol servers (try each in order)
5. Validate downloaded PDB (GUID match + optional checksum)
6. Store validated PDB in persistent cache

**Concurrency**: `SemaphoreSlim(maxConcurrentDownloads)` around server downloads only.

**Test strategy**: Mock `PeDebugInfoReader`, `PersistentSymbolCache`, `SymbolServerClient`; verify chain ordering and status transitions.

### Step 5: PdbSymbolCache Integration

**What**: Refactor `PdbSymbolCache` to use `ISymbolResolver` for PDB discovery.

**Changes**:
- `FindPdbPath()` → call `ISymbolResolver.ResolveAsync()` instead of just checking local directory
- `LoadPdb()` → handle both file-path and embedded `MetadataReaderProvider` sources
- `CachedPdbEntry` → support entries that don't have a `FileStream` (embedded PDB case)

### Step 6: ModuleInfo + ProcessDebugger Integration

**What**: Add `SymbolStatus` to module info; trigger resolution on module load.

**Changes**:
- `ModuleInfo` record: add `SymbolStatus` and `SymbolStatusDetail` fields
- `ProcessDebugger.OnLoadModule`: fire-and-forget `ISymbolResolver.ResolveAsync()`
- `ProcessDebugger.CheckHasSymbols()`: replace with `ISymbolResolver.GetStatus()`
- `ProcessDebugger.ExtractModuleInfo()`: include symbol status from resolver

### Step 7: Configuration (CLI + env vars)

**What**: Wire up `SymbolServerOptions` from CLI arguments and environment variables.

**New CLI options in `Program.cs`**:
- `--symbol-servers <urls>` / `DEBUG_MCP_SYMBOL_SERVERS`
- `--symbol-cache <path>` / `DEBUG_MCP_SYMBOL_CACHE`
- `--no-symbols` / `DEBUG_MCP_NO_SYMBOLS`

**Priority**: CLI argument > environment variable > default value.

### Step 8: modules_list Tool Update

**What**: Expose `symbolStatus` and `symbolStatusDetail` in tool response.

**Changes**: Add two new fields to the module dictionary in `ModulesListTool.ListModules()`.

## Key Gotchas

1. **OnLoadModule callback thread**: Must not block — kick off resolution as fire-and-forget `Task.Run()`.
2. **PEReader requires seekable stream**: Use `File.OpenRead()`, not pipes.
3. **PDB filename lowercasing**: SSQP URLs use lowercased filenames.
4. **Content-Length check**: Some servers don't send Content-Length — fall back to streaming with byte-count limit.
5. **Embedded PDB extraction**: `ReadEmbeddedPortablePdbDebugDirectoryData()` returns a `MetadataReaderProvider`, not a file. To cache, write the metadata bytes to disk.
6. **Thread safety**: `SymbolResolver` tracks per-module status — use `ConcurrentDictionary<string, SymbolStatus>`.
