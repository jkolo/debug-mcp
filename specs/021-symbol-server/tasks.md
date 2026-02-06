# Tasks: Symbol Server Integration

**Input**: Design documents from `/specs/021-symbol-server/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/modules-list.json, quickstart.md

**Tests**: Included — Constitution III (Test-First) requires TDD for all feature implementation.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Exact file paths included in descriptions

---

## Phase 1: Setup

**Purpose**: Create directory structure and shared model types

- [x] T001 Create `DebugMcp/Services/Symbols/` directory for new symbol services
- [x] T002 [P] Create `SymbolStatus` enum in `DebugMcp/Models/Modules/SymbolStatus.cs` with values: None, Loaded, PendingDownload, Downloading, NotFound, Failed
- [x] T003 [P] Create `SymbolSource` enum in `DebugMcp/Models/Modules/SymbolSource.cs` with values: Local, Embedded, Cache, Server, None
- [x] T004 [P] Create `PeDebugInfo` record in `DebugMcp/Services/Symbols/PeDebugInfo.cs` with fields: PdbFileName, PdbGuid, Age, Stamp, IsPortablePdb, SymbolServerKey, ChecksumAlgorithm, Checksum, HasEmbeddedPdb
- [x] T005 [P] Create `SymbolResolutionResult` record in `DebugMcp/Services/Symbols/SymbolResolutionResult.cs` with fields: Status, PdbPath, Source, FailureReason
- [x] T006 [P] Create `SymbolServerOptions` record in `DebugMcp/Services/Symbols/SymbolServerOptions.cs` with defaults: Microsoft+NuGet servers, `~/.debug-mcp/symbols`, 30s timeout, 100MB max, 4 parallel
- [x] T007 Create `tests/DebugMcp.Tests/Unit/Symbols/` directory for unit tests
- [x] T008 Verify build compiles with `dotnet build`

---

## Phase 2: Foundational — PeDebugInfoReader

**Purpose**: Core PE debug directory reader that ALL user stories depend on

**⚠️ CRITICAL**: US1, US3, US4 all require PE debug info extraction. Must complete before any story work.

### Tests

- [x] T009 [P] Write `PeDebugInfoReaderTests` in `tests/DebugMcp.Tests/Unit/Symbols/PeDebugInfoReaderTests.cs`: test `TryReadDebugInfo` with DebugTestApp.dll (has local PDB, should extract CodeView GUID/Age/Path, IsPortablePdb=true)
- [x] T010 [P] Write `PeDebugInfoReaderTests`: test `TryReadDebugInfo` returns null for non-PE file (e.g., a .pdb file or text file)
- [x] T011 [P] Write `PeDebugInfoReaderTests`: test `SymbolServerKey` construction — Portable PDB produces `{guid:N}FFFFFFFF` format
- [x] T012 [P] Write `PeDebugInfoReaderTests`: test `HasEmbeddedPdb` detection (use a system assembly from .NET runtime that has embedded PDB)
- [x] T013 [P] Write `PeDebugInfoReaderTests`: test `ValidatePdb` with matching PDB returns true, non-matching PDB returns false

### Implementation

- [x] T014 Create `ISymbolResolver` interface in `DebugMcp/Services/Symbols/ISymbolResolver.cs` with methods: `ResolveAsync(assemblyPath, ct)`, `GetStatus(assemblyPath)`
- [x] T015 Implement `PeDebugInfoReader` in `DebugMcp/Services/Symbols/PeDebugInfoReader.cs`: `TryReadDebugInfo(assemblyPath)` using PEReader.ReadDebugDirectory + ReadCodeViewDebugDirectoryData + ReadPdbChecksumDebugDirectoryData; `TryExtractEmbeddedPdb(assemblyPath, outputPath)`; `ValidatePdb(pdbPath, debugInfo)` using GUID matching
- [x] T016 Verify all PeDebugInfoReader tests pass with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~PeDebugInfoReader"`

**Checkpoint**: PeDebugInfoReader validated — can extract debug info from any .NET assembly

---

## Phase 3: User Story 1 — Automatic PDB Download (Priority: P1) 🎯 MVP

**Goal**: Automatically download PDB symbols from public symbol servers for third-party libraries. Resolution chain: local → cache → server (embedded PDB in US4).

