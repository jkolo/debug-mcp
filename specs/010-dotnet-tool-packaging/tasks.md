# Tasks: .NET Tool Packaging

**Input**: Design documents from `/specs/010-dotnet-tool-packaging/`
**Prerequisites**: plan.md, spec.md, research.md, quickstart.md

**Tests**: Not explicitly requested in spec. Omitted per template rules.

**Organization**: Tasks grouped by user story (P1: Global Tool, P2: Local Tool, P3: Run from Source).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (.csproj Configuration)

**Purpose**: Add PackAsTool properties and NuGet metadata to enable tool packaging

- [X] T001 Add PackAsTool, ToolCommandName, Version, and RuntimeIdentifier properties to `DotnetMcp/DotnetMcp.csproj`
- [X] T002 Add NuGet metadata (PackageId, Authors, Description, PackageLicenseExpression, RepositoryUrl, PackageTags) to `DotnetMcp/DotnetMcp.csproj`

---

## Phase 2: Foundational (CLI Argument Handling)

**Purpose**: Implement `--version` and `--help` flags that all user stories depend on

**⚠️ CRITICAL**: CLI flags must work before tool packaging can be validated

- [X] T003 Implement `--version` flag in `DotnetMcp/Program.cs` — read `AssemblyInformationalVersion` and print to stdout, then exit
- [X] T004 Implement `--help` flag in `DotnetMcp/Program.cs` — print usage information (tool name, available flags, description), then exit
- [X] T005 Ensure unrecognized arguments display help message and exit with non-zero code in `DotnetMcp/Program.cs`

**Checkpoint**: `dotnet run --project DotnetMcp/DotnetMcp.csproj -- --version` and `-- --help` both work correctly

---

## Phase 3: User Story 1 - Install as Global Tool (Priority: P1) 🎯 MVP

**Goal**: Users can install via `dotnet tool install -g dotnet-mcp` and run from any directory

**Independent Test**: Run `dotnet pack`, install from local feed, verify `dotnet-mcp --version` works globally

### Implementation for User Story 1

- [X] T006 [US1] Run `dotnet pack DotnetMcp/DotnetMcp.csproj -c Release -o ./nupkg` and verify package is created with correct metadata
- [X] T007 [US1] Install tool from local package via `dotnet tool install -g dotnet-mcp --add-source ./nupkg` and verify `dotnet-mcp --version` outputs `1.0.0`
- [X] T008 [US1] Verify `dotnet-mcp` without arguments starts MCP server (stdio transport) — confirm process starts and can be terminated
- [X] T009 [US1] Verify native DbgShim library is included in the NuGet package (inspect .nupkg contents)
- [X] T010 [US1] Uninstall tool via `dotnet tool uninstall -g dotnet-mcp` and verify clean removal

**Checkpoint**: Global tool install, run, and uninstall all work correctly

---

## Phase 4: User Story 2 - Install as Local Tool (Priority: P2)

**Goal**: Users can add tool to a project manifest and run via `dotnet tool run`

**Independent Test**: Create temp project, add tool manifest, install from local feed, verify `dotnet tool run dotnet-mcp --version`

### Implementation for User Story 2

- [X] T011 [US2] Create test directory with `dotnet new tool-manifest`, install tool via `dotnet tool install dotnet-mcp --add-source ./nupkg`
- [X] T012 [US2] Verify `dotnet tool run dotnet-mcp --version` outputs version in project directory
- [X] T013 [US2] Verify `dotnet tool restore` works from `.config/dotnet-tools.json` manifest

**Checkpoint**: Local tool install, restore, and run all work correctly

---

## Phase 5: User Story 3 - Run from Source (Priority: P3)

**Goal**: Users can run directly via `dotnet run` without installing

**Independent Test**: Clone repo, run `dotnet run --project DotnetMcp/DotnetMcp.csproj -- --help`, verify output

### Implementation for User Story 3

- [X] T014 [US3] Verify `dotnet run --project DotnetMcp/DotnetMcp.csproj -- --version` displays version
- [X] T015 [US3] Verify `dotnet run --project DotnetMcp/DotnetMcp.csproj -- --help` displays usage information
- [X] T016 [US3] Verify `dotnet run --project DotnetMcp/DotnetMcp.csproj` starts MCP server correctly

**Checkpoint**: All three run-from-source scenarios work

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and final validation

- [X] T017 [P] Update project README with installation instructions (global, local, source) based on `specs/010-dotnet-tool-packaging/quickstart.md`
- [X] T018 Run quickstart.md validation — execute all commands from quickstart and confirm they work end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (.csproj must have Version property for --version to read)
- **User Story 1 (Phase 3)**: Depends on Phase 2 (needs --version/--help working + packable .csproj)
- **User Story 2 (Phase 4)**: Depends on Phase 3 (needs .nupkg from pack step)
- **User Story 3 (Phase 5)**: Depends on Phase 2 only (runs from source, no package needed)
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (Global Tool)**: Requires Phase 1 + Phase 2 — primary deliverable
- **US2 (Local Tool)**: Requires US1 (reuses the .nupkg)
- **US3 (Run from Source)**: Requires Phase 2 only — can run in parallel with US1

### Parallel Opportunities

- T001 and T002 modify the same file — must be sequential (or combined)
- T003, T004, T005 all modify Program.cs — must be sequential
- US1 and US3 can be validated in parallel after Phase 2
- T017 (README) can be written in parallel with US2/US3 validation

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Add .csproj properties (T001, T002)
2. Complete Phase 2: CLI flags (T003, T004, T005)
3. Complete Phase 3: Pack and validate global tool (T006–T010)
4. **STOP and VALIDATE**: `dotnet-mcp --version` works after global install
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → .csproj configured, CLI flags work
2. US1 (Global Tool) → Pack, install, validate → MVP complete
3. US2 (Local Tool) → Manifest install works → Team workflow enabled
4. US3 (Run from Source) → Already works, just validate → Contributor experience confirmed
5. Polish → README updated, quickstart validated

---

## Notes

- Total tasks: 18
- Tasks per story: US1=5, US2=3, US3=3, Setup=2, Foundational=3, Polish=2
- Key constraint: DbgShim native library must be present in .nupkg (T009 validates this)
- All Phase 2 tasks modify Program.cs — execute sequentially
- Phase 1 tasks modify DotnetMcp.csproj — execute sequentially (or as single task)
