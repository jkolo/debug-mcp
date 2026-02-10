# Tasks: MCP Tool Annotations & Best Practices

**Input**: Design documents from `/specs/024-mcp-best-practices/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, quickstart.md

**Tests**: Included — the feature specification requires annotation verification tests (FR-010–FR-012) and the constitution mandates test-first (Principle III).

**Organization**: Tasks grouped by user story. US1+US2 (P1) are already complete. Remaining work: US4 (annotation tests) and US3 (JSON response examples).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Exact file paths included in all descriptions

## Path Conventions

- **Tool source files**: `DebugMcp/Tools/*.cs`
- **Test files**: `tests/DebugMcp.Tests/Contract/*.cs`
- **Spec files**: `specs/024-mcp-best-practices/*`

---

## Phase 1: US1 + US2 — Tool Annotations & Titles (Priority: P1) — COMPLETE

**Goal**: All 34 tools have Title, ReadOnly, Destructive, Idempotent, OpenWorld annotations on `[McpServerTool]`

**Independent Test**: List tools via MCP client → each tool has correct annotation values matching the classification table

> All 34 tool files already annotated. Build passes (0 errors), 895 tests pass. No tasks remaining.

- [x] T001 [US1] Add ReadOnly, Destructive, Idempotent, OpenWorld to all 34 tools in `DebugMcp/Tools/*.cs`
- [x] T002 [US2] Add Title to all 34 tools in `DebugMcp/Tools/*.cs`
- [x] T003 [US1] Verify build passes with `dotnet build`
- [x] T004 [US1] Verify all 895 existing tests pass with `dotnet test`

**Checkpoint**: US1 + US2 complete and verified.

---

## Phase 2: US4 — Annotation Verification Tests (Priority: P2)

**Goal**: Automated tests verify every tool's annotations match the spec classification table, catch regressions, and detect missing tool coverage.

**Independent Test**: Run `dotnet test --filter "FullyQualifiedName~ToolAnnotation"` → all annotation tests pass, coverage check confirms 34/34 tools tested.

### Tests for US4

> Tests are the implementation for this story — no separate implementation phase needed.

- [x] T005 [US4] Create `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs` with expected annotations dictionary (34 entries mapping tool name → Title, ReadOnly, Destructive, Idempotent, OpenWorld from spec classification table)
- [x] T006 [US4] Add tool discovery helper method in `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs` — reflection on assembly to find all types with `[McpServerToolType]` and methods with `[McpServerTool]`
- [x] T007 [US4] Add per-tool annotation assertion test (`[Theory]` with `[MemberData]`) in `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs` — asserts Title, ReadOnly, Destructive, Idempotent, OpenWorld match expected values with clear error messages (FR-012)
- [x] T008 [US4] Add coverage check test (`[Fact]`) in `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs` — discovers all registered tools and asserts each has an entry in the expected annotations dictionary (FR-011)
- [x] T009 [US4] Add description content tests (`[Theory]`) in `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs` — for the 10 enhanced tools, assert description contains a JSON response example pattern (these tests will FAIL until US3 is complete — correct per test-first)
- [x] T010 [US4] Run annotation tests with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ToolAnnotation"` — verify annotation assertions pass (34/34), coverage check passes, description tests fail as expected

**Checkpoint**: US4 complete. All 192 annotation tests pass (34 per-tool × 5 properties + 2 coverage checks + 20 description tests).

---

## Phase 3: US3 — Enhanced Descriptions with Response Examples (Priority: P2)

**Goal**: 10 key tools have JSON response examples embedded in `[Description]` text so AI clients can see the response shape when listing tools.

**Independent Test**: Read description of any enhanced tool → it contains field documentation, preconditions, and a JSON response example with field names and representative values.

### Implementation for US3

- [x] T011 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/DebugLaunchTool.cs` — session object with processId, processName, state, runtimeVersion
- [x] T012 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/BreakpointSetTool.cs` — breakpoint object with id, location, state, enabled, hitCount
- [x] T013 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/BreakpointWaitTool.cs` — hit result with breakpointId, threadId, location, hitCount
- [x] T014 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/DebugContinueTool.cs` — session state update with state field
- [x] T015 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/DebugStepTool.cs` — session state with source location after step
- [x] T016 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/VariablesGetTool.cs` — array of variable objects with name, type, value, scope
- [x] T017 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/EvaluateTool.cs` — evaluation result with value, type, has_children
- [x] T018 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/StacktraceGetTool.cs` — array of frames with index, function, module, source
- [x] T019 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/ExceptionGetContextTool.cs` — exception autopsy with type, message, innerExceptions, stackFrames
- [x] T020 [P] [US3] Add JSON response example to `[Description]` in `DebugMcp/Tools/DebugDisconnectTool.cs` — disconnect status with previousSession info
- [x] T021 [US3] Run description content tests — should now pass with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ToolAnnotation"`

**Checkpoint**: US3 complete. All 192 annotation + description tests pass. All 10 enhanced tools have JSON response examples.

---

## Phase 4: Polish & Final Verification

**Purpose**: Verify everything works together, no regressions.

- [x] T022 Run full build with `dotnet build` — verify 0 errors, 0 warnings
- [x] T023 Run full test suite with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — verify all tests pass (1087 total: 895 existing + 192 new annotation tests)
- [x] T024 Run quickstart.md validation steps from `specs/024-mcp-best-practices/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (US1+US2)**: COMPLETE — no action needed
- **Phase 2 (US4)**: No dependencies — can start immediately
- **Phase 3 (US3)**: No dependencies on Phase 2 — can run in parallel, but description tests from Phase 2 will only pass after Phase 3 completes
- **Phase 4 (Polish)**: Depends on Phase 2 and Phase 3 completion

### User Story Dependencies

- **US1 (P1)**: COMPLETE
- **US2 (P1)**: COMPLETE
- **US4 (P2)**: Independent — tests verify existing annotations
- **US3 (P2)**: Independent — description changes are purely additive

### Within Each Phase

- Phase 2: T005 → T006 → T007, T008, T009 (parallel) → T010
- Phase 3: T011–T020 (all parallel — different files) → T021
- Phase 4: T022 → T023 → T024

### Parallel Opportunities

- All 10 description tasks (T011–T020) can run in parallel — each modifies a different tool file
- Annotation tests (T007) and coverage test (T008) can be written in the same file simultaneously
- Phase 2 (US4) and Phase 3 (US3) can run in parallel — different files entirely

---

## Parallel Example: US3 Description Tasks

```bash
# All 10 description tasks can run in parallel (different files):
T011: DebugLaunchTool.cs
T012: BreakpointSetTool.cs
T013: BreakpointWaitTool.cs
T014: DebugContinueTool.cs
T015: DebugStepTool.cs
T016: VariablesGetTool.cs
T017: EvaluateTool.cs
T018: StacktraceGetTool.cs
T019: ExceptionGetContextTool.cs
T020: DebugDisconnectTool.cs
```

---

## Implementation Strategy

### MVP First (US1 + US2 — Already Done)

1. ~~Complete US1: Add annotations to all 34 tools~~ DONE
2. ~~Complete US2: Add titles to all 34 tools~~ DONE
3. ~~Verify build + tests~~ DONE (0 errors, 895 tests pass)

### Incremental Delivery

1. ~~US1 + US2 (P1) → Annotations + Titles~~ DONE
2. US4 (P2) → Annotation verification tests → Run tests
3. US3 (P2) → JSON response examples → Run description tests
4. Polish → Final build + full test suite

### Single-Developer Strategy

Since US4 and US3 touch different files, the optimal order is:
1. Write all tests (T005–T009) — single file, captures all expectations
2. Run tests (T010) — annotations pass, descriptions fail (TDD red phase)
3. Add all JSON examples (T011–T020) — 10 parallel edits
4. Run all tests (T021–T023) — everything passes (TDD green phase)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story
- T001–T004 already complete (Phase 1)
- T009 description tests are expected to FAIL until T011–T020 are done (test-first for US3)
- JSON examples are concise single-line format: `Example response: {"success": true, ...}`
- Commit after each phase checkpoint
