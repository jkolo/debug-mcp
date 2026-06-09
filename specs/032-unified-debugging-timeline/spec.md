# Feature Specification: Unified Debugging Timeline

**Feature Branch**: `032-unified-debugging-timeline`

**Created**: 2026-06-09

**Status**: Draft

**Input**: User description: "Unified Debugging Timeline — `debugger://timeline` resource merging breakpoint hits, exceptions, module loads, thread starts/exits, stdout/stderr events with stable IDs and entity references."

## Clarifications

### Session 2026-06-09

- Q: Should breakpoint/tracepoint hit events in the timeline include captured local variable values? → A: No — only metadata (breakpoint ID, thread ID, source file, line number). Locals are deliberately excluded to keep events lightweight and avoid evaluation cost at capture time.
- Q: Should debugger control operations (pause, step, continue) appear as events in the timeline? → A: No — timeline records only program-domain events (exceptions, breakpoints, modules, threads, I/O), not agent control actions.
- Q: Should the timeline send push notifications when new events arrive? → A: No — pull-only; agent reads `debugger://timeline` on demand. Existing breakpoint_wait / BreakpointNotifier handle event-driven waiting.
- Q: Should there be a `SessionStarted` event as the first event in the timeline? → A: Yes — `SessionStarted` is always the first event; contains session start timestamp, session type (launch/attach), and PID.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read Unified Event Stream (Priority: P1)

An AI agent is debugging a crash and wants to understand the sequence of events leading up to it. Instead of correlating information from multiple separate tools (exceptions, threads, stdout, breakpoints), the agent reads a single `debugger://timeline` resource and sees all debugging events in chronological order — breakpoint hits, exceptions thrown, module loads, thread starts/exits, and stdout/stderr output — each with a timestamp, event type, and references to relevant context (thread, source location, module).

**Why this priority**: Without a unified timeline, agents must reconstruct event sequences by cross-referencing multiple tool responses, which is error-prone, token-expensive, and often impossible (events that were never individually queried are invisible). This is the foundational capability of the feature.

**Independent Test**: Can be fully tested by starting a debug session, letting the application run through a variety of events (hit a breakpoint, throw an exception, print output), then reading `debugger://timeline` and verifying all event types appear in the correct chronological order.

**Acceptance Scenarios**:

1. **Given** a debug session is active and the process has hit a breakpoint, thrown an exception, and written to stdout, **When** the agent reads `debugger://timeline`, **Then** all three event types appear in timestamp order with correct event-type labels.

2. **Given** the timeline resource is read, **When** a module-load event is present, **Then** the event includes the module name, load timestamp, and whether symbols were resolved.

3. **Given** a thread started and then terminated during the session, **When** the timeline is read, **Then** both `thread_started` and `thread_exited` events appear with the thread ID and name (if available).

4. **Given** the process has written multiple lines to stdout and stderr, **When** the timeline is read, **Then** stdout and stderr events appear as distinct event types, each containing the text content and source stream label.

---

### User Story 2 - Cross-Event Correlation (Priority: P2)

An AI agent sees an exception in the timeline and wants to understand the full context around it: what stdout lines were printed just before, what thread it was on, and whether any module loaded recently. Instead of making multiple follow-up tool calls, the agent can navigate from the exception event to related events through stable cross-references.

**Why this priority**: The individual event list is useful on its own, but the real value for AI agents is the ability to reason across event types: "the exception happened 3 lines after this stdout output on the same thread." This cross-modal reasoning is what differentiates the timeline from individual tool responses.

**Independent Test**: Can be fully tested by reading the timeline and verifying that exception events reference the thread ID (which also appears in thread_started events), and that breakpoint hit events reference the source location (file + line).

**Acceptance Scenarios**:

1. **Given** a breakpoint hit event in the timeline, **When** the agent inspects it, **Then** the event includes thread ID, source file path, and line number — enabling direct correlation with other events on the same thread.

2. **Given** an exception event in the timeline, **When** the agent reads it, **Then** the exception type, message, and thread ID are present, matching what `exception_get_context` would return for that moment.

3. **Given** a stdout event followed shortly by an exception event on the same thread, **When** the agent reads both events, **Then** both events share the same thread ID, making the causal sequence unambiguous.

---

### User Story 3 - Filtered Timeline Reads (Priority: P3)

A debug session ran for several minutes and generated thousands of events. An AI agent wants to focus only on exception events or only on events from a specific thread, without loading the entire timeline into context.

**Why this priority**: For long-running processes the unfiltered timeline can exceed practical token budgets. Filtering is necessary for scalability, but basic timeline functionality is fully usable without it.

**Independent Test**: Can be fully tested by reading `debugger://timeline` with an event-type filter and verifying only events of that type are returned, and with a thread-ID filter returning only events for that thread.

**Acceptance Scenarios**:

1. **Given** a timeline with mixed event types, **When** the agent reads the timeline with `event_types: ["exception"]` filter, **Then** only exception events are returned.

2. **Given** a multi-threaded application, **When** the agent reads the timeline filtered by a specific thread ID, **Then** only events on that thread appear.

3. **Given** a long-running session, **When** the agent reads the timeline with a time-range filter (last N seconds or from event ID X), **Then** only events within that window are returned.

