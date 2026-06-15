---
description: "Task list for feature 034 — ReSharper Inspections"
---

# Tasks: ReSharper Inspections

**Input**: Design documents from `/specs/034-resharper-inspect/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — TDD is NON-NEGOTIABLE (Constitution III + project `tdd.md`). Every
implementation task is preceded by a failing test. Run build + tests between RED → GREEN →
REFACTOR.

**Organization**: Grouped by user story (US1 = P1 MVP, US2 = P2, US3 = P3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- File paths are absolute-from-repo-root (`DebugMcp/…`, `tests/…`).
- Fast test command: `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folders and the sample target used by fixtures + the opt-in integration test.

- [X] T001 Create source/test folders: `DebugMcp/Models/ReSharper/`, `DebugMcp/Services/ReSharper/`, `DebugMcp/Tools/` (exists), `tests/DebugMcp.Tests/Unit/ReSharper/`, `tests/DebugMcp.Tests/Fixtures/ReSharper/`.
- [X] T002 [P] Create sample target `tests/ReSharperSampleApp/` (`ReSharperSampleApp.sln` + `ReSharperSampleApp.csproj` + `Calculator.cs`) seeded with a **ReSharper-only** issue that the C# compiler does NOT flag (e.g. a `RedundantCast` `var x = (int)5;` and an unused private member). Add the project to the solution; ensure `dotnet build tests/ReSharperSampleApp` succeeds and `code_get_diagnostics` would report nothing for it.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Models, options, error codes, interfaces, and the opt-out DI scaffolding that every story builds on. None of these reference service implementations, so the project compiles before any story begins.

**⚠️ CRITICAL**: No user story work begins until this phase is complete.

- [X] T003 Add error-code constants to `DebugMcp/Models/ErrorResponse.cs` (`ErrorCodes`): `PrerequisiteMissing = "PREREQUISITE_MISSING"`, `EngineAcquisitionFailed = "ENGINE_ACQUISITION_FAILED"`, `InspectionFailed = "INSPECTION_FAILED"`, `BuildFailed = "BUILD_FAILED"`.
- [X] T004 [P] Create `DebugMcp/Models/ReSharper/ReSharperSeverity.cs` — enum `Error, Warning, Suggestion, Hint` with descending threshold order; serialized lower-case.
- [X] T005 [P] Create `DebugMcp/Models/ReSharper/InspectionFinding.cs` — record with `[JsonPropertyName]` fields per data-model.md (id, message, severity, category, file, line, column, end_line, end_column, project, help_link).
- [X] T006 [P] Create `DebugMcp/Models/ReSharper/InspectionResult.cs` — record (target, findings, total_count, returned_count, truncated, limited_to, summary, engine_version, duration_ms, built).
- [X] T007 [P] Create `DebugMcp/Models/ReSharper/EngineInstallState.cs` and `InspectionRunRequest` record (Target, Severity?, Project?, NoBuild) in `DebugMcp/Services/ReSharper/InspectionRunRequest.cs`.
- [X] T008 [P] [TEST] Write `tests/DebugMcp.Tests/Unit/ReSharper/ReSharperOptionsTests.cs` — assert CLI > env > default precedence for Enabled/CacheDirectory/Version/timeouts/MaxResults and `~` expansion. Run: tests FAIL (type missing) but build of the test project must reference the type → see T009 ordering. (RED)
- [X] T009 Create `DebugMcp/Services/ReSharper/ReSharperOptions.cs` — record + static `Create(...)` mirroring `SymbolServerOptions.Create` (defaults: Version `2026.1.2`, CacheDirectory `~/.debug-mcp/resharper`, AcquisitionTimeoutSeconds 600, InspectionTimeoutSeconds 300, MaxResults 500; env vars `DEBUG_MCP_NO_RESHARPER`/`_RESHARPER_CACHE`/`_RESHARPER_VERSION`/`_RESHARPER_ACQUIRE_TIMEOUT`/`_RESHARPER_INSPECT_TIMEOUT`/`_RESHARPER_MAX_RESULTS`). Make T008 GREEN.
- [X] T010 [P] Create interface files in `DebugMcp/Services/ReSharper/`: `IReSharperEngineProvider.cs`, `IReSharperRunner.cs`, `ISarifInspectionParser.cs`, `IReSharperInspectionService.cs` exactly per `contracts/inspection-service.md` (signatures only). Add typed exceptions `ReSharperPrerequisiteException`, `ReSharperAcquisitionException`, `ReSharperBuildFailedException`, `ReSharperRunFailedException`, `SarifParseException` in `DebugMcp/Services/ReSharper/ReSharperExceptions.cs`.
- [X] T011 Wire opt-out scaffolding in `DebugMcp/Program.cs` (no impl references yet, must compile): add `--no-resharper` option (+ optional `--resharper-cache`, `--resharper-version`) after the `--no-roslyn` block; build `ReSharperOptions` via `Create(...)`; `builder.Services.AddSingleton(resharperOptions)`; extend the tool-type filter with `.Where(t => resharperOptions.Enabled || !t.Name.StartsWith("ReSharper", StringComparison.Ordinal))`; add startup logging of enabled/disabled state (mirror the Roslyn log block). Add `using DebugMcp.Services.ReSharper;`.

