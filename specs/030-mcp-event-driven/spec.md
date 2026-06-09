# Feature Specification: MCP Event-Driven Debugger Interface

**Feature Branch**: `030-mcp-event-driven`  
**Created**: 2026-06-09  
**Status**: Draft  

## Overview

AI debugging agents that use debug-mcp must today actively ask the server for information: "has a breakpoint been hit?", "what is the current state?", "what threads are running?". This polling model wastes tokens, introduces timing uncertainty, and forces agents to make multiple round-trips just to understand what happened. This feature replaces polling with an event-driven model: the server notifies the agent when something happens, and static information is always available as subscribable data sources (resources) rather than requiring explicit tool calls.

---

## User Scenarios & Testing

### User Story 1 — Receiving Breakpoint Hit Without Polling (Priority: P1)

An AI agent sets a breakpoint in the code it is debugging and resumes execution. Currently, the agent must call a dedicated waiting tool with a timeout, blocking until the breakpoint is hit or the timeout expires. If the timeout is too short, the agent misses the event; if too long, it blocks unnecessarily.

In the new model, the agent simply resumes execution. When the breakpoint is hit, the server automatically sends a notification to the agent containing: the breakpoint identity, the code location, the current thread, the hit count, and the top stack frame with local variables. The agent can immediately inspect or step through code without any additional calls to gather context.

**Why this priority**: Breakpoint hit detection is the most fundamental operation in interactive debugging. It is the bottleneck that forces all current agent workflows into a polling pattern.

**Independent Test**: Set a breakpoint, resume execution in a test program, and verify that a notification arrives with the correct location, thread, and at least one local variable value — without the agent calling any polling tool.

**Acceptance Scenarios**:

1. **Given** an agent has set a breakpoint on a code line, **When** the program execution reaches that line, **Then** the agent receives a notification containing the breakpoint ID, source file, line number, thread ID, hit count, and local variables from the current execution frame.

2. **Given** a breakpoint is set with a condition expression, **When** the condition evaluates to false, **Then** no notification is sent and execution continues; when the condition evaluates to true, a notification is sent.

3. **Given** an agent has set a tracepoint (non-blocking observation), **When** the program execution reaches it, **Then** the agent receives a notification with the evaluated log message and location; program execution is not paused.

4. **Given** an exception breakpoint is configured for a specific exception type, **When** that exception is thrown, **Then** the agent receives a notification with the exception type, message, and stack location.

5. **Given** the notification arrives, **When** the agent examines it, **Then** the notification payload is self-sufficient: the agent does not need additional calls to determine what happened and where.

---

### User Story 2 — Reading Debugger State via Always-Available Data Sources (Priority: P2)

An AI agent needs to know the current list of breakpoints, which threads are running, what modules are loaded, and which variable snapshots exist. Currently these all require separate tool calls. If the agent wants to check multiple things, it makes multiple calls sequentially.

In the new model, this information is available as subscribable data sources: the agent reads them directly, and when the data changes (a breakpoint is added, a module loads, a snapshot is created), the agent receives an automatic update notification. The agent no longer needs a dedicated "list breakpoints" call; it reads the breakpoints data source and subscribes to receive changes.

**Why this priority**: Reducing redundant tool calls cuts token usage and simplifies agent logic. Replacing four tools with four data sources provides the same information with less overhead.

**Independent Test**: Subscribe to the breakpoints data source, add a breakpoint, and verify that an automatic update notification arrives — without the agent calling any listing tool.

**Acceptance Scenarios**:

1. **Given** an agent reads the breakpoints data source, **Then** it receives the complete current list of breakpoints, tracepoints, and exception breakpoints with their states.

2. **Given** an agent reads the session state data source, **Then** it receives the current session status (running, paused, disconnected) along with the active thread and current code location when paused.

3. **Given** an agent reads the threads data source, **Then** it receives the list of all threads with their current state and location.

4. **Given** an agent reads the modules data source, **Then** it receives the list of all loaded code modules with their version, path, and symbol availability.

5. **Given** an agent reads the snapshots data source, **Then** it receives the list of previously captured variable snapshots available for comparison.

6. **Given** an agent subscribes to any of these data sources, **When** the data changes (breakpoint added/removed, module loaded, snapshot created/deleted), **Then** the agent receives an update notification automatically.

---

### User Story 3 — Receiving Session State Changes Without Polling (Priority: P3)

When a debugger pauses — because of a step completion, a breakpoint hit, an unhandled exception, or explicit pause — an agent currently has no way to know this has happened unless it calls a polling tool. This means agents either constantly poll (expensive) or miss state transitions.

