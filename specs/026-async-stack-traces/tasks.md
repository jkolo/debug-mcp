# Tasks: Async Stack Traces

**Input**: Design documents from `/specs/026-async-stack-traces/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, quickstart.md

**Tests**: Included — the constitution mandates test-first (Principle III). Contract tests verify response shape and backward compatibility. Unit tests validate frame detection and chain walking logic.

**Organization**: US1 (logical call stack) is the MVP and must complete first. US2 (continuation chain) depends on US1's frame detection infrastructure. US3 (variable mapping) is independent of US2 but needs US1's async frame detection.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths included in all descriptions

## Path Conventions

- **Main project**: `DebugMcp/`
- **Test files**: `tests/DebugMcp.Tests/`
- **Spec files**: `specs/026-async-stack-traces/`

---

## Phase 1: Foundational — Model Extension & Test Infrastructure

**Purpose**: Extend the StackFrame model and create test infrastructure. Must complete before any user story implementation.

- [x] T001 Add `FrameKind` (string, default "sync"), `IsAwaiting` (bool, default false), and `LogicalFunction` (string?, default null) optional parameters to the StackFrame record in `DebugMcp/Models/Inspection/StackFrame.cs`
- [x] T002 Add `include_raw` parameter (bool, default false) to `stacktrace_get` tool in `DebugMcp/Tools/StacktraceGetTool.cs` — wire through to response but don't implement logic yet
- [x] T003 Emit `frame_kind`, `is_awaiting`, and `logical_function` fields in `BuildFrameResponse` in `DebugMcp/Tools/StacktraceGetTool.cs`
- [x] T004 Verify build succeeds with `dotnet build` — 0 errors
- [x] T005 Run full unit+contract suite with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — verify no regressions (all existing tests still pass with new default field values)

**Checkpoint**: StackFrame model extended, response format updated with defaults. All existing tests pass — backward compatibility confirmed.

---

## Phase 2: US1 — Logical Async Call Stack (Priority: P1) — MVP

**Goal**: Detect `MoveNext()` frames on compiler-generated state machine types and resolve them to original async method names. Add `frame_kind: "async"` to detected frames.

**Independent Test**: Pause inside an async method. Call `stacktrace_get`. Top frame shows original method name with `frame_kind: "async"`.

### Tests (written first, must fail before implementation)

- [x] T006 [US1] Create `tests/DebugMcp.Tests/Contract/AsyncStackTraceContractTests.cs` with test that verifies `stacktrace_get` response includes `frame_kind` field on every frame
- [x] T007 [US1] Add test in `AsyncStackTraceContractTests.cs` that verifies `include_raw` parameter is accepted by `stacktrace_get` without error
- [x] T008 [US1] Add test in `AsyncStackTraceContractTests.cs` that verifies backward compatibility — response still contains `success`, `thread_id`, `total_frames`, `frames[]` with `index`, `function`, `module`, `is_external`
- [x] T009 [US1] Create `tests/DebugMcp.Tests/Unit/AsyncFrameDetectionTests.cs` with tests for MoveNext pattern detection: (1) `<GetUserAsync>d__5.MoveNext()` → detected as async, original name `GetUserAsync`, (2) `Program.Main()` → not async, (3) `<>c.<Main>b__0_0()` (lambda) → not async
- [x] T010 [US1] Run tests with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~AsyncStackTrace|FullyQualifiedName~AsyncFrameDetection"` — verify tests are discoverable (some will fail before implementation)

### Implementation

- [x] T011 [US1] Add static method `TryParseAsyncStateMachineFrame(string typeName, string methodName)` to `DebugMcp/Services/ProcessDebugger.cs` — returns `(bool IsAsync, string? OriginalMethodName)` using regex `^<(.+?)>d__\d+$` on typeName when methodName is "MoveNext"
- [x] T012 [US1] Update `CreateStackFrame` in `DebugMcp/Services/ProcessDebugger.cs` — call `TryParseAsyncStateMachineFrame`, set `FrameKind = "async"` and `LogicalFunction` to original method name when detected; mark framework async internals (`AsyncMethodBuilderCore`, `ExecutionContext`, `ThreadPoolWorkQueue`) with `IsExternal = true`
- [x] T013 [US1] Update `GetStackFrames` in `DebugMcp/Services/ProcessDebugger.cs` — when building logical frame list, replace `MoveNext` function name with `{Namespace}.{OriginalMethod}()` using the enclosing type's declaring type (parent type contains the original method)
- [x] T014 [US1] Update `BuildFrameResponse` in `StacktraceGetTool.cs` — when `include_raw: true`, add `raw_frames` array to response containing unmodified physical frames alongside logical `frames`
- [x] T015 [US1] Run all tests with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~AsyncStackTrace|FullyQualifiedName~AsyncFrameDetection"` — all tests pass
- [x] T016 [US1] Run full unit+contract suite with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — verify no regressions