**Checkpoint**: `dotnet build` clean; fast suite green (incl. T008/T009). No ReSharper tools exist yet.

---

## Phase 3: User Story 1 — Run ReSharper inspections on a solution (Priority: P1) 🎯 MVP

**Goal**: `resharper_inspect_solution` returns structured findings for a `.sln`; the engine is acquired lazily on first use and reused from cache afterwards.

**Independent Test**: Point the tool at `tests/ReSharperSampleApp/ReSharperSampleApp.sln`; receive a success envelope whose findings include the seeded ReSharper-only issue with native severity + correct file/line; a second call performs no re-download.

### Tests for User Story 1 (write first, MUST fail) ⚠️

- [X] T012 [P] [US1] Record SARIF fixture `tests/DebugMcp.Tests/Fixtures/ReSharper/sample-inspection.sarif` by running `jb inspectcode` once on `tests/ReSharperSampleApp` (capture a real document containing the seeded issue + at least one suggestion and one hint to prove severity granularity). Commit it.
- [X] T013 [P] [US1] Write `tests/DebugMcp.Tests/Unit/ReSharper/SarifInspectionParserTests.cs` — against the fixture: parses id/message/file/line; **native severity preserved (suggestion ≠ hint)**; finding without location yields null file/line; deterministic ordering (file→line→id); malformed JSON throws `SarifParseException`. (RED)
- [X] T014 [P] [US1] Write `tests/DebugMcp.Tests/Unit/ReSharper/ReSharperInspectionServiceTests.cs` happy-path cases with **faked** `IReSharperEngineProvider` (returns a ready `EngineInstallState`) + **faked** `IReSharperRunner` (returns the fixture SARIF): `InspectAsync` on a valid `.sln` returns findings, computes total/returned/summary/engine_version/built, caps at maxResults with `truncated=true` when exceeded, returns empty (not error) when no findings. (RED)

### Implementation for User Story 1

- [X] T015 [US1] Implement `DebugMcp/Services/ReSharper/SarifInspectionParser.cs` (pure) per research R5 (severity extraction with result-property → rule-config → level fallback). Make T013 GREEN.
- [X] T016 [P] [US1] Implement `DebugMcp/Services/ReSharper/ReSharperCliRunner.cs` — runs `jb inspectcode <target> --output=<unique tmp>.sarif --format=Sarif [--severity=<NATIVE>] [--project=<name>] [--no-build]` via `System.Diagnostics.Process`, reads+deletes the temp file in `finally`, throws `ReSharperBuildFailedException`/`ReSharperRunFailedException`, honours cancellation (kill process tree).
- [X] T017 [P] [US1] Implement `DebugMcp/Services/ReSharper/ReSharperEngineProvider.cs` — `EnsureEngineAsync`: check-then-install at `<cache>/<version>/` with `jb` shim + `.installed` sentinel, `SemaphoreSlim` + cross-process lock file, partial-install re-acquire, `dotnet tool install … --tool-path … --version …`, prerequisite probe (`dotnet --version`) → `ReSharperPrerequisiteException`, acquisition failures → `ReSharperAcquisitionException`, acquisition-timeout cancellation. Log acquiring/acquired/cache-hit.
- [X] T018 [US1] Implement `DebugMcp/Services/ReSharper/ReSharperInspectionService.cs` — orchestrate validate→EnsureEngine(acquisition timeout)→Run(inspection timeout, linked CTS)→Parse→sort→total_count→cap→summary→stamp; map exceptions to error codes (incl. `TIMEOUT` with `details.phase`). Make T014 GREEN. (depends on T015–T017)
- [X] T019 [US1] Register services in `DebugMcp/Program.cs` inside `if (resharperOptions.Enabled) { … }`: `IReSharperEngineProvider`/`ReSharperEngineProvider`, `IReSharperRunner`/`ReSharperCliRunner`, `ISarifInspectionParser`/`SarifInspectionParser`, `IReSharperInspectionService`/`ReSharperInspectionService`.
- [X] T020 [US1] Implement `DebugMcp/Tools/ReSharperInspectSolutionTool.cs` (`resharper_inspect_solution`, annotations ReadOnly=true/Destructive=false/Idempotent=true/OpenWorld=true) per `contracts/resharper_inspect_solution.md`: validate `.sln`, parse params (severity/project/noBuild/timeoutSeconds/maxResults bounds), call `InspectAsync`, serialize `{success,data}`/`{success,error}`, log ToolInvoked/Completed/Error.
- [X] T021 [US1] Update `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs`: add `["resharper_inspect_solution"]` entry; bump the count assertion 37 → 38 (rename `ExpectedAnnotations_Covers37Tools` → `…Covers38Tools`). Run fast suite green.

