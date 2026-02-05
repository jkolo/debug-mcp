# Feature Specification: MCP Resources for Debugger State

**Feature Branch**: `019-mcp-resources`
**Created**: 2026-02-05
**Status**: Draft
**Input**: User description: "Dodajmy MCP Resources do DebugMcp. Resources to read-only dane które LLM może przeglądać bez wywoływania tooli — dają kontekst automatycznie. Chcemy wystawić: 1) stan sesji debuggera (PID, attached process, moduły, breakpointy) jako resource debugger://session, 2) listę wątków jako debugger://threads, 3) aktywne breakpointy jako debugger://breakpoints, 4) kod źródłowy debugowanego procesu (z PDB paths) jako debugger://source/{file}. Resources powinny być dostępne tylko gdy jest aktywna sesja debugowania. Powinny się aktualizować (subscriptions/notifications) gdy stan się zmienia."

## Summary

Expose debugger state as MCP Resources — read-only data views that LLM clients can browse without invoking tools. Resources provide automatic context about the debug session, reducing the number of tool calls needed for the LLM to understand the current state. Four resource endpoints are exposed: session overview, thread list, breakpoint list, and source code from the debugged process. Resources are only available when a debug session is active and emit change notifications via MCP subscriptions.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Debug Session Overview Resource (Priority: P1)

As an LLM client debugging a .NET application, I want to read the current debug session state (process ID, name, runtime version, current location, pause reason) as a resource so I have immediate context without calling `debug_state` every time.

**Why this priority**: The session resource is the foundational piece — it tells the LLM whether debugging is active and provides the top-level context for all subsequent decisions.

**Independent Test**: Attach to a process, read `debugger://session`, verify it contains process info and session state.

**Acceptance Scenarios**:

1. **Given** a debug session is active (attached or launched), **When** an LLM reads `debugger://session`, **Then** it receives a JSON document containing: process ID, process name, executable path, runtime version, session state (Running/Paused), launch mode, attached timestamp, and current location (if paused).

2. **Given** no debug session is active, **When** an LLM lists available resources, **Then** `debugger://session` is NOT present in the resource list.

3. **Given** a debug session is active, **When** the debugger pauses at a breakpoint, **Then** a resource-changed notification is emitted for `debugger://session` so subscribed clients can refresh.

4. **Given** a debug session is active, **When** the session is disconnected, **Then** all debugger resources are removed and a resource-list-changed notification is emitted.

---

### User Story 2 - Breakpoints Resource (Priority: P1)

As an LLM client, I want to browse all active breakpoints (including tracepoints and exception breakpoints) as a resource so I know what instrumentation is in place without calling `breakpoint_list`.

**Why this priority**: Same priority as session — breakpoints are the primary debugging tool and the LLM needs to know what's set to make good debugging decisions.

**Independent Test**: Set breakpoints, read `debugger://breakpoints`, verify they appear with correct details.

**Acceptance Scenarios**:

1. **Given** a debug session with breakpoints set, **When** an LLM reads `debugger://breakpoints`, **Then** it receives a JSON list of all breakpoints containing: ID, type (breakpoint/tracepoint/exception), file path, line number, enabled state, verified state, hit count, condition (if any), and log message (if tracepoint).

2. **Given** a breakpoint is added or removed during a session, **When** the change completes, **Then** a resource-changed notification is emitted for `debugger://breakpoints`.

3. **Given** no debug session is active, **When** an LLM lists resources, **Then** `debugger://breakpoints` is NOT present.

---

### User Story 3 - Threads Resource (Priority: P2)

As an LLM client, I want to browse the list of managed threads in the debugged process as a resource so I can see thread states without calling `threads_list`.

**Why this priority**: Thread info is valuable but changes less frequently than session state or breakpoints, and is primarily useful when the process is paused.

**Independent Test**: Attach to a multi-threaded process, pause, read `debugger://threads`, verify thread list with states.

**Acceptance Scenarios**:

1. **Given** a debug session is paused, **When** an LLM reads `debugger://threads`, **Then** it receives a JSON list of managed threads with: thread ID, name (if set), state, whether it is the current/active thread, and current location (if available).

2. **Given** the debugger resumes or pauses, **Then** a resource-changed notification is emitted for `debugger://threads`.

3. **Given** the process is running (not paused), **When** an LLM reads `debugger://threads`, **Then** it receives a message indicating thread info is only available when paused, or the last known snapshot.

---

### User Story 4 - Source Code Resource (Priority: P2)

As an LLM client, I want to read source code files referenced by the debugged process (using paths from PDB symbols) as a resource so I can see the code without needing to know file paths or use `cat`.

