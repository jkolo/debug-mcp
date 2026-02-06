# Feature Specification: Exception Autopsy

**Feature Branch**: `022-exception-autopsy`
**Created**: 2026-02-06
**Status**: Draft
**Input**: User description: "Exception Autopsy: one-shot context bundle when an exception is hit — exception type & message, top N stack frames, local variables in the throwing frame, and source line. Replaces 3-4 separate tool calls with a single call."

## Clarifications

### Session 2026-02-06

- Q: Should the autopsy tool work only for explicit exception breakpoints, or also for unhandled exceptions that stop the process? → A: Both — exception breakpoints AND unhandled exceptions that stop the process.
- Q: How deep should the autopsy expand local variable objects by default? → A: One level — show immediate properties of each local variable, but not their children. Agent can drill deeper via `variables_get` or `object_inspect`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Get full exception context in one call (Priority: P1)

An AI agent is debugging a .NET application and hits an exception breakpoint. Currently, the agent must issue 3-4 sequential tool calls to understand the exception: `breakpoint_wait` (get exception info), `stacktrace_get` (get frames), `variables_get` (get locals in throwing frame), and possibly `evaluate` (inspect inner exceptions). With Exception Autopsy, the agent calls a single tool and receives all this context bundled in one structured response.

**Why this priority**: This is the core value proposition. Every exception investigation requires this sequence, and reducing 3-4 round-trips to 1 saves significant latency and token usage. This alone justifies the feature.

**Independent Test**: Can be fully tested by setting an exception breakpoint, triggering an exception, then calling the autopsy tool. Delivers complete exception context in a single response.

**Acceptance Scenarios**:

1. **Given** the debugger is paused at an exception breakpoint, **When** the agent calls the exception autopsy tool, **Then** the response contains: exception type, exception message, whether it is first-chance or unhandled, stack frames (top N), local variables and arguments in the throwing frame, and source file/line of the throw site.
2. **Given** the debugger is paused at an exception breakpoint for an exception with an inner exception chain, **When** the agent calls the exception autopsy tool, **Then** the response includes the full inner exception chain with type and message for each level.
3. **Given** the debugger is paused at an exception breakpoint but symbols are missing for the throwing frame, **When** the agent calls the exception autopsy tool, **Then** the response still returns whatever information is available (exception type/message, raw stack trace) with clear indicators that source/variables are unavailable.
4. **Given** the debugger is paused due to an unhandled exception (no explicit exception breakpoint set), **When** the agent calls the exception autopsy tool, **Then** the response contains the same full context bundle as for an exception breakpoint hit.
5. **Given** the debugger is NOT paused at an exception, **When** the agent calls the exception autopsy tool, **Then** the response returns a clear error indicating no exception is available.

---

### User Story 2 - Configurable depth and scope (Priority: P2)

An AI agent wants to control how much data the autopsy returns — e.g., only the top 3 frames instead of 10, or include variables for the top 3 frames instead of just the throwing frame. This lets the agent balance context richness against token usage.

**Why this priority**: Without configurability, the tool may return too much data (wasting tokens) or too little (requiring follow-up calls). Sensible defaults make P1 functional, but configurable depth makes the tool efficient across diverse debugging scenarios.

**Independent Test**: Can be tested by calling the autopsy tool with different depth parameters and verifying the response matches the requested scope.

**Acceptance Scenarios**:

1. **Given** the debugger is paused at an exception, **When** the agent calls autopsy with `max_frames: 3`, **Then** only the top 3 stack frames are included.
2. **Given** the debugger is paused at an exception, **When** the agent calls autopsy with `include_variables_for_frames: 3`, **Then** local variables are included for the top 3 frames (not just the throwing frame).
3. **Given** the agent calls autopsy with default parameters, **Then** the response uses sensible defaults: top 10 stack frames, variables for the throwing frame only.

---

### User Story 3 - Autopsy via breakpoint_wait integration (Priority: P3)

When an agent is already using `breakpoint_wait` and an exception breakpoint fires, the agent can opt-in to receive the autopsy context directly in the wait response — without needing a separate call. This further reduces the total number of tool calls for the most common workflow.

**Why this priority**: This is an optimization over P1. The P1 tool works standalone, but for the "wait → analyze" workflow, embedding autopsy in the wait response eliminates one more round-trip. It depends on the P1 autopsy logic being implemented first.

**Independent Test**: Can be tested by calling `breakpoint_wait` with the autopsy flag, triggering an exception breakpoint, and verifying the response contains full autopsy context.

