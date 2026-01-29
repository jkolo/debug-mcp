# Tasks: CI/CD Pipeline for Tag-Based NuGet Publishing

**Input**: Design documents from `/specs/011-ci-cd-pipeline/`
**Prerequisites**: plan.md, spec.md, research.md, quickstart.md

**Tests**: Not requested. CI/CD workflows are validated by execution.

**Organization**: Tasks grouped by user story (P1: Release on Tag, P2: CI on Push).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (SDK Pinning & Package Bumps)

**Purpose**: Pin .NET SDK and update all .NET-related NuGet packages to latest versions

- [x] T001 Create `global.json` at repository root with .NET SDK `10.0.102` and `rollForward: latestPatch`
- [x] T002 Remove hardcoded `<Version>1.0.0</Version>` from `DotnetMcp/DotnetMcp.csproj` (version will be set by CI)
- [x] T003 Bump all .NET-related NuGet packages to latest versions in `DotnetMcp/DotnetMcp.csproj`
- [x] T004 [P] Bump all .NET-related NuGet packages to latest versions in `tests/DotnetMcp.Tests/DotnetMcp.Tests.csproj`
- [x] T005 [P] Bump all .NET-related NuGet packages to latest versions in `tests/DotnetMcp.E2E/DotnetMcp.E2E.csproj`
- [x] T006 Verify build and tests pass after package bumps via `dotnet build` and `dotnet test`

**Checkpoint**: Project builds and all tests pass with pinned SDK and updated packages

---

## Phase 2: Foundational (GitHub Actions Directory)

**Purpose**: Create `.github/workflows/` directory structure

- [x] T007 Create `.github/workflows/` directory at repository root

**Checkpoint**: Directory structure ready for workflow files

---

## Phase 3: User Story 1 - Automated Release on Version Tag (Priority: P1) 🎯 MVP

**Goal**: Pushing a `vX.X.X` tag triggers build, test, pack, publish to NuGet.org + GitHub Packages, and GitHub Release creation

**Independent Test**: Push a test tag and verify workflow triggers, builds, and produces artifacts

### Implementation for User Story 1

- [x] T008 [US1] Create release workflow in `.github/workflows/release.yml` with tag trigger `v*.*.*`
- [x] T009 [US1] Add version extraction step: strip `v` prefix from tag to get NuGet version
- [x] T010 [US1] Add .NET SDK setup step using `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` and `dotnet-quality: 'preview'`
- [x] T011 [US1] Add build step: `dotnet build -c Release`
- [x] T012 [US1] Add test step: `dotnet test --no-build -c Release`
- [x] T013 [US1] Add pack step: `dotnet pack -c Release -p:Version=$VERSION -o ./nupkg`
- [x] T014 [US1] Add NuGet.org publish step: `dotnet nuget push` with `NUGET_API_KEY` secret
- [x] T015 [US1] Add GitHub Packages publish step: `dotnet nuget push` to `https://nuget.pkg.github.com/jkolo/index.json` with `GITHUB_TOKEN`
- [x] T016 [US1] Add GitHub Release creation step using `softprops/action-gh-release` with `.nupkg` attached
- [x] T017 [US1] Add Matrix failure notification step (conditional: `if: failure()`) using Matrix webhook action with optional secrets `MATRIX_HOMESERVER`, `MATRIX_TOKEN`, `MATRIX_ROOM_ID`

**Checkpoint**: Release workflow file complete with all steps from tag trigger to GitHub Release

---

## Phase 4: User Story 2 - Build Validation on Every Push (Priority: P2)

**Goal**: Every push and PR triggers build + test to catch regressions

**Independent Test**: Push a commit and verify CI workflow runs and reports results

### Implementation for User Story 2

- [x] T018 [US2] Create CI workflow in `.github/workflows/ci.yml` with push and pull_request triggers
- [x] T019 [US2] Add .NET SDK setup step (same as release workflow)
- [x] T020 [US2] Add restore, build, and test steps
- [x] T021 [US2] Add Matrix failure notification step (conditional, same pattern as release workflow)

**Checkpoint**: CI workflow runs on push/PR and reports build + test status

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Validation and documentation

- [x] T022 Validate release workflow YAML syntax via `actionlint` or manual review
- [x] T023 Validate CI workflow YAML syntax via `actionlint` or manual review
- [x] T024 Run quickstart.md validation — verify all documented commands and secrets are correct

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Can run in parallel with Phase 1
- **US1 Release (Phase 3)**: Depends on Phase 1 (SDK pinning) + Phase 2 (directory)
- **US2 CI (Phase 4)**: Depends on Phase 2 (directory); independent of US1
- **Polish (Phase 5)**: Depends on US1 + US2 completion

### User Story Dependencies

- **US1 (Release)**: Requires Phase 1 + Phase 2 — primary deliverable
- **US2 (CI)**: Requires Phase 2 only — can start in parallel with US1

### Parallel Opportunities

- T003, T004, T005 modify different `.csproj` files — T004 and T005 can run in parallel
- T007 (directory) is independent of T001-T006 (package bumps)
- US1 and US2 can be developed in parallel after Phase 2
- T022 and T023 can run in parallel

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: SDK pinning + package bumps (T001–T006)
2. Complete Phase 2: Directory creation (T007)
3. Complete Phase 3: Release workflow (T008–T017)
4. **STOP and VALIDATE**: Push a test tag and verify release workflow
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → SDK pinned, packages bumped, directory ready
2. US1 (Release) → Tag-triggered release works → MVP complete
3. US2 (CI) → PR/push validation works → Full CI/CD
4. Polish → YAML validated, quickstart confirmed

---

## Notes

- Total tasks: 24
- Tasks per story: US1=10, US2=4, Setup=6, Foundational=1, Polish=3
- Key constraint: .NET 10 is preview — use `dotnet-quality: 'preview'` in setup-dotnet action
- Matrix notification is optional (gracefully skip if secrets not configured)
- Version is derived from git tag — `.csproj` must NOT contain hardcoded `<Version>`