**Checkpoint**: MVP — solution inspection works end-to-end with lazy cached acquisition.

---

## Phase 4: User Story 2 — Scope and filter the inspection (Priority: P2)

**Goal**: Add the project-scoped tool and prove severity filtering + project scoping reduce results; `--no-build` skips the build.

**Independent Test**: Same solution, run with `severity=warning` and `project=<name>` → strict subset, all ≥ warning, all in that project; `resharper_inspect_project` on the `.csproj` returns its subset; unknown project → `PROJECT_NOT_FOUND`.

### Tests for User Story 2 (write first, MUST fail) ⚠️

- [X] T022 [P] [US2] Add to `ReSharperInspectionServiceTests.cs`: severity threshold reduces results (suggestion/hint dropped at `warning`); `project` scope passes through to the run request; solution scope naming an unknown project → `PROJECT_NOT_FOUND`; `noBuild=true` sets `built=false` and passes `--no-build`. (RED)
- [X] T023 [P] [US2] Write `tests/DebugMcp.Tests/Unit/ReSharper/ReSharperInspectProjectToolReturn` cases (in service tests or a tool-focused test) for `.csproj` target validation + `INVALID_PATH` on non-`.csproj`. (RED)

### Implementation for User Story 2

- [X] T024 [US2] Extend `ReSharperInspectionService` to validate `project` against the solution's project set (→ `PROJECT_NOT_FOUND`) and thread `noBuild`/`severity` into `InspectionRunRequest`. Make T022 GREEN.
- [X] T025 [US2] Implement `DebugMcp/Tools/ReSharperInspectProjectTool.cs` (`resharper_inspect_project`, same annotations) per `contracts/resharper_inspect_project.md`: validate `.csproj`, params (no `project` scope param), call `InspectAsync(project: null)`. Make T023 GREEN.
- [X] T026 [US2] Update `ToolAnnotationTests.cs`: add `["resharper_inspect_project"]`; bump count 38 → 39 (rename to `…Covers39Tools`). Run fast suite green.

**Checkpoint**: Both granular tools work; filtering/scoping verified.

---

## Phase 5: User Story 3 — Robust failure behaviour & opt-out (Priority: P3)

**Goal**: Every failure mode returns a distinct, actionable error without crashing the server; `--no-resharper` removes the tools.

**Independent Test**: Simulate each failure (bad target, unreachable acquisition, missing dotnet, build failure, timeout) → distinct codes; with `--no-resharper`, no `resharper_*` tools advertised and other tools unaffected.

### Tests for User Story 3 (write first, MUST fail) ⚠️

