# Tasks: Roslyn Code Analysis

**Input**: Design documents from `/specs/015-roslyn-code-analysis/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included per constitution (III. Test-First)

**Organization**: Tasks grouped by user story. US4 (Load) is foundational - all other stories depend on it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1-US5 maps to User Stories from spec.md
- Exact file paths included

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add Roslyn packages and create directory structure

- [x] T001 Add NuGet packages to DebugMcp/DebugMcp.csproj: Microsoft.CodeAnalysis.CSharp.Workspaces 5.0.0, Microsoft.CodeAnalysis.Workspaces.MSBuild 5.0.0, Microsoft.Build.Locator 1.7.8
- [x] T002 [P] Add Microsoft.Build packages with ExcludeAssets="runtime" to DebugMcp/DebugMcp.csproj
- [x] T003 [P] Create directory DebugMcp/Models/CodeAnalysis/
- [x] T004 [P] Create directory DebugMcp/Services/CodeAnalysis/
- [x] T005 [P] Create directory tests/DebugMcp.Tests/Unit/CodeAnalysis/
- [x] T006 [P] Create directory tests/DebugMcp.E2E/Features/CodeAnalysis/

---

## Phase 2: Foundational - User Story 4: Load Solution/Project (Priority: P1) 🎯 MVP

**Goal**: Enable loading .sln/.csproj files into Roslyn workspace for analysis

**Independent Test**: Load TestTargetApp.sln and verify project count and document info returned

**⚠️ CRITICAL**: All other stories depend on this - no other story work until Phase 2 complete

### Models for US4

- [x] T007 [P] [US4] Create WorkspaceType enum in DebugMcp/Models/CodeAnalysis/WorkspaceType.cs
- [x] T008 [P] [US4] Create ProjectInfo record in DebugMcp/Models/CodeAnalysis/ProjectInfo.cs
- [x] T009 [P] [US4] Create WorkspaceDiagnostic record in DebugMcp/Models/CodeAnalysis/WorkspaceDiagnostic.cs
- [x] T010 [P] [US4] Create WorkspaceInfo record in DebugMcp/Models/CodeAnalysis/WorkspaceInfo.cs

### Error Codes for US4

- [x] T011 [US4] Add error codes NO_WORKSPACE, INVALID_PATH, LOAD_FAILED to DebugMcp/Models/ErrorResponse.cs

### Service Interface

- [x] T012 [US4] Create ICodeAnalysisService interface in DebugMcp/Services/CodeAnalysis/ICodeAnalysisService.cs with LoadAsync method

### Tests for US4

- [x] T013 [US4] Write unit test CodeAnalysisServiceTests.LoadSolution_ValidPath_ReturnsWorkspaceInfo in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T014 [P] [US4] Write unit test CodeAnalysisServiceTests.LoadSolution_InvalidPath_ReturnsError in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T015 [P] [US4] Write unit test CodeAnalysisServiceTests.LoadProject_ValidPath_ReturnsWorkspaceInfo in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T016 [P] [US4] Write E2E test in tests/DebugMcp.E2E/Features/CodeAnalysis/CodeLoad.feature: Scenario "Load valid solution"

### Implementation for US4

- [x] T017 [US4] Implement CodeAnalysisService with MSBuildLocator static initialization in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T018 [US4] Implement LoadAsync method with MSBuildWorkspace.Create() and OpenSolutionAsync/OpenProjectAsync in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T019 [US4] Implement WorkspaceFailed event handler for error collection in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T020 [US4] Implement IProgress<ProjectLoadProgress> for MCP progress notifications in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T021 [US4] Create CodeLoadTool with [McpServerTool(Name = "code_load")] in DebugMcp/Tools/CodeLoadTool.cs
- [x] T022 [US4] Register ICodeAnalysisService as singleton in DebugMcp/Program.cs

**Checkpoint**: code_load tool functional. Can load solutions and projects. All subsequent stories can now begin.

---

## Phase 3: User Story 1 - Find All Usages of a Symbol (Priority: P1)

**Goal**: Find all locations where a symbol is referenced across the solution

**Independent Test**: Load TestTargetApp.sln, find usages of a known property, verify correct locations returned

### Models for US1

- [x] T023 [P] [US1] Create UsageKind enum in DebugMcp/Models/CodeAnalysis/UsageKind.cs
- [x] T024 [P] [US1] Create SymbolUsage record in DebugMcp/Models/CodeAnalysis/SymbolUsage.cs

### Error Codes for US1

- [x] T025 [US1] Add error codes SYMBOL_NOT_FOUND, INVALID_LOCATION to DebugMcp/Models/ErrorResponse.cs

### Service Methods for US1

- [x] T026 [US1] Add GetSymbolAtLocationAsync method to ICodeAnalysisService in DebugMcp/Services/CodeAnalysis/ICodeAnalysisService.cs
- [x] T027 [US1] Add FindSymbolByNameAsync method to ICodeAnalysisService in DebugMcp/Services/CodeAnalysis/ICodeAnalysisService.cs
- [x] T028 [US1] Add FindUsagesAsync method to ICodeAnalysisService in DebugMcp/Services/CodeAnalysis/ICodeAnalysisService.cs

### Tests for US1

- [x] T029 [US1] Write unit test FindUsages_ByName_ReturnsAllLocations in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T030 [P] [US1] Write unit test FindUsages_ByLocation_ReturnsAllLocations in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T031 [P] [US1] Write unit test FindUsages_NoUsages_ReturnsEmptyList in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T032 [P] [US1] Write unit test FindUsages_InvalidSymbol_ReturnsError in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T033 [P] [US1] Write E2E test in tests/DebugMcp.E2E/Features/CodeAnalysis/CodeFindUsages.feature: Scenario "Find property usages"

### Implementation for US1

- [x] T034 [US1] Implement GetSymbolAtLocationAsync using SemanticModel.GetSymbolInfo in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T035 [US1] Implement FindSymbolByNameAsync using Compilation.GetTypeByMetadataName in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T036 [US1] Implement FindUsagesAsync using SymbolFinder.FindReferencesAsync in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T037 [US1] Create CodeFindUsagesTool with [McpServerTool(Name = "code_find_usages")] in DebugMcp/Tools/CodeFindUsagesTool.cs
- [x] T038 [US1] Add tool logging with ToolInvoked/ToolCompleted in DebugMcp/Tools/CodeFindUsagesTool.cs

**Checkpoint**: code_find_usages tool functional. Can find symbol references by name or location.

---

## Phase 4: User Story 2 - Find Where Variable Is Assigned (Priority: P1)

**Goal**: Find all locations where a variable/field/property is assigned a value

**Independent Test**: Load TestTargetApp.sln, find assignments to a known field, verify all write operations returned

### Models for US2

- [x] T039 [P] [US2] Create AssignmentKind enum in DebugMcp/Models/CodeAnalysis/AssignmentKind.cs
- [x] T040 [P] [US2] Create SymbolAssignment record in DebugMcp/Models/CodeAnalysis/SymbolAssignment.cs

### Service Methods for US2

- [x] T041 [US2] Add FindAssignmentsAsync method to ICodeAnalysisService in DebugMcp/Services/CodeAnalysis/ICodeAnalysisService.cs

### Tests for US2

- [x] T042 [US2] Write unit test FindAssignments_SimpleAssignment_ReturnsLocation in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T043 [P] [US2] Write unit test FindAssignments_CompoundAssignment_ReturnsLocation in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T044 [P] [US2] Write unit test FindAssignments_IncrementDecrement_ReturnsLocation in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T045 [P] [US2] Write unit test FindAssignments_OutRefParam_ReturnsLocation in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T046 [P] [US2] Write E2E test in tests/DebugMcp.E2E/Features/CodeAnalysis/CodeFindAssignments.feature: Scenario "Find field assignments"

### Implementation for US2

- [x] T047 [US2] Create AssignmentWalker CSharpSyntaxWalker class in DebugMcp/Services/CodeAnalysis/AssignmentWalker.cs
- [x] T048 [US2] Implement VisitAssignmentExpression in AssignmentWalker for =, +=, -=, etc. in DebugMcp/Services/CodeAnalysis/AssignmentWalker.cs
- [x] T049 [US2] Implement VisitPrefixUnaryExpression and VisitPostfixUnaryExpression in AssignmentWalker for ++/-- in DebugMcp/Services/CodeAnalysis/AssignmentWalker.cs
- [x] T050 [US2] Implement VisitArgument in AssignmentWalker for out/ref parameters in DebugMcp/Services/CodeAnalysis/AssignmentWalker.cs
- [x] T051 [US2] Implement FindAssignmentsAsync using AssignmentWalker in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T052 [US2] Create CodeFindAssignmentsTool with [McpServerTool(Name = "code_find_assignments")] in DebugMcp/Tools/CodeFindAssignmentsTool.cs

**Checkpoint**: code_find_assignments tool functional. Can find all write operations to a symbol.

---

## Phase 5: User Story 3 - Get Compilation Diagnostics (Priority: P2)

**Goal**: Retrieve compilation errors and warnings for a project

**Independent Test**: Load a project with known errors, verify all diagnostics returned with correct severity

### Models for US3

- [x] T053 [P] [US3] Create DiagnosticSeverity enum in DebugMcp/Models/CodeAnalysis/DiagnosticSeverity.cs
- [x] T054 [P] [US3] Create DiagnosticInfo record in DebugMcp/Models/CodeAnalysis/DiagnosticInfo.cs

### Error Codes for US3

- [x] T055 [US3] Add error code PROJECT_NOT_FOUND to DebugMcp/Models/ErrorResponse.cs

### Service Methods for US3

- [x] T056 [US3] Add GetDiagnosticsAsync method to ICodeAnalysisService in DebugMcp/Services/CodeAnalysis/ICodeAnalysisService.cs

### Tests for US3

- [x] T057 [US3] Write unit test GetDiagnostics_ProjectWithErrors_ReturnsErrors in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T058 [P] [US3] Write unit test GetDiagnostics_ProjectWithWarnings_ReturnsWarnings in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T059 [P] [US3] Write unit test GetDiagnostics_CleanProject_ReturnsEmpty in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T060 [P] [US3] Write unit test GetDiagnostics_InvalidProject_ReturnsError in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T061 [P] [US3] Write E2E test in tests/DebugMcp.E2E/Features/CodeAnalysis/CodeGetDiagnostics.feature: Scenario "Get project diagnostics"

### Implementation for US3

- [x] T062 [US3] Implement GetDiagnosticsAsync using Compilation.GetDiagnostics in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T063 [US3] Add severity filtering and result limiting to GetDiagnosticsAsync in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T064 [US3] Create CodeGetDiagnosticsTool with [McpServerTool(Name = "code_get_diagnostics")] in DebugMcp/Tools/CodeGetDiagnosticsTool.cs

**Checkpoint**: code_get_diagnostics tool functional. Can retrieve compilation diagnostics.

---

## Phase 6: User Story 5 - Navigate to Symbol Definition (Priority: P2)

**Goal**: Find where a symbol is defined (go-to-definition)

**Independent Test**: Load TestTargetApp.sln, navigate to definition of a method call, verify correct source location returned

### Models for US5

- [x] T065 [P] [US5] Create SymbolKind enum in DebugMcp/Models/CodeAnalysis/SymbolKind.cs
- [x] T066 [P] [US5] Create SymbolDefinition record in DebugMcp/Models/CodeAnalysis/SymbolDefinition.cs

### Service Methods for US5

- [x] T067 [US5] Add GoToDefinitionAsync method to ICodeAnalysisService in DebugMcp/Services/CodeAnalysis/ICodeAnalysisService.cs

### Tests for US5

- [x] T068 [US5] Write unit test GoToDefinition_SourceSymbol_ReturnsLocation in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T069 [P] [US5] Write unit test GoToDefinition_PartialClass_ReturnsAllLocations in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T070 [P] [US5] Write unit test GoToDefinition_MetadataSymbol_ReturnsAssemblyInfo in tests/DebugMcp.Tests/Unit/CodeAnalysis/CodeAnalysisServiceTests.cs
- [x] T071 [P] [US5] Write E2E test in tests/DebugMcp.E2E/Features/CodeAnalysis/CodeGoToDefinition.feature: Scenario "Go to method definition"

### Implementation for US5

- [x] T072 [US5] Implement GoToDefinitionAsync using DeclaringSyntaxReferences in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T073 [US5] Handle metadata symbols (no source) returning assembly info in DebugMcp/Services/CodeAnalysis/CodeAnalysisService.cs
- [x] T074 [US5] Create CodeGoToDefinitionTool with [McpServerTool(Name = "code_goto_definition")] in DebugMcp/Tools/CodeGoToDefinitionTool.cs

**Checkpoint**: code_goto_definition tool functional. Can navigate to symbol definitions.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, cleanup, and final validation

- [x] T075 [P] Update website/docs/tools/ with code analysis tool documentation
- [ ] T076 [P] Add asciinema recording demonstrating code analysis workflow (deferred - requires interactive session)
- [x] T077 Run all E2E tests and verify acceptance scenarios from spec.md
- [x] T078 Verify quickstart.md examples work correctly
- [x] T079 Performance validation: verify SC-001 (<2s symbol search) and SC-002 (<30s solution load)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
     ↓
Phase 2 (US4: Load) ← BLOCKS ALL OTHER STORIES
     ↓
┌────┴────┬────────┬────────┐
↓         ↓        ↓        ↓
Phase 3   Phase 4  Phase 5  Phase 6
(US1)     (US2)    (US3)    (US5)
     ↓         ↓        ↓        ↓
     └────┬────┴────────┴────────┘
          ↓
      Phase 7 (Polish)
```

