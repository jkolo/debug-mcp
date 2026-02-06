# Tasks: MCP Completions for Debugger Expressions

**Input**: Design documents from `/specs/020-mcp-completions/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/mcp-completions.md, quickstart.md

**Tests**: Included — Constitution Principle III (Test-First) is NON-NEGOTIABLE.

**Organization**: Tasks grouped by user story. US1+US2 are both P1 (combined in MVP phase). US3 is P2. US4 is P3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Exact file paths included in descriptions

---

## Phase 1: Setup

**Purpose**: Create directory structure and models for MCP Completions

- [ ] T001 Create `DebugMcp/Services/Completions/` directory and `tests/DebugMcp.Tests/Unit/Completions/` directory
- [ ] T002 Create `CompletionKind` enum in `DebugMcp/Services/Completions/CompletionKind.cs` per data-model.md
- [ ] T003 Create `CompletionContext` record in `DebugMcp/Services/Completions/CompletionContext.cs` per data-model.md
- [ ] T004 Add `Completions = new()` capability to MCP server options in `DebugMcp/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [ ] T005 [P] Write unit tests for `CompletionContextParser` in `tests/DebugMcp.Tests/Unit/Completions/CompletionContextParserTests.cs` — test: empty string → Variable kind, "cust" → Variable kind with prefix, "user." → Member kind, "user.Na" → Member kind with prefix, "DateTime." → StaticMember kind, "System." → Namespace kind
- [ ] T006 Implement `CompletionContextParser` in `DebugMcp/Services/Completions/CompletionContextParser.cs` — static `Parse(string expression)` method returning `CompletionContext` (makes T005 tests pass)
- [ ] T007 [P] Write unit tests for `ExpressionCompletionProvider` base behavior in `tests/DebugMcp.Tests/Unit/Completions/ExpressionCompletionProviderTests.cs` — test: returns empty when no session, returns empty when running (not paused), returns empty for unknown tool reference
- [ ] T008 Create `ExpressionCompletionProvider` class in `DebugMcp/Services/Completions/ExpressionCompletionProvider.cs` — inject `IDebugSessionManager`, `ILogger`; implement base `GetCompletionsAsync()` returning empty for no session/running states (makes T007 tests pass)
- [ ] T009 Register `ExpressionCompletionProvider` as singleton in DI in `DebugMcp/Program.cs`
- [ ] T010 Add `WithCompleteHandler()` in `DebugMcp/Program.cs` that delegates to `ExpressionCompletionProvider.GetCompletionsAsync()`

**Checkpoint**: Foundation ready — completion handler registered, returns empty appropriately

---

## Phase 3: User Story 1+2 — Variable + Object Member Completion (Priority: P1) MVP

**Goal**: LLM clients can request completions for variable names in scope and object members after a dot. This covers the two most critical use cases.

**Independent Test**: Attach to a paused process, request completions for "" (returns variables), request completions for "user." (returns members).

### Tests for US1+US2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T011 [P] [US1] Write unit tests for variable completion in `tests/DebugMcp.Tests/Unit/Completions/VariableCompletionTests.cs` — test: returns all scope variables for empty prefix, filters by prefix ("cust" → customer, customerId), includes this in instance methods, returns empty list when no variables
- [ ] T012 [P] [US2] Write unit tests for object member completion in `tests/DebugMcp.Tests/Unit/Completions/MemberCompletionTests.cs` — test: returns members for "user.", filters by prefix for "user.Na", includes both public and non-public members, returns empty if object evaluation fails

### Implementation for US1+US2

- [ ] T013 [US1] Implement variable completion in `ExpressionCompletionProvider.GetVariableCompletionsAsync()` in `DebugMcp/Services/Completions/ExpressionCompletionProvider.cs` — use `_sessionManager.GetVariables()`, extract names, filter by prefix (makes T011 tests pass)
- [ ] T014 [US2] Implement member completion in `ExpressionCompletionProvider.GetMemberCompletionsAsync()` in `DebugMcp/Services/Completions/ExpressionCompletionProvider.cs` — evaluate object expression, get type, enumerate members via metadata, filter by prefix (makes T012 tests pass)
- [ ] T015 [US1+US2] Wire context parsing to completion provider in `ExpressionCompletionProvider.GetCompletionsAsync()` — call parser, route Variable → `GetVariableCompletionsAsync()`, route Member → `GetMemberCompletionsAsync()`
- [ ] T016 [US1+US2] Add logging for completion requests (Debug level) and slow completions >100ms (Info level) in `ExpressionCompletionProvider`

**Checkpoint**: Variable and member completions functional. MVP complete.

---

## Phase 4: User Story 3 — Static Type Member Completion (Priority: P2)

**Goal**: LLM clients can request completions for static members of types like `DateTime.Now`, `Math.PI`.

**Independent Test**: Request completions for "DateTime." and verify static members like `Now`, `UtcNow`, `Today` are returned.

### Tests for US3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T017 [P] [US3] Write unit tests for static member completion in `tests/DebugMcp.Tests/Unit/Completions/StaticMemberCompletionTests.cs` — test: returns static members for "Math.", filters by prefix for "DateTime.N", returns empty if type not found in loaded modules

### Implementation for US3