- [X] T027 [P] [US3] Add to `ReSharperInspectionServiceTests.cs` (faked seams throwing): `ReSharperPrerequisiteException` → `PREREQUISITE_MISSING`; `ReSharperAcquisitionException` → `ENGINE_ACQUISITION_FAILED` (remediation in details); `ReSharperBuildFailedException` → `BUILD_FAILED`; `ReSharperRunFailedException`/`SarifParseException` → `INSPECTION_FAILED`; acquisition cancellation → `TIMEOUT` `phase=acquisition`; inspection cancellation → `TIMEOUT` `phase=inspection`. (RED)
- [X] T028 [P] [US3] Write `tests/DebugMcp.Tests/Contract/ReSharperOptOutTests.cs` — building the tool-type list with `ReSharperOptions{Enabled=false}` yields no `ReSharper*` tools, while `Code*` and others remain (mirror the registration filter expression). (RED)
- [X] T029 [P] [US3] Write opt-in `tests/DebugMcp.Tests/Integration/ReSharperInspectionIntegrationTests.cs` — real `jb inspectcode` on `tests/ReSharperSampleApp`: asserts the seeded ReSharper-only issue is found AND `code_get_diagnostics` does not report it (SC-002). Tier with existing Integration tests (skipped in dev). (RED until engine present)

### Implementation for User Story 3

- [X] T030 [US3] Finalize error mapping/messages + `details` (phase, remediation, offending path, build-error tail) in `ReSharperInspectionService`, `ReSharperEngineProvider`, `ReSharperCliRunner`. Make T027 GREEN.
- [X] T031 [US3] Confirm/adjust the opt-out filter + startup log in `Program.cs` so T028 is GREEN (no behavior change expected if T011 correct).

**Checkpoint**: All failure modes + opt-out verified; full unit/contract suite green.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T032 [P] Update `README.md` and `ROADMAP.md` — ReSharper inspection tools (39 tools total), `--no-resharper`, lazy auto-install + cache, native severities.
- [X] T033 [P] Add docs page `website/docs/tools/resharper.md` and sidebar entry in `website/sidebars.ts`; note the engine auto-install + the two tools. (Coordinate with the pending 033 website branch.)
- [X] T034 [P] Update `CLAUDE.md` "Active Technologies" + "Recent Changes" with feature 034 (ReSharper engine acquisition, 2 tools, 37→39).
- [X] T035 Run `quickstart.md` validation end-to-end (cached-engine happy path, SC-002, filtering, failures, `--no-resharper`); confirm `dotnet build` 0 warnings.
- [X] T036 Save engram (`save_engram`) recording the implemented design + any SARIF severity-extraction gotcha discovered in T012/T015.

---

## Dependencies & Execution Order

### Phase dependencies
- Setup (P1) → Foundational (P2) → US1 (P3) → US2 (P4) → US3 (P5) → Polish (P6).
- Foundational BLOCKS all stories. US2 depends on the pipeline from US1 (shared service/runner). US3 depends on US1 (error mapping lives in the US1 service) and US2 (project-scope error).

### Within each story
- Tests written FIRST and FAIL before implementation (RED → GREEN → REFACTOR); build + tests between phases.
- Models → services → tools.

### Parallel opportunities
- Setup: T002 ∥ folder creation.
- Foundational: T004–T007 ∥ (distinct model files); T010 after models.
- US1: T012/T013/T014 (tests, distinct files) ∥; T016/T017 ∥ (runner vs provider, distinct files) after parser interface; T015 before T018; T018 after T015–T017.
- US2: T022/T023 ∥. US3: T027/T028/T029 ∥.
- Polish: T032/T033/T034 ∥.

---

## Parallel Example: User Story 1

```bash
# Tests first (distinct files) — write together, all must fail:
Task: "SarifInspectionParserTests.cs (T013)"
Task: "ReSharperInspectionServiceTests.cs happy path (T014)"
# Then implementations that don't share a file:
Task: "ReSharperCliRunner.cs (T016)"
Task: "ReSharperEngineProvider.cs (T017)"
```

---

## Implementation Strategy

### MVP first (US1 only)
1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & validate** the
   solution happy path (acquire+cache+findings) → demoable MVP.

### Incremental delivery
US1 (MVP) → US2 (project tool + filtering) → US3 (robustness + opt-out + integration) →
Polish. Each story keeps the fast unit/contract suite green and the server fully functional.

---

## Notes
- 37 → 39 tools total (US1 +1 → 38, US2 +1 → 39); the annotation count assertion is bumped in
  the same phase that adds each tool to keep the suite green.
- The 180 MB engine is never downloaded by the fast suite — only the opt-in T029 integration
  test (and manual quickstart) touch the real engine.
- Do not run the flaky/integration debugger tests as part of this work (see CLAUDE.md).
- Commit after each task or logical RED/GREEN/REFACTOR group (Conventional Commits).
