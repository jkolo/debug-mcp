# Data Model: Symbol Server Integration

**Feature**: 021-symbol-server
**Date**: 2026-02-06

## Entities

### SymbolStatus (Enum)

Per-module symbol resolution state. Tracks the lifecycle from initial detection through resolution.

| Value | Description |
|-------|-------------|
| `None` | No symbol resolution attempted (dynamic/in-memory modules) |
| `Loaded` | Symbols successfully loaded (local, embedded, cached, or downloaded) |
| `PendingDownload` | Queued for download from symbol server |
| `Downloading` | Active HTTP download in progress |
| `NotFound` | PDB not available from any source (all sources checked) |
| `Failed` | Download or validation error (network timeout, checksum mismatch, etc.) |

**State transitions**:
```
None → PendingDownload → Downloading → Loaded
                                     → NotFound
                                     → Failed
None → Loaded  (local PDB or embedded PDB found immediately)
```

### PeDebugInfo (Record)

Extracted from PE debug directory. Immutable value object.

| Field | Type | Description |
|-------|------|-------------|
| `PdbFileName` | `string` | PDB filename from CodeView entry path |
| `PdbGuid` | `Guid` | 16-byte GUID from CodeView entry |
| `Age` | `int` | Age from CodeView entry (always 1 for Portable PDB) |
| `Stamp` | `uint` | TimeDateStamp from debug directory entry |
| `IsPortablePdb` | `bool` | True when MinorVersion == 0x504D |
| `SymbolServerKey` | `string` | Constructed key: `{guid:N}FFFFFFFF` for Portable, `{guid:N}{age:x}` for Windows |
| `ChecksumAlgorithm` | `string?` | Algorithm name (e.g., "SHA256") or null if no PdbChecksum entry |
| `Checksum` | `ImmutableArray<byte>` | Expected PDB checksum bytes (empty if no PdbChecksum entry) |
| `HasEmbeddedPdb` | `bool` | True if PE contains EmbeddedPortablePdb debug entry |

**Validation rules**:
- `PdbFileName` must not be empty
- `PdbGuid` must not be `Guid.Empty`
- `SymbolServerKey` is derived, not user-settable

### SymbolResolutionResult (Record)

Result of resolving symbols for a module.

| Field | Type | Description |
|-------|------|-------------|
| `Status` | `SymbolStatus` | Final resolution status |
| `PdbPath` | `string?` | File path to PDB (null if not resolved) |
| `Source` | `SymbolSource` | Where the PDB was found |
| `FailureReason` | `string?` | Human-readable error (null on success) |

### SymbolSource (Enum)

Where a PDB was resolved from.

| Value | Description |
|-------|-------------|
| `Local` | Found next to assembly on disk |
| `Embedded` | Extracted from PE debug directory |
| `Cache` | Found in persistent symbol cache |
| `Server` | Downloaded from a symbol server |
| `None` | Not resolved |

### SymbolServerOptions (Record)

Configuration for symbol server behavior. Immutable after construction.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `ServerUrls` | `IReadOnlyList<string>` | Microsoft + NuGet | Ordered list of SSQP server base URLs |
| `CacheDirectory` | `string` | `~/.debug-mcp/symbols` | Persistent cache path |
| `Enabled` | `bool` | `true` | Master enable/disable for symbol downloads |
| `TimeoutSeconds` | `int` | `30` | Per-file download timeout |
| `MaxFileSizeMB` | `int` | `100` | Maximum PDB file size to download |
| `MaxConcurrentDownloads` | `int` | `4` | Parallel download limit |

**Validation rules**:
- `TimeoutSeconds` must be > 0
- `MaxFileSizeMB` must be > 0
- `MaxConcurrentDownloads` must be >= 1
- `CacheDirectory` must be a valid directory path

### ModuleInfo (Existing — Modified)

Extended with symbol status information.

| Field | Type | Change | Description |
|-------|------|--------|-------------|
| (all existing fields) | — | NO CHANGE | — |
| `SymbolStatus` | `string` | **NEW** | One of: `loaded`, `not_found`, `downloading`, `failed`, `pending`, `none` |
| `SymbolStatusDetail` | `string?` | **NEW** | Additional info: source for loaded, error for failed, server URL for downloading |

## Relationships

```
ProcessDebugger (OnLoadModule callback)
  → PeDebugInfoReader.ReadDebugInfo(modulePath) → PeDebugInfo
  → ISymbolResolver.ResolveAsync(modulePath, PeDebugInfo) → SymbolResolutionResult
    → checks: local path → embedded PDB → PersistentSymbolCache → SymbolServerClient
  → PdbSymbolCache.GetOrCreateReader(resolvedPath) → MetadataReader
  → ModuleInfo includes SymbolStatus from resolution state
```

## Service Interfaces

### ISymbolResolver

```
ResolveAsync(assemblyPath, cancellationToken) → SymbolResolutionResult
GetStatus(assemblyPath) → SymbolStatus
```

Orchestrates the resolution chain. Stateful: tracks per-module status for `GetStatus()` queries.

### SymbolServerClient

```
TryDownloadAsync(serverUrl, debugInfo, outputPath, cancellationToken) → bool
BuildDownloadUrl(serverUrl, debugInfo) → string
```

Stateless HTTP client. One instance shared across downloads.

### PersistentSymbolCache

```
TryGetPath(debugInfo) → string?
Store(debugInfo, sourcePath) → string (cached path)
GetCacheDirectory() → string
```

File-based cache with SymStore-compatible layout.

### PeDebugInfoReader

```
TryReadDebugInfo(assemblyPath) → PeDebugInfo?
TryExtractEmbeddedPdb(assemblyPath, outputPath) → bool
ValidatePdb(pdbPath, debugInfo) → bool
```

Wraps `PEReader` for clean PE debug directory access.
