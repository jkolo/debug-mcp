# Tasks: Cross-Platform Support (Windows & macOS)

**Input**: Design documents from `/specs/025-cross-platform/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, quickstart.md

**Tests**: Included — the constitution mandates test-first (Principle III). Platform detection tests validate FindDbgShim logic.

**Organization**: US1 (Windows) and US2 (macOS) share the same code changes and are combined into a single phase. US3 (ARM64) is delivered by the same FindDbgShim changes. US4 (CI/CD) and US5 (packaging) are independent.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)
- Exact file paths included in all descriptions

## Path Conventions

- **Main project**: `DebugMcp/`
- **Test files**: `tests/DebugMcp.Tests/Contract/`
- **CI/CD**: `.github/workflows/`
- **Spec files**: `specs/025-cross-platform/`

---

## Phase 1: Foundational — NuGet Package References

**Purpose**: Update build configuration to include all platform native libraries. This MUST complete before any platform-specific code or testing can proceed.

- [ ] T001 Remove `<RuntimeIdentifier>linux-x64</RuntimeIdentifier>` and add 5 new DbgShim package references (win-x64, win-arm64, linux-arm64, osx-x64, osx-arm64) to `DebugMcp/DebugMcp.csproj`
- [ ] T002 Update `<Description>` in `DebugMcp/DebugMcp.csproj` — replace "on Linux" with "on Windows, macOS, and Linux"
- [ ] T003 Verify build succeeds with `dotnet build` (no RuntimeIdentifier, all 6 DbgShim packages resolve)

**Checkpoint**: Project builds without hardcoded RID. All 6 native libraries available.

---

## Phase 2: US1+US2+US3 — Platform & Architecture Detection (Priority: P1/P2)

**Goal**: FindDbgShim discovers the correct native library on all 6 platform/architecture combinations. ReapLaunchedChild works on macOS in addition to Linux.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~PlatformDetection"` — all platform detection tests pass on current platform. Build and run on Windows, macOS, Linux to verify.

### Tests (written first, must fail before implementation)

- [ ] T004 [US1] Create `tests/DebugMcp.Tests/Contract/PlatformDetectionTests.cs` with test class that verifies FindDbgShim returns a valid path on the current platform (uses reflection to invoke private method or tests via public InitializeDbgShim path)
- [ ] T005 [US1] Add test in `tests/DebugMcp.Tests/Contract/PlatformDetectionTests.cs` that verifies the tool reports a clear error message when DbgShim is not found (simulated via nonexistent NuGet cache path)
- [ ] T006 [US3] Add test in `tests/DebugMcp.Tests/Contract/PlatformDetectionTests.cs` that verifies `RuntimeInformation.ProcessArchitecture` is used (not `OSArchitecture`) for architecture selection
- [ ] T007 Run tests with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~PlatformDetection"` — verify tests are discoverable (some may fail before implementation — expected for TDD)

### Implementation

- [ ] T008 [P] [US1] Update `FindDbgShim` in `DebugMcp/Services/ProcessDebugger.cs` — use `RuntimeInformation.ProcessArchitecture` to select between x64 and arm64 variants for each OS (win-x64/win-arm64, linux-x64/linux-arm64, osx-x64/osx-arm64)
- [ ] T009 [P] [US2] Update `ReapLaunchedChild` in `DebugMcp/Services/ProcessDebugger.cs` — change guard from `!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)` to `!(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))`
- [ ] T010 Run all tests with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~PlatformDetection"` — all platform detection tests pass
- [ ] T011 Run full unit+contract suite with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — verify no regressions

**Checkpoint**: All 34 tools work on the current platform. FindDbgShim handles all 6 RIDs. ReapLaunchedChild works on macOS.

---

## Phase 3: US4 — CI/CD Matrix Builds (Priority: P2)

**Goal**: CI automatically builds and tests on Linux, Windows, and macOS. Platform-specific failures are clearly identified.

**Independent Test**: Push a commit and verify CI runs on all 3 OS runners with build + test passing.

- [ ] T012 [US4] Update `.github/workflows/ci.yml` — add `strategy.matrix` with `os: [ubuntu-latest, windows-latest, macos-latest]`, set `runs-on: ${{ matrix.os }}`, keep existing build+test steps unchanged
- [ ] T013 [US4] Update `.github/workflows/release.yml` — remove `-p:RuntimeIdentifier=linux-x64` from build/pack steps (if present), ensure `dotnet pack` produces RID-agnostic package
- [ ] T014 [US4] Push branch and verify CI matrix runs: 3 jobs (ubuntu, windows, macos), all pass build + unit tests

