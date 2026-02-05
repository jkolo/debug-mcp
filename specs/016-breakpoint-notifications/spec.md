# Feature Specification: MCP Breakpoint Notifications

**Feature Branch**: `016-breakpoint-notifications`
**Created**: 2026-02-05
**Status**: Complete
**Input**: User description: "Breakpoints that hit should not only be returned by breakpoint_wait tool, but also send MCP notification when a breakpoint is hit. Additionally add notify-only breakpoints (tracepoints) that don't block execution but send notification when code passes through, including a custom log message that can contain evaluate expressions."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Receive MCP Notification on Breakpoint Hit (Priority: P1)

As an LLM agent debugging a .NET application, I want to receive MCP notifications when any breakpoint is hit, so that I can be informed about breakpoint events without actively polling `breakpoint_wait`.

**Why this priority**: This is the core enhancement - enabling push-based notifications for existing breakpoint functionality. Currently LLM agents must call `breakpoint_wait` and block; with notifications, agents can work on other tasks and react when breakpoints fire.

**Independent Test**: Set a breakpoint, continue execution, verify MCP notification is received when breakpoint hits. The agent does NOT need to call `breakpoint_wait` to be informed.

**Acceptance Scenarios**:

1. **Given** a debug session with an active breakpoint, **When** the debuggee executes the breakpoint location, **Then** an MCP notification is sent containing breakpoint ID, location (file, line, column), thread ID, and timestamp.

2. **Given** a debug session with multiple breakpoints, **When** one breakpoint is hit, **Then** only that specific breakpoint's notification is sent (not all breakpoints).

3. **Given** a debug session where `breakpoint_wait` is also called, **When** a breakpoint is hit, **Then** both the notification is sent AND `breakpoint_wait` returns the result (they are not mutually exclusive).

---

### User Story 2 - Create Notify-Only Tracepoints (Priority: P1)

As an LLM agent, I want to set "tracepoints" that don't pause execution but send notifications when code passes through, so that I can monitor code flow without interrupting the application.

**Why this priority**: This is the second core feature - allowing non-blocking observation points. Essential for monitoring loops, frequently-called methods, or production-like debugging scenarios.

**Independent Test**: Set a tracepoint on a line inside a loop, run the application, verify multiple notifications received without execution pausing.

**Acceptance Scenarios**:

1. **Given** a running debug session, **When** I create a tracepoint at a specific location, **Then** the tracepoint is registered and returns a unique ID.

2. **Given** a tracepoint is set, **When** execution passes through that location, **Then** an MCP notification is sent and execution continues immediately (no pause).

3. **Given** a tracepoint inside a loop that runs 10 times, **When** the loop executes, **Then** 10 separate notifications are sent (one per iteration).

4. ~~**Given** a tracepoint is set, **When** I want to convert it to a regular breakpoint, **Then** I can update its type to blocking.~~ — **Future enhancement**: Type conversion not in scope for 016.

---

### User Story 3 - Include Custom Log Message with Evaluate Expressions in Tracepoint (Priority: P1)

As an LLM agent, I want to specify a custom log message template for tracepoints that can include evaluated expressions, so that I can capture variable values and context when code passes through.

**Why this priority**: This completes the tracepoint functionality by allowing meaningful context capture. Without expression evaluation, tracepoints would only report "code passed here" without useful debugging information.

**Independent Test**: Create a tracepoint with log message "Counter is {i}, sum is {sum}", run code, verify notifications contain evaluated values.

**Acceptance Scenarios**:

1. **Given** a tracepoint with log message "Value is {myVar}", **When** execution passes through with myVar=42, **Then** notification contains "Value is 42".

2. **Given** a tracepoint with multiple expressions "x={x}, y={y}, result={x+y}", **When** execution passes through, **Then** all expressions are evaluated and included in the notification.

3. **Given** a tracepoint with an expression that throws (e.g., null reference), **When** execution passes through, **Then** notification is still sent with error indicator for that expression (e.g., "Value is <error: NullReferenceException>").

4. **Given** a tracepoint with log message containing method calls "List count: {myList.Count}", **When** execution passes through, **Then** the method is evaluated and result included.

---

### User Story 4 - Manage Tracepoint Lifecycle (Priority: P2)

As an LLM agent, I want to list, enable/disable, and remove tracepoints just like regular breakpoints, so that I have full control over observation points.

**Why this priority**: Essential for usability but depends on US2 being implemented first. Tracepoints should integrate with existing breakpoint management.