### User Story Dependencies

| Story | Depends On | Can Parallel With |
|-------|------------|-------------------|
| US4 (Load) | Setup only | None (foundational) |
| US1 (Find Usages) | US4 complete | US2, US3, US5 |
| US2 (Find Assignments) | US4 complete | US1, US3, US5 |
| US3 (Diagnostics) | US4 complete | US1, US2, US5 |
| US5 (Go to Definition) | US4 complete | US1, US2, US3 |

### Within Each User Story

1. Models (can be parallel)
2. Error codes
3. Service interface methods
4. Tests (write first, must fail)
5. Implementation (make tests pass)
6. Tool class
7. Checkpoint validation

---

## Parallel Example: After Phase 2 Complete

```bash
# Developer A: User Story 1
Task: "Create UsageKind enum in DebugMcp/Models/CodeAnalysis/UsageKind.cs"
Task: "Create SymbolUsage record in DebugMcp/Models/CodeAnalysis/SymbolUsage.cs"

# Developer B: User Story 2
Task: "Create AssignmentKind enum in DebugMcp/Models/CodeAnalysis/AssignmentKind.cs"
Task: "Create SymbolAssignment record in DebugMcp/Models/CodeAnalysis/SymbolAssignment.cs"

# Developer C: User Story 3
Task: "Create DiagnosticSeverity enum in DebugMcp/Models/CodeAnalysis/DiagnosticSeverity.cs"
Task: "Create DiagnosticInfo record in DebugMcp/Models/CodeAnalysis/DiagnosticInfo.cs"
```

---

## Implementation Strategy

### MVP First (US4 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: US4 (Load Solution)
3. **STOP and VALIDATE**: Can load real solutions, progress notifications work
4. Deploy/demo MVP

### Recommended Order (Single Developer)

1. Setup → US4 (Load) → **MVP checkpoint**
2. US1 (Find Usages) → **checkpoint** (most fundamental navigation)
3. US2 (Find Assignments) → **checkpoint** (completes data flow analysis)
4. US3 (Diagnostics) → **checkpoint**
5. US5 (Go to Definition) → **checkpoint**
6. Polish phase

### Parallel Team Strategy

1. Team completes Setup + US4 together (foundation)
2. Once US4 complete:
   - Dev A: US1 (Find Usages)
   - Dev B: US2 (Find Assignments)
   - Dev C: US3 + US5 (Diagnostics + Definitions)
3. Polish phase together

---

## Notes

- All [P] tasks can run in parallel (different files)
- Constitution requires Test-First: write tests, verify they fail, then implement
- Commit after each task or logical group
- Stop at any checkpoint to validate independently
- US4 (Load) is labeled as foundational since all tools require a loaded workspace