**Independent Test**: Attach to DebugTestApp (which references no NuGet packages, but loads .NET runtime assemblies), inspect stack trace with library frames, verify function names are resolved for System.Private.CoreLib or System.Console.

### Tests for User Story 1

- [x] T017 [P] [US1] Write `PersistentSymbolCacheTests` in `tests/DebugMcp.Tests/Unit/Symbols/PersistentSymbolCacheTests.cs`: test `Store()` creates correct directory layout `{cacheDir}/{pdbFileName}/{signature}/{pdbFileName}`
- [x] T018 [P] [US1] Write `PersistentSymbolCacheTests`: test `TryGetPath()` returns cached path when PDB exists, null when missing
- [x] T019 [P] [US1] Write `PersistentSymbolCacheTests`: test cache survives new instance (same directory, different object)
- [x] T020 [P] [US1] Write `SymbolServerClientTests` in `tests/DebugMcp.Tests/Unit/Symbols/SymbolServerClientTests.cs`: test `BuildDownloadUrl()` produces correct SSQP URL with lowercased filename
- [x] T021 [P] [US1] Write `SymbolServerClientTests`: test `TryDownloadAsync()` with mocked HttpMessageHandler returning 200 + PDB bytes → returns true, writes file
- [x] T022 [P] [US1] Write `SymbolServerClientTests`: test `TryDownloadAsync()` with 404 response → returns false, no file created
- [x] T023 [P] [US1] Write `SymbolServerClientTests`: test `TryDownloadAsync()` with timeout → returns false, logs warning
- [x] T024 [P] [US1] Write `SymbolServerClientTests`: test `TryDownloadAsync()` with Content-Length exceeding MaxFileSizeMB → returns false, skipped
- [x] T025 [P] [US1] Write `SymbolResolverTests` in `tests/DebugMcp.Tests/Unit/Symbols/SymbolResolverTests.cs`: test resolution chain order — local PDB found → returns Loaded/Local without checking server
- [x] T026 [P] [US1] Write `SymbolResolverTests`: test resolution chain — local not found, cache hit → returns Loaded/Cache
- [x] T027 [P] [US1] Write `SymbolResolverTests`: test resolution chain — local not found, cache miss, server download succeeds → returns Loaded/Server, PDB stored in cache
- [x] T028 [P] [US1] Write `SymbolResolverTests`: test resolution chain — all sources fail → returns NotFound/None
- [x] T029 [P] [US1] Write `SymbolResolverTests`: test concurrent resolution respects SemaphoreSlim concurrency limit
- [x] T030 [P] [US1] Write `SymbolResolverTests`: test `GetStatus()` returns current status (Downloading while in progress, Loaded after completion)
- [x] T031 [P] [US1] Write `SymbolResolverTests`: test downloaded PDB is validated (GUID match) before accepting

### Implementation for User Story 1