**Why this priority**: Source browsing is high-value but depends on PDB paths being available and valid on the current machine. It's a resource template with parameters, making it slightly more complex.

**Independent Test**: Attach to a process with PDB symbols, read `debugger://source/{file}` with a valid source file path, verify source code is returned.

**Acceptance Scenarios**:

1. **Given** a debug session with loaded PDB symbols, **When** an LLM reads `debugger://source/{file}` with a valid source file path, **Then** it receives the file contents as `text/plain`.

2. **Given** a source file path from PDB that does not exist on disk, **When** an LLM reads `debugger://source/{file}`, **Then** it receives an error indicating the file is not found.

3. **Given** no debug session is active, **When** an LLM attempts to read `debugger://source/{file}`, **Then** the resource is not available.

4. **Given** a debug session is active, **When** an LLM lists resources, **Then** the resource template `debugger://source/{file}` is listed (as a template, not individual files).

---

### Edge Cases

- Resources are requested while the debugger is in the middle of attaching/launching (transitional state) — should return a "session initializing" status or wait until ready.
- Multiple rapid state changes (e.g., stepping through code quickly) — notifications should be batched or debounced to avoid flooding the client.
- Very large thread counts (hundreds of threads) — thread resource should still return within a reasonable time.
- Source file paths containing special characters or spaces — URI encoding must handle them correctly.
- Breakpoint hit count updates during running — breakpoint resource should reflect the latest hit counts.
- Session disconnect while a resource read is in progress — should return gracefully, not throw.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose `debugger://session` as an MCP Resource containing debug session state
- **FR-002**: System MUST expose `debugger://breakpoints` as an MCP Resource listing all breakpoints, tracepoints, and exception breakpoints
- **FR-003**: System MUST expose `debugger://threads` as an MCP Resource listing managed threads
- **FR-004**: System MUST expose `debugger://source/{file}` as an MCP Resource Template for reading source files referenced by PDB symbols
- **FR-005**: All debugger resources MUST only be listed when an active debug session exists
- **FR-006**: System MUST emit `notifications/resources/list_changed` when a debug session starts or ends (resources appear/disappear)
- **FR-007**: System MUST emit `notifications/resources/updated` for subscribed resources when their underlying state changes (pause/resume, breakpoint add/remove, thread state change)
- **FR-008**: System MUST declare `resources` and `resources.subscribe` capabilities in the MCP server configuration
- **FR-009**: All resource content MUST be returned as JSON (`application/json`) except source code which MUST be `text/plain`
- **FR-010**: When no session is active, listing resources MUST return an empty list (no debugger resources)
- **FR-011**: Resource reads on stale/disconnected sessions MUST return a clear error, not crash

### Key Entities

- **Debug Session Resource**: Snapshot of current session — process info, state, current location, timestamps
- **Breakpoint Resource**: Collection of all breakpoints with their full metadata (type, location, state, hit count, conditions)
- **Thread Resource**: Collection of managed threads with state and location info
- **Source Resource**: Individual source file content resolved via PDB symbol paths

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: LLM clients can discover all 4 debugger resources via `resources/list` when a session is active
- **SC-002**: LLM clients receive 0 debugger resources when no session is active
- **SC-003**: Resource subscriptions deliver change notifications within 1 second of state changes
- **SC-004**: All existing tool-based functionality continues to work unchanged (100% backward compatible)
- **SC-005**: All existing tests pass unchanged
- **SC-006**: Resource reads complete within 500ms under normal conditions (< 100 breakpoints, < 200 threads)

## Dependencies

- Feature 001 (Debug Session) — session state data
- Feature 002 (Breakpoint Ops) — breakpoint registry
- Feature 016 (Breakpoint Notifications) — tracepoint metadata
- ModelContextProtocol SDK — Resource registration and notification APIs

## Assumptions

- The ModelContextProtocol SDK (0.1.0-preview.13 or later) supports resource registration via attributes or builder methods (analogous to `WithTools`)
- MCP clients (LLM frontends) support reading resources and subscribing to notifications
- PDB source file paths stored in symbols point to files accessible on the current machine (no source server/SourceLink resolution needed for MVP)
- Change notifications use the standard MCP `notifications/resources/updated` mechanism

## Out of Scope

- Writable resources (MCP resources are read-only by definition)
- Source code decompilation when PDB source files are not on disk
- SourceLink or source server resolution for remote source files
- Resource caching or persistence across sessions
- Filtering or pagination of resource content
- Workspace/Roslyn resources (can be added in a future feature if needed)