**Checkpoint**: `stacktrace_get` detects async frames on physical stack, resolves MoveNext to original names, returns `frame_kind`. US1 is fully functional for same-thread async calls.

---

## Phase 3: US2 — Async Continuation Chain Discovery (Priority: P2)

**Goal**: Walk `Task.m_continuationObject` to discover async callers suspended on other threads. Add `async_continuation` frames to the logical stack.

**Independent Test**: Pause in deeply nested async method. Call `stacktrace_get`. Logical stack shows all async callers even those not on the physical thread stack.

### Tests (written first)

- [x] T017 [US2] Create `tests/DebugMcp.Tests/Unit/AsyncStackTraceServiceTests.cs` with mock-based test: given a mock `TryGetFieldValue` that returns continuation chain of depth 3, service produces 3 logical frames with correct method names and `frame_kind: "async_continuation"`
- [x] T018 [US2] Add test in `AsyncStackTraceServiceTests.cs` for chain termination: null `m_continuationObject` stops chain walking; depth limit (50) stops infinite chains
- [x] T019 [US2] Add test in `AsyncStackTraceServiceTests.cs` for graceful degradation: unresolvable continuation type (non-delegate, non-Task) produces partial stack without error
- [x] T020 [US2] Run tests — verify discoverable (will fail before implementation)

### Implementation

- [x] T021 [US2] Create `DebugMcp/Services/AsyncStackTraceService.cs` — service class with constructor taking `ILogger<AsyncStackTraceService>`. Define interface `IAsyncStackTraceService` with method `IReadOnlyList<StackFrame> BuildLogicalFrames(IReadOnlyList<StackFrame> physicalFrames, Func<CorDebugValue, string, CorDebugValue?> fieldReader)`
- [x] T022 [US2] Implement `WalkContinuationChain` in `AsyncStackTraceService.cs` — read `m_continuationObject` field from Task value, handle: null (stop), delegate `_target` (extract state machine), `List<object>` (iterate), depth limit 50
- [x] T023 [US2] Implement `ExtractStateMachineFromContinuation` in `AsyncStackTraceService.cs` — given a continuation delegate, read `_target` field to get state machine instance; read `<>1__state` to determine await position; read `<>t__builder.m_task` to continue chain
- [x] T024 [US2] Make `TryGetFieldValue` in `ProcessDebugger.cs` internal (currently private) so `AsyncStackTraceService` can access it. Add helper method `GetFieldValueForChainWalking(CorDebugValue obj, string fieldName)` that wraps the call
- [x] T025 [US2] Register `IAsyncStackTraceService` in DI container in `DebugMcp/Program.cs`. Inject into `StacktraceGetTool` or `DebugSessionManager`.
- [x] T026 [US2] Integrate chain walking into `GetStackFrames` in `ProcessDebugger.cs` — after building physical frames, call `AsyncStackTraceService.BuildLogicalFrames` to append continuation chain frames with `frame_kind: "async_continuation"` and `is_awaiting: true`
- [x] T027 [US2] Add ValueTask handling in `AsyncStackTraceService.cs` — read `_obj` field from ValueTask struct; if it's a Task, delegate to standard chain walking; if IValueTaskSource, mark chain as unresolvable
- [x] T028 [US2] Run all tests — all pass
- [x] T029 [US2] Run full unit+contract suite — no regressions

**Checkpoint**: `stacktrace_get` discovers full async call chain including suspended callers. US1 + US2 deliver complete logical async stack traces.

---

## Phase 4: US3 — Async State Machine Variable Inspection (Priority: P3)

**Goal**: Map compiler-generated state machine field names to original local variable names using PDB custom debug info.

**Independent Test**: Pause in async method with locals. Call `variables_get`. Variables show original source names.

### Tests (written first)

- [x] T030 [US3] Add test in `AsyncStackTraceServiceTests.cs` for field name mapping: `<result>5__2` → `result`, `<>7__wrap1` → stripped to display name, `<>1__state` → `__state` (internal)
- [x] T031 [US3] Add test in `AsyncStackTraceContractTests.cs` verifying `variables_get` on an async frame returns variables without angle-bracket prefixes in names