In the new model, the server sends a notification whenever the session state changes: launching, running, pausing, stepping, disconnecting. The agent can react immediately to state transitions without any polling.

**Why this priority**: Session state transitions are the control flow of debugging. Without knowing when the session pauses or resumes, agents cannot coordinate multi-step debugging workflows reliably.

**Independent Test**: Resume execution after a step, and verify that a state-changed notification arrives with the new state and current location — without the agent polling.

**Acceptance Scenarios**:

1. **Given** the agent calls the step-over action, **When** the step completes and the program pauses at the next line, **Then** the agent receives a notification with state=paused, the new code location, and the reason for pausing.

2. **Given** the agent launches a debugging session, **When** the process starts, **Then** the agent receives a notification with state=running and the process information.

3. **Given** the debugged process exits, **Then** the agent receives a notification with state=disconnected and the exit reason.

4. **Given** an exception causes an unhandled crash, **Then** the agent receives a notification with state=paused and pause_reason=unhandled_exception including the exception type.

---

### User Story 4 — Process I/O Behaves Correctly in Async Agent Contexts (Priority: P4)

AI agents that work in concurrent or asynchronous environments sometimes run into subtle bugs when tools claim to be asynchronous but actually block. The read-output and write-input tools for the debugged process currently have this problem: they are declared as asynchronous but execute synchronously under the hood.

In the new model, these tools behave honestly: either they are genuinely asynchronous (using proper I/O waiting), or they declare themselves synchronous. Agent frameworks that schedule tool calls based on their declared behavior will no longer be misled.

**Why this priority**: This is a correctness issue that only matters in specific agent frameworks. It does not affect single-threaded or sequential agent implementations, which is why it is lowest priority.

**Independent Test**: Call the read-output tool in a concurrently-executing agent context and verify that other concurrent operations are not blocked while it executes.

**Acceptance Scenarios**:

1. **Given** a running process produces output, **When** the agent calls the read-output tool, **Then** the operation completes correctly whether executed synchronously or awaited asynchronously.

2. **Given** the agent writes input to the process, **Then** the operation completes correctly in both synchronous and asynchronous calling contexts.

---

### Edge Cases

- What happens when a breakpoint is hit but the notification delivery fails? Execution must not be silently continued — the debugger should remain paused, and delivery should be retried or the failure logged.
- What happens when an agent subscribes to a data source after multiple changes have already occurred? The agent should receive the current snapshot of the data immediately upon subscription.
- What happens when breakpoint notifications arrive faster than the agent can process them (e.g., a high-frequency loop)? The notification queue must not grow unboundedly; a maximum queue depth should be enforced with logging when events are dropped.
- What happens when a notification payload includes a locals snapshot but evaluating variables times out? The notification should be delivered with partial data (location, hit count) rather than not delivered at all.
- What happens when a module loads during the instant between an agent reading the modules data source and subscribing to updates? No modules should be silently lost — the subscription must cover the full history from the last read.

---

## Requirements

### Functional Requirements

**Breakpoint Event Notifications**

- **FR-001**: When a blocking breakpoint is hit, the server MUST automatically send a notification to the connected client containing: breakpoint ID, source file and line, thread ID, hit count, timestamp, and the top stack frame's local variables (where evaluation is possible within the configured timeout).
- **FR-002**: When a tracepoint is hit, the server MUST automatically send a notification containing: tracepoint ID, source file and line, thread ID, hit count, timestamp, and the evaluated log message.
- **FR-003**: When an exception breakpoint matches a thrown exception, the server MUST automatically send a notification containing: breakpoint ID, exception type, exception message, and the throwing source location.
- **FR-004**: The `breakpoint_wait` tool MUST be removed from the server's tool list. Agents relying on polling MUST migrate to the notification-based model.
- **FR-005**: The `breakpoint_list` tool MUST be removed from the server's tool list. Agents reading the breakpoint list MUST use the `debugger://breakpoints` data source.

**Session State Data Sources**

- **FR-006**: The server MUST provide the current session state as a readable, subscribable data source (`debugger://session`) containing: connection status, current execution state, active thread ID, current code location, and process information.
- **FR-007**: The server MUST provide the current thread list as a readable, subscribable data source (`debugger://threads`) containing: thread IDs, names, states, and current locations.
- **FR-008**: The `debug_state` tool MUST be removed from the server's tool list. Agents reading session state MUST use the `debugger://session` data source.
- **FR-009**: The `threads_list` tool MUST be removed from the server's tool list. Agents reading thread information MUST use the `debugger://threads` data source.

