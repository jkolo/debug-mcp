# Feature Specification: Async Stack Traces

**Feature Branch**: `026-async-stack-traces`
**Created**: 2026-02-11
**Status**: Draft
**Input**: User description: "Async stack traces."

## User Scenarios & Testing

### User Story 1 - Logical Async Call Stack (Priority: P1)

When an AI agent inspects the stack trace of a paused async method, the debugger presents a **logical call chain** that shows the sequence of async method calls as the developer wrote them, rather than the raw compiler-generated `MoveNext()` frames and thread pool internals.

Today, pausing inside an `async Task` method shows frames like:
```
[0] MyApp.Services.<GetUserAsync>d__5.MoveNext()
[1] System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(...)
[2] System.Threading.ExecutionContext.RunInternal(...)
[3] System.Threading.ThreadPoolWorkQueue.Dispatch()
```

After this feature, the same pause shows:
```
[0] MyApp.Services.GetUserAsync()              [async]
[1] MyApp.Controllers.HandleRequest()          [async, awaiting]
[2] MyApp.Program.Main()                       [async, awaiting]
```

**Why this priority**: Async/await is the dominant pattern in modern .NET code. Without logical stack traces, AI agents see meaningless compiler-generated frames and cannot reason about the actual execution flow. This is the core value of the feature.

**Independent Test**: Pause inside an `async Task` method in a test app. Call `stacktrace_get`. Verify the response contains logical method names (not `MoveNext`) with `frame_kind` indicating async frames.

**Acceptance Scenarios**:

1. **Given** the debuggee is paused inside an `async Task` method, **When** the agent calls `stacktrace_get`, **Then** the top frame shows the original async method name (not `<MethodName>d__N.MoveNext()`) and includes `frame_kind: "async"`.

2. **Given** the debuggee is paused inside a nested async call chain (A awaits B awaits C), **When** the agent calls `stacktrace_get`, **Then** all three methods appear in the logical stack in correct call order (C at top, A at bottom), each marked as async.

3. **Given** the debuggee is paused in synchronous code called from an async method, **When** the agent calls `stacktrace_get`, **Then** synchronous frames show `frame_kind: "sync"` and the calling async frame shows `frame_kind: "async"`.

4. **Given** the debuggee is paused and the agent requests the raw physical stack, **When** the agent calls `stacktrace_get` with `include_raw: true`, **Then** the response includes both the logical frames and the raw physical frames for advanced inspection.

---

### User Story 2 - Async Continuation Chain Discovery (Priority: P2)

When an async method is paused at an `await` point, the debugger traces the continuation chain backward through `Task.m_continuationObject` to discover which async methods are logically waiting for this result. This reveals the full async call chain even when callers are suspended on different threads or the thread pool.

**Why this priority**: In real-world applications, the calling async method is typically not on the physical call stack at all — it registered a continuation and returned. Without continuation chain walking, the logical stack shows only the current frame with no callers.

**Independent Test**: Create a test app where method A awaits B which awaits C. Pause in C. Call `stacktrace_get`. Verify A and B appear in the logical stack even though they are not on the physical thread stack.

**Acceptance Scenarios**:

1. **Given** method A awaits B which awaits C, and execution is paused in C, **When** the agent calls `stacktrace_get`, **Then** the logical stack includes frames for A, B, and C in correct order, with A and B marked as `awaiting: true`.

2. **Given** the continuation chain includes framework-internal methods (e.g., `Task.WhenAll` internals), **When** the logical stack is constructed, **Then** framework internals are collapsed or marked as external, showing only user-code callers.

3. **Given** the continuation chain is broken or unresolvable (e.g., fire-and-forget `Task.Run`), **When** the logical stack is constructed, **Then** the stack ends gracefully with an indicator that the chain could not be fully resolved.

---

### User Story 3 - Async State Machine Variable Inspection (Priority: P3)

When an AI agent inspects variables in an async frame, the debugger maps compiler-generated state machine field names (like `<>7__wrap1`, `<result>5__2`) back to their original local variable names and presents them naturally.

**Why this priority**: After seeing the logical call stack, agents need to inspect variables. Compiler-generated field names are opaque; mapping them to source names makes async debugging as natural as synchronous debugging.

**Independent Test**: Pause inside an async method that has local variables. Call `variables_get` on the async frame. Verify variables show their original source names, not compiler-generated names.

**Acceptance Scenarios**:

1. **Given** the debuggee is paused in an async method with local variables, **When** the agent calls `variables_get` on that frame, **Then** local variables captured in the state machine are displayed with their original source names (e.g., `result` not `<result>5__2`).

2. **Given** the async method has a `this` reference captured in the state machine, **When** the agent inspects the frame, **Then** `this` is accessible just as in a synchronous frame.