- [ ] T018 [US3] Implement type resolution helper in `ExpressionCompletionProvider.ResolveTypeAsync()` in `DebugMcp/Services/Completions/ExpressionCompletionProvider.cs` — search loaded modules for type name (simple and qualified), return type info or null
- [ ] T019 [US3] Implement static member completion in `ExpressionCompletionProvider.GetStaticMemberCompletionsAsync()` in `DebugMcp/Services/Completions/ExpressionCompletionProvider.cs` — resolve type, enumerate static members only, filter by prefix (makes T017 tests pass)
- [ ] T020 [US3] Wire StaticMember kind to completion provider in `ExpressionCompletionProvider.GetCompletionsAsync()` — route to `GetStaticMemberCompletionsAsync()`

**Checkpoint**: Static type member completions functional.

---

## Phase 5: User Story 4 — Namespace-Qualified Type Completion (Priority: P3)

**Goal**: LLM clients can request completions for namespace prefixes to discover types (e.g., "System." → Collections, IO, String).

**Independent Test**: Request completions for "System." and verify child namespaces and types are returned.

### Tests for US4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T021 [P] [US4] Write unit tests for namespace completion in `tests/DebugMcp.Tests/Unit/Completions/NamespaceCompletionTests.cs` — test: returns child namespaces for "System.", returns types in namespace for "System.Collections.", filters by prefix, returns empty for unknown namespace

### Implementation for US4

- [ ] T022 [US4] Implement namespace enumeration helper in `ExpressionCompletionProvider.GetNamespaceContentsAsync()` in `DebugMcp/Services/Completions/ExpressionCompletionProvider.cs` — enumerate loaded modules, collect unique child namespaces and types for given prefix
- [ ] T023 [US4] Implement namespace completion in `ExpressionCompletionProvider.GetNamespaceCompletionsAsync()` in `DebugMcp/Services/Completions/ExpressionCompletionProvider.cs` — get namespace contents, filter by prefix (makes T021 tests pass)
- [ ] T024 [US4] Wire Namespace kind to completion provider in `ExpressionCompletionProvider.GetCompletionsAsync()` — route to `GetNamespaceCompletionsAsync()`

**Checkpoint**: Namespace completions functional. All user stories complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup

- [ ] T025 Verify all existing tests still pass (`dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"`)
- [ ] T026 Run full build and verify 0 warnings related to new code (`dotnet build`)
- [ ] T027 Verify completions work with `--no-roslyn` flag (completions should work regardless)
- [ ] T028 Run quickstart.md validation manually — test variable, member, static, namespace completions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1+US2 (Phase 3)**: Depends on Phase 2 completion
- **US3 (Phase 4)**: Depends on Phase 2 completion. Can run in parallel with Phase 3.
- **US4 (Phase 5)**: Depends on Phase 2 completion. Can run in parallel with Phases 3-4.
- **Polish (Phase 6)**: Depends on all user story phases being complete

### User Story Dependencies

- **US1+US2 (Variable + Member)**: Combined in Phase 3 as core MVP. No dependency on US3/US4.
- **US3 (Static)**: Independent of US1/US2. Only needs foundational parser.
- **US4 (Namespace)**: Independent of US1/US2/US3. Only needs foundational parser.

### Within Each Phase

- Tests MUST be written and FAIL before implementation (Constitution Principle III)
- Parser before provider methods
- Provider methods after tests
- Wiring after implementation
- Logging after core functionality works

### Parallel Opportunities

**Phase 2 (Foundational)**:
- T005 (parser tests) can run in parallel with T007 (provider base tests)
- After tests pass, T006 and T008 run sequentially (TDD Red→Green)

**Phase 3 (US1+US2)**:
- T011 (variable tests) can run in parallel with T012 (member tests)

**Cross-phase**:
- Phase 4 (US3) and Phase 5 (US4) can run in parallel with Phase 3 if foundation is complete

---

## Parallel Example: Phase 2 (Foundational)

```text
# Step 1: Test tasks run in parallel ([P] marker):
T005: tests/DebugMcp.Tests/Unit/Completions/CompletionContextParserTests.cs
T007: tests/DebugMcp.Tests/Unit/Completions/ExpressionCompletionProviderTests.cs

# Step 2: Implementation tasks (each makes its corresponding tests pass):
T006: CompletionContextParser (makes T005 pass)
T008: ExpressionCompletionProvider base (makes T007 pass)
```

## Parallel Example: Phase 3 (US1+US2 Tests)

```text
# All test files can be written in parallel:
T011: tests/DebugMcp.Tests/Unit/Completions/VariableCompletionTests.cs
T012: tests/DebugMcp.Tests/Unit/Completions/MemberCompletionTests.cs
```

---

## Implementation Strategy

### MVP First (US1+US2)

1. Complete Phase 1: Setup (T001-T004)
2. Complete Phase 2: Foundational (T005-T010)
3. Complete Phase 3: Variable + Member completions (T011-T016)
4. **STOP and VALIDATE**: Test completions work for "" and "user."
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1+US2 (Variable + Member) → Test → Deploy (MVP!)
3. Add US3 (Static) → Test → Deploy
4. Add US4 (Namespace) → Test → Deploy
5. Polish → Final validation → Release

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution Principle III (Test-First) is NON-NEGOTIABLE — tests before implementation
- All DateTimeOffset per project convention
- Commit after each task or logical group
- MCP SDK 0.7.0-preview.1 uses `WithCompleteHandler()` builder method