**Acceptance Scenarios**:

1. **Given** the agent calls `breakpoint_wait` with `include_autopsy: true`, **When** an exception breakpoint fires, **Then** the wait response includes the full autopsy bundle alongside the standard hit information.
2. **Given** the agent calls `breakpoint_wait` with `include_autopsy: true`, **When** a non-exception breakpoint fires, **Then** the wait response contains standard hit information without autopsy data (since no exception is present).
3. **Given** the agent calls `breakpoint_wait` without the autopsy flag (default), **When** an exception breakpoint fires, **Then** the wait response is unchanged from current behavior (backward compatible).

---

### Edge Cases

- What happens when the exception is thrown in native code (no managed frame at top of stack)? The autopsy should return available managed frames and note that the throw originated in native code.
- What happens when the exception object cannot be inspected (e.g., corrupted state exception)? The autopsy should return type/message from the callback data with a warning that full inspection failed.
- What happens when local variable inspection times out or fails for one variable? The autopsy should return partial results — successful variables plus error markers for failed ones — rather than failing entirely.
- What happens when the exception has a very deep inner exception chain (10+ levels)? The autopsy should cap the inner exception chain at a reasonable limit (e.g., 10) and indicate if more exist.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a tool that returns a bundled exception context when the debugger is paused at an exception — whether triggered by an explicit exception breakpoint or by an unhandled exception that stopped the process.
- **FR-002**: The bundled context MUST include: exception type name, exception message, first-chance vs. unhandled status, stack frames up to a configurable limit, local variables and arguments for the throwing frame (expanded one level deep — immediate properties shown, nested objects as type/value summary), and source file/line for each frame where symbols are available.
- **FR-003**: The bundled context MUST include the inner exception chain (type and message for each level), capped at a configurable maximum depth.
- **FR-004**: System MUST return partial results with clear status indicators when some parts of the context are unavailable (missing symbols, failed variable inspection, native frames).
- **FR-005**: System MUST allow the caller to control: number of stack frames returned, number of frames to include variables for, and maximum inner exception chain depth.
- **FR-006**: System MUST use sensible defaults when optional parameters are not provided (top 10 frames, variables for throwing frame only, inner exception depth of 5).
- **FR-007**: System MUST return a clear error when called while the debugger is not paused at an exception.
- **FR-008**: The existing `breakpoint_wait` tool MUST accept an optional parameter to include autopsy context in its response when an exception breakpoint fires.
- **FR-009**: The autopsy response MUST include the thread ID where the exception occurred.
- **FR-010**: Adding the autopsy parameter to `breakpoint_wait` MUST NOT change its default behavior (backward compatible).

### Key Entities

- **ExceptionAutopsyResult**: Bundled context for an exception hit — contains exception details, stack frames with source info, local variables per frame, inner exception chain, and availability status markers.
- **InnerExceptionEntry**: Type and message for one level of the inner exception chain.
- **FrameVariables**: Variables and arguments for a single stack frame, with per-variable success/failure status.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An agent can obtain complete exception context (type, message, stack, locals, source) in a single tool call instead of 3-4 calls.
- **SC-002**: Default autopsy response completes within 2 seconds for typical exceptions (under 20 stack frames, under 50 local variables in the throwing frame).
- **SC-003**: All existing `breakpoint_wait` behavior is preserved when the autopsy parameter is not provided (zero regressions in existing tests).
- **SC-004**: Partial results are returned successfully when symbols, source, or variable inspection are unavailable — the tool never fails entirely due to missing optional context.

## Assumptions

- The debugger is already paused at an exception — either via an explicit exception breakpoint (`breakpoint_set_exception`) or due to an unhandled exception that stopped the process. The autopsy tool inspects the current state; it does not set breakpoints or continue execution.
- Exception objects in .NET expose `InnerException`, `StackTrace`, `Message`, and `GetType()` — these are always available for managed exceptions.
- Variable inspection for a frame reuses the same mechanism as the existing `variables_get` tool.
- Stack frame retrieval reuses the same mechanism as the existing `stacktrace_get` tool.
- The autopsy does not evaluate arbitrary expressions — it reads existing state only, making it inherently safe (no side effects).

## Dependencies

- Existing exception breakpoint infrastructure (`breakpoint_set_exception`, `ExceptionInfo` model).
- Existing stack trace retrieval (`stacktrace_get` / `IProcessDebugger`).
- Existing variable inspection (`variables_get` / `IProcessDebugger`).
- Existing source location resolution (symbol server, PDB loading).