3. **Given** the debuggee is in a `MoveNext` frame (physical), **When** the agent requests variables with the logical frame reference, **Then** the debugger reads the state machine's fields and presents them as the original method's locals.

---

### Edge Cases

- What happens when a `ValueTask` is used instead of `Task`? The continuation chain mechanism differs — the debugger should handle both `Task<T>` and `ValueTask<T>`.
- How does the system handle `ConfigureAwait(false)` where continuations run on arbitrary threads? The logical stack should still be correct since it follows the continuation chain, not physical thread affinity.
- What happens with `Task.WhenAll` or `Task.WhenAny` where multiple continuations exist? The debugger should follow the single continuation that leads to the current pause point.
- What happens with iterator methods (`IAsyncEnumerable`) that also use state machines? The frame should be correctly identified as async iterator, not conflated with regular async.
- What happens when the state machine object has been garbage collected? The continuation chain ends and the logical stack shows only resolvable frames.
- What happens in release builds where local variable names are stripped from PDBs? The debugger falls back to compiler-generated names gracefully.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST detect async state machine frames (`<MethodName>d__N.MoveNext()`) and resolve them to the original async method name.
- **FR-002**: The system MUST assign a `frame_kind` to each stack frame: `sync`, `async`, or `async_continuation` (for frames discovered via continuation chain, not on the physical stack).
- **FR-003**: The system MUST walk the `Task.m_continuationObject` chain to discover async callers that are not on the physical thread stack.
- **FR-004**: The system MUST present the logical async call stack by default when calling `stacktrace_get`, with raw physical frames available via an `include_raw` parameter.
- **FR-005**: The system MUST collapse or mark framework-internal async machinery frames (thread pool dispatch, `ExecutionContext.Run`, `AsyncMethodBuilder.Start`, etc.) as external by default.
- **FR-006**: The system MUST handle both `Task<T>` and `ValueTask<T>` continuation chains.
- **FR-007**: The system MUST map compiler-generated state machine field names to their original local variable names using PDB sequence point and local scope information.
- **FR-008**: The system MUST indicate when a logical frame is in an `awaiting` state (the async method has yielded at an `await` and is waiting for a result).
- **FR-009**: The system MUST gracefully degrade when continuation chains are unresolvable (fire-and-forget, GC'd tasks) — show partial logical stack without error.
- **FR-010**: The system MUST work correctly for async methods in both Debug and Release build configurations (with reduced information in Release where PDBs lack locals).

### Key Entities

- **Logical Frame**: A stack frame representing an async method as the developer wrote it, with the original method name, source location at the `await` point, and frame kind.
- **Continuation Chain**: The linked list of `Task.m_continuationObject` references connecting an awaited task back to its logical caller.
- **Async State Machine**: The compiler-generated class (`IAsyncStateMachine` implementation) that holds captured locals and tracks the current `await` position via `<>1__state`.

## Success Criteria

### Measurable Outcomes

- **SC-001**: For any async call chain of depth 3 or more, the logical stack trace shows all async callers with correct method names and source locations, matching what a human developer would expect to see.
- **SC-002**: Framework-internal frames (thread pool, `ExecutionContext`, `AsyncMethodBuilder`) are hidden by default, reducing noise by at least 60% compared to raw physical frames.
- **SC-003**: State machine local variables are presented with their original source names in at least 90% of cases (Debug builds with full PDBs).
- **SC-004**: The async stack trace response adds no more than 500ms overhead compared to the current synchronous stack trace for a typical 10-frame async call chain.
- **SC-005**: All 34 existing tools continue to work unchanged — async stack traces are an enhancement to `stacktrace_get`, not a breaking change.

## Assumptions

- ClrDebug 0.3.4 exposes field enumeration sufficient to read `Task.m_continuationObject` and state machine fields without needing raw memory reads.
- PDB files (portable PDBs) for Debug builds contain local variable name mappings for state machine fields via the `StateMachineHoistedLocalScopes` custom debug information.
- The `AsyncStateMachineAttribute` on the original method provides a reliable link from the `MoveNext()` frame back to the original method name.
- `Task.m_continuationObject` field layout is stable across .NET 8+ and is accessible via field inspection on the debuggee's `Task` object.

## Scope

### In Scope

- Logical async stack trace synthesis from physical frames + continuation chain
- Frame kind classification (sync, async, async_continuation)
- State machine local variable name resolution
- `include_raw` parameter for physical frame access
- Framework frame collapsing

### Out of Scope

- Async debugging for F# or other .NET languages (C# only)
- Historical async trace recording (only current state at pause point)
- Cross-process async tracing (distributed tracing)
- `IAsyncEnumerable` iterator state machine support (can be added later)
- Thread affinity analysis (which thread ran which continuation)