**New Data Sources**

- **FR-010**: The server MUST provide the list of loaded modules as a readable, subscribable data source (`debugger://modules`) containing: module name, file path, version, and symbol availability status.
- **FR-011**: The `debugger://modules` data source MUST automatically update and notify subscribers when a module is loaded or unloaded during execution.
- **FR-012**: The `modules_list` tool MUST be removed. Agents reading module information MUST use `debugger://modules`.
- **FR-013**: The server MUST provide the list of variable snapshots as a readable, subscribable data source (`debugger://snapshots`) containing: snapshot IDs, labels, timestamps, and variable counts.
- **FR-014**: The `debugger://snapshots` data source MUST automatically update and notify subscribers when a snapshot is created or deleted.
- **FR-015**: The `snapshot_list` tool MUST be removed. Agents reading snapshot information MUST use `debugger://snapshots`.

**Session State Change Notifications**

- **FR-016**: The server MUST send a notification whenever the session state changes (launching, running, paused, stepping, disconnecting), containing: the new state, the reason for the change (breakpoint hit, step complete, exception, user pause, process exit), and the current code location when paused.

**Process I/O Correctness**

- **FR-017**: The `process_read_output` and `process_write_input` tools MUST have consistent async behavior: either both perform genuine asynchronous I/O, or both declare themselves synchronous. Mixed or misleading async declarations are not permitted.

### Key Entities

- **Notification**: A server-initiated message sent to the connected client without the client requesting it. Contains an event type identifier and a structured payload specific to that event type.
- **Data Source (Resource)**: A named, stable endpoint that clients can read at any time to get current data, and optionally subscribe to for automatic change notifications.
- **Breakpoint Hit Payload**: The data accompanying a breakpoint-hit notification — location, thread, hit count, and local variables from the current frame.
- **Session State Change Payload**: The data accompanying a session-state-changed notification — new state, change reason, and current location.
- **Module**: A unit of compiled code loaded into the debugged process. Has a name, file path, version, and optional debug symbols.
- **Snapshot**: A captured point-in-time copy of variable values from a paused execution frame, stored in the server's memory for later comparison.

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: An AI agent can complete a full debug cycle (set breakpoint → resume → receive hit notification → inspect locals → step → receive step notification) using zero polling tool calls — all state changes arrive via notifications.
- **SC-002**: The total number of server-side tools decreases from 41 to 35, with all removed tools replaced by data sources or existing notifications.
- **SC-003**: All four newly defined or confirmed data sources (`debugger://breakpoints`, `debugger://session`, `debugger://threads`, `debugger://modules`, `debugger://snapshots`) are readable and subscribable; clients receive an automatic update notification within 500 ms of the underlying data changing.
- **SC-004**: Breakpoint hit notifications include local variable data sufficient for an agent to diagnose a bug without any follow-up inspection calls in at least 80% of typical debugging scenarios (single-frame inspection of primitive and simple reference types).
- **SC-005**: The `process_read_output` and `process_write_input` tools produce correct results when called from both synchronous and asynchronous calling contexts without blocking other concurrent operations.
- **SC-006**: All existing test suites pass after the removal of the six tools, with updated tool-count assertions and new tests covering notification delivery for all three breakpoint types.

---

## Assumptions

- The existing `debugger/breakpointHit` notification mechanism (already implemented and sending notifications via an internal queue) is the foundation for FR-001 through FR-003; this feature completes it by removing the polling tool that competed with it.
- The `debugger://session`, `debugger://threads`, and `debugger://breakpoints` data sources already exist and are implemented; this feature removes the duplicate tools and adds the two missing data sources.
- Agent clients that currently use the removed tools will need to be updated; backward compatibility is not required — the removed tools have no grace period.
- Local variable evaluation during breakpoint hit notifications is best-effort: if evaluation times out or fails, the notification is still sent with partial data. The maximum evaluation budget per notification is bounded at 100 ms.
- The `debugger://modules` data source scopes to currently loaded modules only; historical module load/unload events are not replayed on subscription.

---

## Out of Scope

- Extracting code analysis tools (`code_find_usages`, `code_goto_definition`, etc.) into a separate MCP server — identified as a future improvement but excluded from this feature.
- Implementing inbound reference traversal in `references_get` — noted as incomplete but not related to the event-driven refactoring.
- Streaming output from `process_read_output` as a real-time event stream — this feature only fixes the async declaration inconsistency, not the buffered-output model.
- Changes to the MCP SDK version — currently at 1.3.0, no upgrade needed for this feature.