- [x] T032 [US1] Implement `PersistentSymbolCache` in `DebugMcp/Services/Symbols/PersistentSymbolCache.cs`: `TryGetPath(debugInfo)` checks `{cacheDir}/{pdbFileName}/{signature}/{pdbFileName}`; `Store(debugInfo, sourcePath)` copies file to cache layout; directory creation on demand
- [x] T033 [US1] Implement `SymbolServerClient` in `DebugMcp/Services/Symbols/SymbolServerClient.cs`: `BuildDownloadUrl(serverUrl, debugInfo)` constructs SSQP URL; `TryDownloadAsync(serverUrl, debugInfo, outputPath, ct)` downloads with HttpClient, Content-Length check, timeout, 404 handling; logging at Info/Warning levels per FR-010
- [x] T034 [US1] Implement `SymbolResolver` in `DebugMcp/Services/Symbols/SymbolResolver.cs`: implements `ISymbolResolver`; resolution chain: (1) local .pdb next to assembly, (2) PersistentSymbolCache, (3) SymbolServerClient for each configured server URL; SemaphoreSlim for concurrency (FR-012); ConcurrentDictionary for per-module status tracking; PDB validation via PeDebugInfoReader.ValidatePdb after download (FR-008); stores validated PDB in PersistentSymbolCache (FR-004); logging per FR-010
- [x] T035 [US1] Verify all US1 unit tests pass with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~PersistentSymbolCache|FullyQualifiedName~SymbolServerClient|FullyQualifiedName~SymbolResolver"`
- [x] T036 [US1] Refactor `PdbSymbolCache.LoadPdb()` in `DebugMcp/Services/Breakpoints/PdbSymbolCache.cs`: inject `ISymbolResolver`; replace `FindPdbPath()` call with `ISymbolResolver.ResolveAsync()`; keep `FindPdbPath()` as fast local-only fallback when resolver is null (backwards compatibility)
- [x] T037 [US1] Update `ProcessDebugger` in `DebugMcp/Services/ProcessDebugger.cs`: inject `ISymbolResolver`; in `OnLoadModule` callback fire-and-forget `ISymbolResolver.ResolveAsync()` via `Task.Run()` (FR-009); update `CheckHasSymbols()` to use `ISymbolResolver.GetStatus()` when available
- [x] T038 [US1] Register services in `DebugMcp/Program.cs`: register `PeDebugInfoReader`, `PersistentSymbolCache`, `SymbolServerClient` (with `HttpClient`), `ISymbolResolver` → `SymbolResolver`, `SymbolServerOptions` with default values
- [x] T039 [US1] Verify full build compiles with `dotnet build` and all existing tests pass with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"`

**Checkpoint**: PDB download from symbol servers works end-to-end. Stack traces for .NET runtime assemblies show resolved function names. Cached PDBs reused across sessions.

---

## Phase 4: User Story 2 — Symbol Server Configuration (Priority: P2)

**Goal**: Users can configure symbol server URLs, cache directory, and disable symbol downloads via CLI arguments and environment variables.

**Independent Test**: Start debugger with `--no-symbols`, attach to process, verify no network requests made. Start with `--symbol-servers "https://custom.server/symbols"`, verify custom server is queried.

### Tests for User Story 2

- [x] T040 [P] [US2] Write CLI argument test in `tests/DebugMcp.Tests/Unit/CliArgumentTests.cs`: test `--no-symbols` flag is recognized in `--help` output
- [x] T041 [P] [US2] Write CLI argument test: test `--symbol-servers`, `--symbol-cache`, `--symbol-timeout`, `--symbol-max-size` are recognized in `--help` output
- [x] T042 [P] [US2] Write `SymbolServerOptionsTests` in `tests/DebugMcp.Tests/Unit/Symbols/SymbolServerOptionsTests.cs`: test default values (Microsoft + NuGet URLs, 30s timeout, 100MB max, 4 concurrent)
- [x] T043 [P] [US2] Write `SymbolServerOptionsTests`: test parsing from environment variables (DEBUG_MCP_SYMBOL_SERVERS, DEBUG_MCP_SYMBOL_CACHE, DEBUG_MCP_NO_SYMBOLS)

### Implementation for User Story 2

- [x] T044 [US2] Add CLI options in `DebugMcp/Program.cs`: `--symbol-servers` (string, semicolon-separated URLs), `--symbol-cache` (string, directory path), `--no-symbols` (bool), `--symbol-timeout` (int, seconds), `--symbol-max-size` (int, MB)
- [x] T045 [US2] Implement `SymbolServerOptions` factory method/builder in `DebugMcp/Services/Symbols/SymbolServerOptions.cs`: parse CLI values with env var fallback (CLI > env var > default); expand `~` in cache directory path; validate constraints (timeout > 0, maxSize > 0, concurrent >= 1)
- [x] T046 [US2] Wire configuration in `DebugMcp/Program.cs`: construct `SymbolServerOptions` from parsed CLI values + env vars; pass to `SymbolResolver` and `SymbolServerClient` registrations; when `--no-symbols` set, register `SymbolServerOptions` with `Enabled = false`; log configured servers at startup (Info level)
- [x] T047 [US2] Update `SymbolResolver` to respect `SymbolServerOptions.Enabled` flag — when disabled, skip server download steps and return NotFound after checking local + cache
- [x] T048 [US2] Verify all US2 tests pass with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~CliArgument|FullyQualifiedName~SymbolServerOptions"`