**Independent Test**: Create tracepoint, list breakpoints (should include tracepoints), disable tracepoint, verify no notifications, re-enable, verify notifications resume.

**Acceptance Scenarios**:

1. **Given** existing tracepoints, **When** I call `breakpoint_list`, **Then** tracepoints are included with a type indicator distinguishing them from blocking breakpoints.

2. **Given** an active tracepoint, **When** I disable it, **Then** no notifications are sent when code passes through.

3. **Given** a disabled tracepoint, **When** I enable it, **Then** notifications resume when code passes through.

4. **Given** a tracepoint, **When** I remove it, **Then** it is deleted and no longer appears in the list.

---

### User Story 5 - Filter Notification Frequency (Priority: P3)

As an LLM agent, I want to optionally limit how often tracepoint notifications are sent, so that I don't get overwhelmed by notifications from hot code paths.

**Why this priority**: Nice-to-have for usability in high-frequency scenarios. Can be implemented after core functionality is stable.

**Independent Test**: Create tracepoint with hit count condition "every 100th hit", run loop 500 times, verify only 5 notifications received.

**Acceptance Scenarios**:

1. **Given** a tracepoint with hit_count_multiple=100, **When** execution passes through 500 times, **Then** only 5 notifications are sent (at hits 100, 200, 300, 400, 500).

2. **Given** a tracepoint with max_notifications=10, **When** execution passes through 100 times, **Then** only first 10 notifications are sent, then tracepoint auto-disables.

---

### Edge Cases

- What happens when tracepoint expression evaluation times out? → Notification is sent with timeout error for that expression, execution continues.
- What happens when debuggee exits while tracepoint evaluation is in progress? → Pending notifications are discarded gracefully.
- What happens when MCP connection is lost during notification? → Notifications are dropped (fire-and-forget semantics).
- What happens with tracepoints in optimized code where line doesn't exist? → Tracepoint creation fails with appropriate error.
- What happens when hundreds of tracepoints fire simultaneously? → Notifications are queued and sent asynchronously to avoid blocking debuggee.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST send MCP notification when any blocking breakpoint is hit, containing: breakpoint ID, file path, line number, thread ID, and timestamp.
- **FR-002**: System MUST support creating tracepoints (notify-only breakpoints) that do not pause execution.
- **FR-003**: System MUST allow tracepoints to include a log message template with embedded expressions in `{expression}` syntax.
- **FR-004**: System MUST evaluate expressions in tracepoint log messages in the context of the current stack frame.
- **FR-005**: System MUST handle expression evaluation errors gracefully, including the error in the notification rather than failing silently.
- **FR-006**: System MUST include tracepoints in `breakpoint_list` results with a type indicator.
- **FR-007**: System MUST allow tracepoints to be enabled, disabled, and removed using existing breakpoint management tools.
- **FR-008**: System MUST support optional hit count filtering for tracepoints (notify every Nth hit).
- **FR-009**: System MUST support optional maximum notification limit for tracepoints.
- **FR-010**: System MUST send notifications asynchronously to avoid blocking the debuggee execution.
- **FR-011**: System MUST continue sending `breakpoint_wait` responses in addition to notifications (backward compatibility).

### Key Entities

- **Tracepoint**: A non-blocking observation point at a code location. Contains: ID, file, line, column, log message template, enabled state, hit count filter, max notifications.
- **BreakpointNotification**: MCP notification payload containing: breakpoint/tracepoint ID, type (blocking/tracepoint), location, thread ID, timestamp, evaluated log message (for tracepoints).
- **~~EvaluatedExpression~~**: *(Not materialized as a model — expression results are inlined into the log message string.)*

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: MCP notifications for breakpoint hits are delivered within 100ms of the breakpoint being triggered.
- **SC-002**: Tracepoint execution overhead adds less than 5ms per hit (excluding expression evaluation time).
- **SC-003**: Expression evaluation in tracepoints completes within the same timeout as the existing `evaluate` tool.
- **SC-004**: System can handle at least 100 tracepoint notifications per second without dropping notifications or degrading debuggee performance.
- **SC-005**: All existing breakpoint tests continue to pass (backward compatibility maintained).
- **SC-006**: Tracepoint with 3 expressions evaluates and sends notification in under 500ms total.

## Assumptions

- MCP protocol supports server-to-client notifications (verified: MCP SDK has notification support).
- Expression evaluation reuses existing `evaluate` tool infrastructure.
- Tracepoints use the same underlying ICorDebug breakpoint mechanism but with different callback handling.
- Log message template syntax uses `{expression}` similar to C# string interpolation for familiarity.
