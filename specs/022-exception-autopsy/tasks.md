# Tasks: Exception Autopsy

**Input**: Design documents from `/specs/022-exception-autopsy/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included (constitution mandates Test-First / TDD).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup

**Purpose**: Register service in DI and verify build

- [x] T001 Register `IExceptionAutopsyService` / `ExceptionAutopsyService` in DI container in `DebugMcp/Program.cs` — add `services.AddSingleton<IExceptionAutopsyService, ExceptionAutopsyService>()` alongside existing service registrations

---

## Phase 2: Foundational (Models & Service Interface)

**Purpose**: Define all data model records and the service interface. These are shared across all user stories and MUST be complete before any story begins.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T002 [P] Create `ExceptionDetail` record in `DebugMcp/Models/Inspection/ExceptionDetail.cs` — fields: Type (string), Message (string), IsFirstChance (bool), StackTraceString (string?). Per data-model.md.
- [x] T003 [P] Create `InnerExceptionEntry` record in `DebugMcp/Models/Inspection/InnerExceptionEntry.cs` — fields: Type (string), Message (string), Depth (int). Per data-model.md.
- [x] T004 [P] Create `VariableError` record in `DebugMcp/Models/Inspection/VariableError.cs` — fields: Name (string), Error (string). Per data-model.md.
- [x] T005 [P] Create `FrameVariables` record in `DebugMcp/Models/Inspection/FrameVariables.cs` — fields: Locals (IReadOnlyList\<Variable\>), Errors (IReadOnlyList\<VariableError\>?). Per data-model.md.
- [x] T006 [P] Create `AutopsyFrame` record in `DebugMcp/Models/Inspection/AutopsyFrame.cs` — fields: Index (int), Function (string), Module (string), IsExternal (bool), Location (SourceLocation?), Arguments (IReadOnlyList\<Variable\>?), Variables (FrameVariables?). Per data-model.md.
- [x] T007 Create `ExceptionAutopsyResult` record in `DebugMcp/Models/Inspection/ExceptionAutopsyResult.cs` — fields: ThreadId (int), Exception (ExceptionDetail), InnerExceptions (IReadOnlyList\<InnerExceptionEntry\>), InnerExceptionsTruncated (bool), Frames (IReadOnlyList\<AutopsyFrame\>), TotalFrames (int), ThrowingFrameIndex (int). Depends on T002-T006.
- [x] T008 Create `IExceptionAutopsyService` interface in `DebugMcp/Services/IExceptionAutopsyService.cs` — method: `Task<ExceptionAutopsyResult> GetExceptionContextAsync(int maxFrames = 10, int includeVariablesForFrames = 1, int maxInnerExceptions = 5, CancellationToken ct = default)`. Depends on T007.

**Checkpoint**: All models compile, interface defined, DI registered (will fail at runtime until implementation exists — that's expected).

---

## Phase 3: User Story 1 — Get full exception context in one call (Priority: P1) MVP

**Goal**: Implement the core `exception_get_context` tool that bundles exception type/message, stack frames, locals, inner exceptions into a single response.

**Independent Test**: Set an exception breakpoint, trigger exception, call `exception_get_context`, verify response contains all bundled data.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T009 [P] [US1] Write unit test `GetExceptionContext_WhenPausedAtException_ReturnsBundledContext` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — mock IProcessDebugger (CurrentPauseReason=Exception, ActiveThreadId=1) and IDebugSessionManager (GetStackFrames returns 3 frames, GetVariables returns 2 locals). Assert result contains: exception type/message, isFirstChance, threadId, frames with locations, variables for frame 0.
- [x] T010 [P] [US1] Write unit test `GetExceptionContext_WhenNotPausedAtException_ThrowsInvalidOperationException` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — mock CurrentPauseReason=Breakpoint (no exception). Assert service throws or returns error.
- [x] T011 [P] [US1] Write unit test `GetExceptionContext_WithInnerExceptions_ReturnsChain` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — mock EvaluateAsync to return inner exception type/message for 2 levels, null for 3rd. Assert innerExceptions has 2 entries with correct depth values.
- [x] T012 [P] [US1] Write unit test `GetExceptionContext_WhenVariableInspectionFails_ReturnsPartialResultWithErrors` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — mock GetVariables to throw for one frame. Assert result has FrameVariables with errors populated and locals for successful frames.
- [x] T013 [P] [US1] Write unit test `GetExceptionContext_WhenSymbolsMissing_ReturnsFramesWithNullLocation` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — mock GetStackFrames returning frames with IsExternal=true and Location=null. Assert AutopsyFrame.location is null and isExternal is true.

### Implementation for User Story 1

- [x] T014 [US1] Implement `ExceptionAutopsyService.GetExceptionContextAsync` in `DebugMcp/Services/ExceptionAutopsyService.cs` — constructor injects IDebugSessionManager + IProcessDebugger + ILogger. Core flow: (1) check CurrentPauseReason is Exception or (CurrentPauseReason is Breakpoint and last hit has ExceptionInfo), (2) get ActiveThreadId, (3) evaluate `$exception.GetType().FullName` and `$exception.Message` and `$exception.StackTrace` via EvaluateAsync, (4) call GetStackFrames with maxFrames, (5) for top N frames call GetVariables with try/catch for partial results, (6) walk inner exception chain via EvaluateAsync loop. Per research.md R-001 through R-006.
- [x] T015 [US1] Implement inner exception chain walker as private method `WalkInnerExceptionsAsync` in `DebugMcp/Services/ExceptionAutopsyService.cs` — loop: evaluate `$exception.InnerException` (then `.InnerException.InnerException`, etc.) up to maxInnerExceptions depth. For each: evaluate `.GetType().FullName` and `.Message`. Return list of InnerExceptionEntry. Set truncated=true if chain continues beyond max. Per research.md R-002.
- [x] T016 [US1] Implement frame variable collection as private method `CollectFrameVariablesAsync` in `DebugMcp/Services/ExceptionAutopsyService.cs` — for each frame index within includeVariablesForFrames: call GetVariables(threadId, frameIndex, "all"). Wrap in try/catch — on failure, create VariableError entries. Return FrameVariables with locals (one-level expanded) and errors.
- [x] T017 [US1] Create `ExceptionGetContextTool` MCP tool in `DebugMcp/Tools/ExceptionGetContextTool.cs` — `[McpServerToolType]` class with `[McpServerTool(Name = "exception_get_context")]` method. Parameters: max_frames (int, default 10), include_variables_for_frames (int, default 1), max_inner_exceptions (int, default 5). Validate parameter ranges per contract. Delegate to IExceptionAutopsyService. Serialize result to JSON. Follow existing tool patterns (stopwatch, logging, error handling). Per contracts/exception_get_context.json.
- [x] T018 [US1] Verify all T009-T013 unit tests pass after implementation. Run `dotnet test --filter "FullyQualifiedName~ExceptionAutopsyService"`.

**Checkpoint**: `exception_get_context` tool is functional. Agent can call it when paused at any exception (breakpoint or unhandled) and receive full bundled context. All unit tests green.

---

## Phase 4: User Story 2 — Configurable depth and scope (Priority: P2)

**Goal**: Ensure all three parameters (max_frames, include_variables_for_frames, max_inner_exceptions) correctly control output scope.

**Independent Test**: Call autopsy with non-default parameters and verify response respects limits.

### Tests for User Story 2

- [x] T019 [P] [US2] Write unit test `GetExceptionContext_WithMaxFrames3_ReturnsOnly3Frames` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — mock GetStackFrames with 10 frames available. Call with maxFrames=3. Assert result.Frames.Count == 3 and totalFrames == 10.
- [x] T020 [P] [US2] Write unit test `GetExceptionContext_WithVariablesFor3Frames_ReturnsVariablesForTop3` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — mock 5 frames. Call with includeVariablesForFrames=3. Assert frames[0..2] have non-null Variables, frames[3..4] have null Variables.
- [x] T021 [P] [US2] Write unit test `GetExceptionContext_WithMaxInnerExceptions0_SkipsInnerChain` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — call with maxInnerExceptions=0. Assert innerExceptions is empty and EvaluateAsync for InnerException is never called.
- [x] T022 [P] [US2] Write unit test `GetExceptionContext_WithDefaultParameters_Uses10Frames1Variable5Inner` in `tests/DebugMcp.Tests/Unit/Inspection/ExceptionAutopsyServiceTests.cs` — call with defaults. Assert GetStackFrames called with maxFrames=10, GetVariables called for frame 0 only, inner chain walks up to 5 levels.

### Implementation for User Story 2

- [x] T023 [US2] Add parameter validation to `ExceptionGetContextTool` in `DebugMcp/Tools/ExceptionGetContextTool.cs` — validate max_frames (1-100), include_variables_for_frames (0-10), max_inner_exceptions (0-20). Return structured error for out-of-range values. Per contracts/exception_get_context.json inputSchema.
- [x] T024 [US2] Verify all T019-T022 tests pass. Run `dotnet test --filter "FullyQualifiedName~ExceptionAutopsyService"`.

**Checkpoint**: All three parameters work correctly. Defaults match spec (FR-006). Parameter validation rejects invalid ranges.

---

## Phase 5: User Story 3 — Autopsy via breakpoint_wait integration (Priority: P3)

**Goal**: Extend `breakpoint_wait` with optional `include_autopsy` parameter. When true and an exception breakpoint fires, include full autopsy context in the response.

**Independent Test**: Call `breakpoint_wait` with `include_autopsy: true`, trigger exception breakpoint, verify response contains autopsy bundle alongside standard hit info.

### Tests for User Story 3

- [x] T025 [P] [US3] Write unit test `WaitForBreakpoint_WithIncludeAutopsyTrue_WhenExceptionHit_IncludesAutopsyInResponse` in `tests/DebugMcp.Tests/Unit/Inspection/BreakpointWaitAutopsyTests.cs` — mock BreakpointManager to return hit with ExceptionInfo, mock ExceptionAutopsyService to return result. Assert JSON response includes "autopsy" field with bundled context.
- [x] T026 [P] [US3] Write unit test `WaitForBreakpoint_WithIncludeAutopsyTrue_WhenRegularBreakpointHit_NoAutopsyInResponse` in `tests/DebugMcp.Tests/Unit/Inspection/BreakpointWaitAutopsyTests.cs` — mock hit with null ExceptionInfo. Assert response has no "autopsy" field (or autopsy: null).
- [x] T027 [P] [US3] Write unit test `WaitForBreakpoint_WithoutIncludeAutopsy_WhenExceptionHit_ResponseUnchanged` in `tests/DebugMcp.Tests/Unit/Inspection/BreakpointWaitAutopsyTests.cs` — mock exception hit but include_autopsy not set. Assert response matches current format exactly (backward compatible, no autopsy field).

### Implementation for User Story 3

- [x] T028 [US3] Add `include_autopsy` parameter to `BreakpointWaitTool.WaitForBreakpointAsync` in `DebugMcp/Tools/BreakpointWaitTool.cs` — `[Description("Include full exception autopsy context when exception breakpoint fires")] bool include_autopsy = false`. Inject IExceptionAutopsyService via constructor.
- [x] T029 [US3] Add autopsy logic to breakpoint wait response in `DebugMcp/Tools/BreakpointWaitTool.cs` — after receiving hit: if include_autopsy && hit.ExceptionInfo != null, call `_autopsyService.GetExceptionContextAsync()`. Serialize autopsy result as "autopsy" field in JSON response. Wrap in try/catch — if autopsy fails, still return standard response with warning. Per contracts/breakpoint_wait_autopsy.json.
- [x] T030 [US3] Verify all T025-T027 tests pass. Run `dotnet test --filter "FullyQualifiedName~BreakpointWaitAutopsy"`.
- [x] T031 [US3] Run full existing breakpoint_wait test suite to verify backward compatibility: `dotnet test --filter "FullyQualifiedName~BreakpointWait"` — zero regressions (SC-003).

**Checkpoint**: `breakpoint_wait` with `include_autopsy: true` works. Default behavior unchanged. All existing tests pass.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Ensure quality, documentation, and full test coverage

- [x] T032 [P] Add `exception_get_context` to MCP tools documentation in `docs/MCP_TOOLS.md` — include description, parameters with defaults/ranges, example request, example success response, example error response, and example partial response (missing symbols).
- [x] T033 [P] Add `include_autopsy` parameter documentation to `breakpoint_wait` section in `docs/MCP_TOOLS.md`.
- [x] T034 Run full unit test suite: `dotnet test tests/DebugMcp.Tests -c Release --no-build --filter "FullyQualifiedName~Unit"` — all tests pass including new ones.
- [x] T035 Build release: `dotnet build -c Release` — zero errors, zero warnings in new code.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: T002-T006 are parallel (different files). T007 depends on T002-T006. T008 depends on T007.
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion. Tests (T009-T013) are all parallel. Implementation (T014-T017) is sequential.
- **User Story 2 (Phase 4)**: Depends on Phase 3 (US1 implementation must exist for parameter tests to exercise). Tests T019-T022 are parallel.
- **User Story 3 (Phase 5)**: Depends on Phase 3 (needs IExceptionAutopsyService). Independent of Phase 4. Tests T025-T027 are parallel.
- **Polish (Phase 6)**: Depends on all user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Depends only on Foundational phase. This IS the MVP.
- **User Story 2 (P2)**: Depends on US1 (adds parameter validation/coverage to existing implementation).
- **User Story 3 (P3)**: Depends on US1 (reuses IExceptionAutopsyService). Independent of US2.

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD per constitution)
- Models before services
- Services before tools
- Core implementation before integration

### Parallel Opportunities

- Phase 2: T002-T006 (all model records) are fully parallel
- Phase 3 tests: T009-T013 are fully parallel
- Phase 4 tests: T019-T022 are fully parallel
- Phase 5 tests: T025-T027 are fully parallel
- Phase 4 and Phase 5 can run in parallel after Phase 3 completes (US2 and US3 are independent)
- Phase 6: T032 and T033 are parallel

---

## Parallel Example: User Story 1

```
# Launch all US1 tests together (write first, must fail):
T009: Unit test — paused at exception → bundled context
T010: Unit test — not paused at exception → error
T011: Unit test — inner exception chain
T012: Unit test — partial results on variable failure
T013: Unit test — missing symbols → null location

# After tests written, implement sequentially:
T014: ExceptionAutopsyService core logic
T015: Inner exception chain walker
T016: Frame variable collector
T017: ExceptionGetContextTool MCP wrapper
T018: Verify all tests pass
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational models + interface (T002-T008)
3. Complete Phase 3: User Story 1 (T009-T018)
4. **STOP and VALIDATE**: Call `exception_get_context` on a real exception — verify bundled response
5. This alone delivers the core value: 3-4 calls → 1 call

### Incremental Delivery

1. Setup + Foundational → Models and interface ready
2. Add User Story 1 → Core autopsy tool works (MVP!)
3. Add User Story 2 → Parameters validated, configurable depth
4. Add User Story 3 → `breakpoint_wait` integration, zero extra round-trips
5. Polish → Docs, full test pass, release build

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution mandates TDD — write tests first, verify they fail, then implement
- All new records should be positional records (project convention)
- Use DateTimeOffset for any timestamps (project convention)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