**Checkpoint**: Symbol server behavior fully configurable via CLI and environment variables. `--no-symbols` disables all network access.

---

## Phase 5: User Story 3 — Symbol Download Status Visibility (Priority: P3)

**Goal**: The `modules_list` tool exposes per-module symbol status (loaded, not_found, downloading, failed, pending, none) with detail info.

**Independent Test**: Attach to a process, call `modules_list`, verify each module has `symbolStatus` and `symbolStatusDetail` fields in the response JSON.

### Tests for User Story 3

- [x] T049 [P] [US3] Write `ModulesListContractTests` update in `tests/DebugMcp.Tests/Contract/ModulesListContractTests.cs` (or create if needed): verify response JSON contains `symbolStatus` field for every module, value is one of: loaded, not_found, downloading, failed, pending, none
- [x] T050 [P] [US3] Write `ModulesListContractTests`: verify response JSON contains `symbolStatusDetail` field (nullable string) for every module

### Implementation for User Story 3

- [x] T051 [US3] Extend `ModuleInfo` record in `DebugMcp/Models/Modules/ModuleInfo.cs`: add `SymbolStatus` (string) and `SymbolStatusDetail` (string?) fields as last positional parameters with defaults ("none", null)
- [x] T052 [US3] Update `ProcessDebugger.ExtractModuleInfo()` in `DebugMcp/Services/ProcessDebugger.cs`: query `ISymbolResolver.GetStatus()` for the module path; map `SymbolStatus` enum to snake_case string; set `SymbolStatusDetail` to source name for loaded, error message for failed
- [x] T053 [US3] Update `ModulesListTool.ListModules()` in `DebugMcp/Tools/ModulesListTool.cs`: add `["symbolStatus"]` and `["symbolStatusDetail"]` to the module dictionary in the response
- [x] T054 [US3] Update any existing tests that construct `ModuleInfo` to include the new fields (search for `new ModuleInfo(` across test files)
- [x] T055 [US3] Verify all US3 tests pass and no existing tests break with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"`

**Checkpoint**: `modules_list` shows symbol status for every module. Agent can query which modules have symbols loaded vs pending vs failed.

---

## Phase 6: User Story 4 — Embedded PDB Support (Priority: P3)

**Goal**: The debugger extracts PDB data from assemblies with embedded Portable PDBs in the PE debug directory.

**Independent Test**: Load a .NET assembly known to have an embedded PDB (many runtime assemblies do). Verify symbols are resolved without any external .pdb file present.

### Tests for User Story 4

- [x] T056 [P] [US4] Write `PeDebugInfoReaderTests` addition: test `TryExtractEmbeddedPdb()` with an assembly that has embedded PDB → writes valid .pdb file to output path, returns true
- [x] T057 [P] [US4] Write `PeDebugInfoReaderTests` addition: test `TryExtractEmbeddedPdb()` with assembly that has NO embedded PDB → returns false, no file created
- [x] T058 [P] [US4] Write `SymbolResolverTests` addition: test resolution chain — local not found, embedded PDB found → returns Loaded/Embedded, PDB extracted and cached

### Implementation for User Story 4

- [x] T059 [US4] Implement `TryExtractEmbeddedPdb(assemblyPath, outputPath)` in `DebugMcp/Services/Symbols/PeDebugInfoReader.cs`: use `PEReader.ReadEmbeddedPortablePdbDebugDirectoryData()` to get MetadataReaderProvider; write metadata bytes to outputPath; return true if embedded entry exists, false otherwise
- [x] T060 [US4] Update `SymbolResolver.ResolveAsync()` in `DebugMcp/Services/Symbols/SymbolResolver.cs`: insert embedded PDB check between local check and cache check (step 2 in chain); call `PeDebugInfoReader.TryExtractEmbeddedPdb()` if `debugInfo.HasEmbeddedPdb`; store extracted PDB in PersistentSymbolCache; return Loaded/Embedded on success
- [x] T061 [US4] Verify all US4 tests pass with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~PeDebugInfoReader|FullyQualifiedName~SymbolResolver"`

**Checkpoint**: Assemblies with embedded PDBs have symbols extracted automatically. Resolution chain is complete: local → embedded → cache → server.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Edge cases, logging, and final validation