**Checkpoint**: CI validates all platforms on every push. Release produces RID-agnostic package.

---

## Phase 4: US5 — Multi-Platform NuGet Package (Priority: P3)

**Goal**: Single NuGet package installs correctly on all platforms with automatic native library resolution.

**Independent Test**: Run `dotnet pack`, inspect package contents, verify all 6 native libraries are included.

- [ ] T015 [US5] Run `dotnet pack DebugMcp/DebugMcp.csproj -c Release -o ./nupkg` and verify: (1) single .nupkg produced, (2) package size < 50 MB, (3) `unzip -l` shows all 6 dbgshim natives under `runtimes/` paths
- [ ] T016 [US5] Test local install with `dotnet tool install -g debug-mcp --add-source ./nupkg` on current platform — verify `debug-mcp --version` works

**Checkpoint**: Package ready for NuGet.org publishing. Install works on current platform.

---

## Phase 5: Polish & Final Verification

**Purpose**: Documentation updates, full regression test, quickstart validation.

- [ ] T017 Update `PackageTags` in `DebugMcp/DebugMcp.csproj` — add `windows`, `macos`, `cross-platform` tags
- [ ] T018 Run full build with `dotnet build` — verify 0 errors, 0 warnings
- [ ] T019 Run full test suite with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — all tests pass
- [ ] T020 Run quickstart.md validation steps from `specs/025-cross-platform/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — start immediately
- **Phase 2 (US1+US2+US3)**: Depends on Phase 1 (needs DbgShim packages to build)
- **Phase 3 (US4 — CI/CD)**: Depends on Phase 1 (needs buildable project) — can run in parallel with Phase 2
- **Phase 4 (US5 — Packaging)**: Depends on Phase 1 + Phase 2 (needs working code to pack)
- **Phase 5 (Polish)**: Depends on all previous phases

### User Story Dependencies

- **US1 (Windows, P1)**: Implemented in Phase 2 — needs Phase 1
- **US2 (macOS, P1)**: Implemented in Phase 2 — same code changes as US1
- **US3 (ARM64, P2)**: Implemented in Phase 2 — same FindDbgShim changes as US1/US2
- **US4 (CI/CD, P2)**: Phase 3 — independent of Phase 2 code changes, only needs Phase 1
- **US5 (Packaging, P3)**: Phase 4 — needs everything working

### Within Phase 2

- T004, T005, T006 (tests) → T007 (run tests) → T008, T009 (implementation, parallel) → T010, T011 (verify)

### Parallel Opportunities

- T008 and T009 are [P] — different methods in the same file, no dependencies
- Phase 2 and Phase 3 can run in parallel after Phase 1 completes
- T004, T005, T006 can be written in the same file simultaneously

---

## Implementation Strategy

### MVP First (Phase 1 + Phase 2)

1. Complete Phase 1: Update csproj (unblocks everything)
2. Complete Phase 2: Platform detection + tests
3. **STOP and VALIDATE**: Build succeeds, all tests pass on current platform
4. This alone delivers US1+US2+US3 functionality

### Incremental Delivery

1. Phase 1 → csproj ready
2. Phase 2 → Platform code works (US1+US2+US3 delivered)
3. Phase 3 → CI validates all platforms (US4 delivered)
4. Phase 4 → Package verified (US5 delivered)
5. Phase 5 → Polish + final validation

### Single-Developer Strategy

Since this is a small feature (5 files, 20 tasks), optimal order:
1. T001–T003: Update csproj, verify build
2. T004–T007: Write tests, verify they're discoverable
3. T008–T009: Implement FindDbgShim + ReapLaunchedChild changes
4. T010–T011: Verify all tests pass
5. T012–T014: CI/CD matrix
6. T015–T016: Package verification
7. T017–T020: Polish and final validation

---

## Notes

- [P] tasks = different files or different methods, no dependencies
- [Story] label maps task to specific user story
- US1, US2, US3 share the same code changes — combined into Phase 2
- Constitution Principle III (Test-First) requires T004–T006 before T008–T009
- No new projects, abstractions, or architectural changes — all surgical edits
- ARM64 support (US3) is "free" once FindDbgShim handles architecture detection