---

### Edge Cases

- What happens when no events have occurred yet? The timeline returns an empty array, not an error.
- How does the system handle stdout events with very long lines? Content is truncated to a reasonable length (e.g., 1 KB per event) with a `truncated: true` flag.
- What happens if the debug session ends while the timeline is being read? The timeline returns all events captured up to session end, plus a terminal `session_ended` event.
- How does the timeline handle very high event rates (tight loops with tracepoints)? Events are capped per session (e.g., 10,000 events); older events are dropped when the cap is reached and a `events_dropped` counter is included in the response.
- What happens when the timeline resource is read after session disconnect? The timeline remains readable until a new session starts or the server restarts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a `debugger://timeline` MCP resource containing all significant debugging events from the current session in chronological order.
- **FR-002**: The timeline MUST include the following event types: breakpoint hit, tracepoint hit, exception (first-chance and user-unhandled), module loaded, thread started, thread exited, stdout written, stderr written.
- **FR-003**: Each timeline event MUST include: a stable event ID, a UTC timestamp, an event type label, and type-specific payload fields (e.g., thread ID for thread events, exception type for exception events, source location for breakpoint hits).
- **FR-004**: Breakpoint and tracepoint hit events MUST include: thread ID, source file path, line number, and breakpoint ID. Local variable values are NOT captured in timeline events — only location metadata is stored.
- **FR-005**: Exception events MUST include: exception type name, message, thread ID, and whether it is first-chance or user-unhandled.
- **FR-006**: Module load events MUST include: module name, whether symbols were resolved, and the file path of the loaded assembly.
- **FR-007**: Thread lifecycle events MUST include: thread ID and (where available) thread name.
- **FR-008**: Stdout/stderr events MUST include: the text content (truncated at 1 KB with a `truncated` flag if needed) and the source stream (stdout or stderr).
- **FR-009**: The timeline resource MUST support filtering by one or more event types, by thread ID, and by a sliding time window or start-from-event-ID cursor.
- **FR-010**: The timeline MUST cap stored events at a configurable maximum (default: 10,000). When the cap is reached, the oldest events are evicted and a `events_dropped` counter is included in the resource response.
- **FR-011**: The timeline MUST be cleared when a new debug session starts (launch or attach).
- **FR-012**: The timeline resource MUST remain readable after session disconnect until a new session starts.
- **FR-013**: A `session_started` event MUST be the first event recorded when a new debug session begins (launch or attach). It MUST include: session start timestamp, session type (`launch` or `attach`), and the target process ID (PID).
- **FR-014**: A `session_ended` event MUST be appended to the timeline when the debug session disconnects or the process exits.

### Key Entities

- **TimelineEvent**: A single recorded event — includes stable ID (monotonically increasing integer), UTC timestamp, event type, thread ID (where applicable), and type-specific payload.
- **TimelineEventType**: Enumeration — `SessionStarted`, `BreakpointHit`, `TracepointHit`, `ExceptionFirstChance`, `ExceptionUserUnhandled`, `ModuleLoaded`, `ThreadStarted`, `ThreadExited`, `StdoutWritten`, `StderrWritten`, `SessionEnded`. Debugger control operations (pause, step, continue) are NOT timeline event types — timeline records program-domain events only.
- **TimelineFilter**: Optional filter parameters for reading the resource — event types to include, thread ID, max events to return, cursor (start after event ID).
- **TimelineResponse**: The resource payload — list of `TimelineEvent`, total event count, `events_dropped` counter, session ID.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An agent can reconstruct the complete sequence of debugging events from a short session (< 1 minute) with a single resource read, without any follow-up tool calls.
- **SC-002**: The timeline resource populates with all defined event types within 100ms of each event occurring (i.e., events are near-real-time).
- **SC-003**: Reading the timeline for a session with up to 1,000 events completes in under 500ms.
- **SC-004**: Cross-event correlation tasks (e.g., "find stdout lines printed before this exception") that previously required 3–5 tool calls can be performed using only the timeline resource.
- **SC-005**: Timeline filtering reduces result set to only the requested event types with no false positives.

## Assumptions

- The timeline is in-memory only — events are not persisted to disk and are lost when the server process restarts.
- The timeline captures events from a single active debug session; there is no multi-session timeline.
- stdout/stderr events are captured via the existing `ProcessIoManager` (feature 017) — no new I/O capture mechanism is needed.
- Tracepoint events are captured via the existing `BreakpointManager` `BreakpointResolved` event (feature 031) — no new tracepoint infrastructure is needed.
- Exception events are captured via the existing `ICorDebug` exception callbacks already implemented in `ProcessDebugger`.
- Module load events are captured via the existing `ICorDebug` `LoadModule` callback already wired in `ProcessDebugger`.
- Thread lifecycle events are captured via the existing `ICorDebug` `CreateThread` / `ExitThread` callbacks in `ProcessDebugger`.
- The 10,000-event default cap is sufficient for typical debugging sessions; it is configurable via a startup flag for power users.
- The `debugger://timeline` resource follows the existing MCP resource pattern established in feature 030 (using `[McpServerResource]` attribute and the existing resource infrastructure).