- [x] T062 [P] Add edge case handling in `SymbolServerClient`: disk space exhaustion during download (catch IOException, log error, continue without caching)
- [x] T063 [P] Add edge case handling in `SymbolResolver`: corrupted PDB after download (validation fails → discard file, mark as Failed with reason)
- [x] T064 [P] Add MCP logging in `SymbolResolver` per FR-010: Debug for cache hits, Info for download start/complete, Warning for failures/timeouts/size-exceeded
- [x] T065 Verify full test suite passes with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"`
- [x] T066 Run quickstart.md validation: verify implementation matches all 8 steps described in quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — create models and directory structure
- **Phase 2 (Foundational)**: Depends on Phase 1 — PeDebugInfoReader needs PeDebugInfo model
- **Phase 3 (US1)**: Depends on Phase 2 — uses PeDebugInfoReader for PDB validation
- **Phase 4 (US2)**: Depends on Phase 3 — extends SymbolServerOptions that US1 registers with defaults
- **Phase 5 (US3)**: Depends on Phase 3 — needs ISymbolResolver.GetStatus() from US1
- **Phase 6 (US4)**: Depends on Phase 2 — needs PeDebugInfoReader; can run in parallel with US2/US3
- **Phase 7 (Polish)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational (Phase 2). Core MVP.
- **US2 (P2)**: Depends on US1 (Phase 3) for the services it configures.
- **US3 (P3)**: Depends on US1 (Phase 3) for ISymbolResolver.GetStatus().
- **US4 (P3)**: Depends only on Foundational (Phase 2). Can run in parallel with US2/US3.

### Within Each User Story

- Tests written FIRST, verified to FAIL before implementation
- Models → Services → Integration → Verification
- Story complete before moving to next priority

### Parallel Opportunities

**Phase 1**: T002, T003, T004, T005, T006 all create separate files — run in parallel
**Phase 2**: T009–T013 test files — run in parallel; T014, T015 service files — run in parallel
**Phase 3 (US1)**: T017–T031 test files — all [P]; T032, T033 service files — [P] (different files)
**Phase 4 (US2)**: T040–T043 test files — all [P]
**Phase 5 (US3)**: T049, T050 — [P]
**Phase 6 (US4)**: T056–T058 — [P]; can run Phase 6 in parallel with Phase 4 and Phase 5
**Phase 7**: T062, T063, T064 — all [P]

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel (T017-T031):
Task: "Write PersistentSymbolCacheTests in tests/DebugMcp.Tests/Unit/Symbols/PersistentSymbolCacheTests.cs"
Task: "Write SymbolServerClientTests in tests/DebugMcp.Tests/Unit/Symbols/SymbolServerClientTests.cs"
Task: "Write SymbolResolverTests in tests/DebugMcp.Tests/Unit/Symbols/SymbolResolverTests.cs"

# After tests fail, launch parallel service implementations:
Task: "Implement PersistentSymbolCache in DebugMcp/Services/Symbols/PersistentSymbolCache.cs"
Task: "Implement SymbolServerClient in DebugMcp/Services/Symbols/SymbolServerClient.cs"

# Then sequential integration:
Task: "Implement SymbolResolver (depends on PersistentSymbolCache + SymbolServerClient)"
Task: "Refactor PdbSymbolCache to use ISymbolResolver"
Task: "Update ProcessDebugger to trigger resolution on module load"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (models, enums, records)
2. Complete Phase 2: Foundational (PeDebugInfoReader)
3. Complete Phase 3: User Story 1 (PDB download chain)
4. **STOP and VALIDATE**: Attach to process, verify .NET runtime PDBs download and stack traces resolve
5. This alone delivers the core value proposition

### Incremental Delivery

1. Setup + Foundational → PE debug info extraction works
2. + User Story 1 → PDB downloads work, cache persists → **MVP!**
3. + User Story 4 → Embedded PDBs extracted (can run in parallel with US2)
4. + User Story 2 → Full configuration support
5. + User Story 3 → Status visibility in modules_list
6. + Polish → Edge cases, logging, validation

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Constitution III (Test-First): write tests before implementation, verify they fail
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- FR references map to functional requirements in spec.md