### Implementation

- [ ] T032 [US3] *(deferred)* Add `GetStateMachineLocalNamesAsync` method to `DebugMcp/Services/Breakpoints/PdbSymbolReader.cs` — use `MetadataReader.GetCustomDebugInformation()` to read `StateMachineHoistedLocalScopes` blob — heuristic fallback in T033 covers common cases
- [x] T033 [US3] Add fallback name stripping in `AsyncStackTraceService.cs` — when PDB info unavailable, apply heuristic: `<name>5__N` → `name`, `<>1__state` → `__state`, `<>t__builder` → `__builder`
- [x] T034 [US3] Update `GetLocals` or `GetThisReference` in `ProcessDebugger.cs` — when frame is detected as async (MoveNext), read state machine fields via `this` reference and apply name mapping from PdbSymbolReader before returning variables
- [x] T035 [US3] Run all tests — all pass
- [x] T036 [US3] Run full unit+contract suite — no regressions

**Checkpoint**: Variables in async frames show original source names. All three user stories delivered.

---

## Phase 5: Polish & Final Verification

**Purpose**: Structured logging, full regression test, quickstart validation.

- [x] T037 Add structured logging to `AsyncStackTraceService.cs` — log chain depth, unresolvable continuations, fallback paths at Debug level; log frame detection results at Information level
- [x] T038 Run full build with `dotnet build` — verify 0 errors, 0 warnings
- [x] T039 Run full test suite with `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — all tests pass
- [ ] T040 Run quickstart.md validation steps from `specs/026-async-stack-traces/quickstart.md` *(manual — requires live debugger session)*

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — start immediately
- **Phase 2 (US1)**: Depends on Phase 1 (needs extended StackFrame model)
- **Phase 3 (US2)**: Depends on Phase 2 (needs async frame detection from US1)
- **Phase 4 (US3)**: Depends on Phase 2 (needs async frame detection); independent of Phase 3
- **Phase 5 (Polish)**: Depends on all previous phases

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 1 — No dependencies on other stories
- **US2 (P2)**: Depends on US1's frame detection infrastructure (TryParseAsyncStateMachineFrame)
- **US3 (P3)**: Depends on US1's frame detection — independent of US2

### Within Each Phase

- Tests MUST be written and FAIL before implementation
- Model changes before service changes
- Service changes before tool changes
- Core logic before integration

### Parallel Opportunities

- T006, T007, T008 (US1 contract tests) can be written in parallel
- T009 (US1 unit tests) is independent of T006-T008
- Phase 3 (US2) and Phase 4 (US3) can run in parallel after Phase 2 completes
- T030 and T031 (US3 tests) can be written in parallel
- T032 and T033 (US3 PDB + fallback) can be implemented in parallel

---

## Implementation Strategy

### MVP First (Phase 1 + Phase 2)

1. Complete Phase 1: Extend StackFrame model (unblocks everything)
2. Complete Phase 2: Async frame detection + logical names (US1 delivered)
3. **STOP and VALIDATE**: `stacktrace_get` shows logical async method names on same-thread calls
4. This alone delivers the core value — AI agents see meaningful async stack traces

### Incremental Delivery

1. Phase 1 → Model ready
2. Phase 2 → Logical names on physical stack (US1 delivered)
3. Phase 3 → Full continuation chain (US2 delivered) — biggest complexity leap
4. Phase 4 → Variable name mapping (US3 delivered)
5. Phase 5 → Polish + final validation

### Single-Developer Strategy

Since this is a medium-complexity feature (~7 files, 40 tasks), optimal order:
1. T001–T005: Extend model, update tool response, verify build
2. T006–T010: Write US1 tests, verify discoverable
3. T011–T016: Implement frame detection, verify tests pass
4. T017–T020: Write US2 tests
5. T021–T029: Implement chain walking, verify tests pass
6. T030–T036: US3 variable mapping
7. T037–T040: Polish and final validation

---

## Notes

- [P] tasks = different files or different methods, no dependencies
- [Story] label maps task to specific user story
- Constitution Principle III (Test-First) requires tests before implementation in each phase
- US2 (continuation chain) is the most complex phase — TryGetFieldValue chain walking through Task internals requires careful handling of null/broken chains
- US3 (variable mapping) is independent of US2 and can be implemented in parallel if desired
- The `TryParseAsyncStateMachineFrame` regex `^<(.+?)>d__\d+$` is the same pattern used by Visual Studio, Rider, and dotnet-dump — stable since C# 5.0
