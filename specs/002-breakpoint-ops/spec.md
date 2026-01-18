# Feature Specification: Breakpoint Operations

**Feature Branch**: `002-breakpoint-ops`
**Created**: 2026-01-17
**Status**: Draft
**Input**: User description: "MCP tools for breakpoint operations: set, remove, list, and wait for breakpoints"
**Depends On**: 001-debug-session (requires active debug session)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Set Breakpoint at Source Location (Priority: P1)

As an AI assistant debugging a user's application, I need to set a breakpoint at a
specific line in a source file so I can pause execution when that code runs and
investigate the program state at that point.

**Why this priority**: Setting breakpoints is the most fundamental debugging
operation. Without the ability to pause at specific code locations, meaningful
debugging is impossible. This is the core capability that all other breakpoint
features build upon.

**Independent Test**: Can be tested by attaching to a process, invoking the set
breakpoint tool with a file and line number, then continuing execution. When the
line is reached, execution should pause.

**Acceptance Scenarios**:

1. **Given** a debug session is active, **When** the AI invokes `breakpoint_set`
   with a valid source file and line number, **Then** the system creates a
   breakpoint and returns its unique identifier and verification status.

2. **Given** a debug session is active, **When** the AI sets a breakpoint at a
   line that doesn't contain executable code, **Then** the system either adjusts
   to the nearest valid line or returns an error explaining why the location is
   invalid.

3. **Given** a breakpoint is set, **When** program execution reaches that line,
   **Then** the debugger pauses execution and the session state transitions to
   "paused" with reason "breakpoint".

4. **Given** a line contains multiple statements (e.g., a lambda expression),
   **When** the AI invokes `breakpoint_set` with a specific column position,
   **Then** the system creates a breakpoint at that exact sequence point,
   pausing only when that specific lambda/statement executes.

---

### User Story 2 - Wait for Breakpoint Hit (Priority: P2)

As an AI assistant, I need to wait for a breakpoint to be hit so I can respond
when execution pauses at a location of interest, enabling interactive debugging
workflows.

**Why this priority**: After setting a breakpoint, the AI needs a way to know
when it's hit. Without this, the AI would need to poll repeatedly. This enables
event-driven debugging workflows where the AI can wait for interesting events.

**Independent Test**: Can be tested by setting a breakpoint, continuing
execution, and invoking the wait tool. When the breakpoint is hit, the tool
should return with location and context information.

**Acceptance Scenarios**:

1. **Given** a debug session with breakpoints set, **When** the AI invokes
   `breakpoint_wait` with a timeout, **Then** the tool blocks until a breakpoint
   is hit or the timeout expires, returning hit information or timeout status.

2. **Given** a breakpoint is hit while waiting, **When** `breakpoint_wait`
   returns, **Then** it includes the breakpoint ID, source location, thread ID,
   and stack frame context.

3. **Given** no breakpoint is hit within the timeout period, **When** the timeout
   expires, **Then** the tool returns with a clear timeout indication, allowing
   the AI to decide whether to retry or take other action.

---

### User Story 3 - List Active Breakpoints (Priority: P3)

As an AI assistant, I need to list all active breakpoints so I can understand
which locations are being monitored and manage breakpoints effectively across a
debugging session.

**Why this priority**: Breakpoint management requires visibility into current
breakpoints. The AI may inherit breakpoints from a previous session or need to
audit what's currently set before adding or removing breakpoints.

**Independent Test**: Can be tested by setting several breakpoints, then invoking
the list tool. All set breakpoints should be returned with their details.

**Acceptance Scenarios**:

1. **Given** a debug session is active, **When** the AI invokes `breakpoint_list`,
   **Then** the system returns all breakpoints with their IDs, locations, enabled
   status, and hit counts.

2. **Given** no breakpoints are set, **When** the AI invokes `breakpoint_list`,
   **Then** the system returns an empty list with no errors.

3. **Given** some breakpoints are enabled and others disabled, **When** the AI
   invokes `breakpoint_list`, **Then** each breakpoint's enabled/disabled state
   is clearly indicated.

---

### User Story 4 - Remove Breakpoint (Priority: P4)

As an AI assistant, I need to remove breakpoints that are no longer needed so
I can clean up after debugging specific issues and prevent unwanted pauses in
subsequent execution.

**Why this priority**: While important for cleanup, removing breakpoints is less
critical than setting them. The AI can always disable breakpoints or disconnect
if removal isn't available.

**Independent Test**: Can be tested by setting a breakpoint, confirming it exists
via list, then removing it and confirming it no longer appears.

**Acceptance Scenarios**:

1. **Given** a breakpoint exists with a known ID, **When** the AI invokes
   `breakpoint_remove` with that ID, **Then** the breakpoint is removed and
   subsequent execution won't pause at that location.

2. **Given** an invalid breakpoint ID, **When** the AI invokes `breakpoint_remove`,
   **Then** the system returns an error indicating the breakpoint was not found.

3. **Given** a breakpoint is hit and execution is paused, **When** the AI removes
   that breakpoint and continues, **Then** subsequent hits at that location do
   not occur.

---

### User Story 5 - Conditional Breakpoints (Priority: P5)

As an AI assistant, I need to set breakpoints that only trigger when a condition
is met so I can debug issues that occur under specific circumstances without
stopping at every iteration.

**Why this priority**: While valuable for advanced debugging, conditional
breakpoints are an enhancement over basic breakpoints. The AI can achieve similar
results by checking conditions manually after each hit.

**Independent Test**: Can be tested by setting a conditional breakpoint in a
loop, running the code, and verifying execution only pauses when the condition
evaluates to true.

**Acceptance Scenarios**:

1. **Given** a debug session is active, **When** the AI sets a breakpoint with
   a condition expression (e.g., `i > 5`), **Then** the breakpoint only triggers
   when the condition evaluates to true.

2. **Given** a conditional breakpoint exists, **When** the AI lists breakpoints,
   **Then** the condition expression is included in the breakpoint details.

3. **Given** a condition expression has a syntax error, **When** the AI sets the
   breakpoint, **Then** the system returns an error explaining the invalid
   condition.

---

### User Story 6 - Exception Breakpoints (Priority: P6)

As an AI assistant, I need to set breakpoints that trigger when specific
exceptions are thrown so I can catch errors at their origin rather than after
they propagate up the call stack.

**Why this priority**: Exception breakpoints are valuable for debugging but
represent an alternative breakpoint type. The AI can often locate exception
sources via stack traces, making this a convenience rather than necessity.

**Independent Test**: Can be tested by setting an exception breakpoint for
`NullReferenceException`, running code that throws one, and verifying execution
pauses at the throw site.

**Acceptance Scenarios**:

1. **Given** a debug session is active, **When** the AI invokes
   `breakpoint_set_exception` with an exception type name, **Then** the system
   creates an exception breakpoint that triggers on first-chance throws.

2. **Given** an exception breakpoint exists, **When** that exception type is
   thrown, **Then** execution pauses at the throw site with exception details
   in the hit information.

3. **Given** an exception breakpoint is set for a base type, **When** a derived
   exception is thrown, **Then** the breakpoint triggers (inheritance matching).

---

### Edge Cases

- What happens when setting a breakpoint in code that hasn't been loaded yet?
  The breakpoint should be marked as "pending" and verified when the module loads.

- What happens when setting a breakpoint at the same location twice?
  The system should return the existing breakpoint ID rather than creating a
  duplicate.

- What happens when the source file path doesn't match the compiled symbols?
  The system should attempt path normalization and report clear errors if the
  file cannot be resolved.

- What happens when setting a breakpoint in optimized (release) code?
  The system should warn that breakpoint behavior may be affected by
  optimizations.

- What happens if a conditional breakpoint's expression references an undefined
  variable? The breakpoint should fail evaluation and report the error without
  crashing the debuggee.

- What happens when setting a breakpoint with a column that doesn't correspond
  to a valid sequence point? The system should return the available sequence
  points on that line (start/end columns) so the AI can choose the correct one.

- What happens when an exception breakpoint is set for a type that doesn't exist?
  The breakpoint should be created as pending and validated when assemblies load;
  if never found, it remains unverified.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `breakpoint_set` tool that creates a
  breakpoint at a specified source file, line number, and optional column
  position (for targeting lambda expressions or inline statements using PDB
  sequence points).

- **FR-002**: System MUST provide a `breakpoint_remove` tool that removes a
  breakpoint by its unique identifier.

- **FR-003**: System MUST provide a `breakpoint_list` tool that returns all
  breakpoints in the current debug session with their details.

- **FR-004**: System MUST provide a `breakpoint_wait` tool that blocks until
  a breakpoint is hit or a timeout expires.

- **FR-005**: System MUST assign a unique identifier to each breakpoint for
  later reference in remove and list operations.

- **FR-006**: System MUST track breakpoint hit counts and report them in list
  and wait responses.

- **FR-007**: System MUST support enabling and disabling breakpoints without
  removing them.

- **FR-008**: System MUST support conditional breakpoints with expression
  evaluation in the debuggee context.

- **FR-009**: System MUST handle pending breakpoints for code not yet loaded,
  verifying them when modules load.

- **FR-010**: System MUST return structured error responses when operations fail,
  including actionable error messages.

- **FR-011**: System MUST provide a `breakpoint_set_exception` tool that creates
  an exception breakpoint for a specified exception type, triggering on
  first-chance throws (using ICorDebugManagedCallback.Exception).

### Key Entities

- **Breakpoint**: Represents a pause point in code. Key attributes: unique ID,
  source location (file, line), enabled status, hit count, condition expression
  (optional), verification status.

- **BreakpointHit**: Information about a triggered breakpoint. Key attributes:
  breakpoint ID, thread ID, timestamp, source location, stack frame context.

- **BreakpointLocation**: Source code position. Key attributes: file path, line
  number, column (optional - used to target specific lambda expressions or
  inline statements; resolved via PDB sequence points), function name (optional).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: AI assistants can set a breakpoint and have it verified within
  2 seconds of invocation.

- **SC-002**: The `breakpoint_wait` tool returns within 100 milliseconds of a
  breakpoint being hit.

- **SC-003**: The system correctly handles 100% of invalid location scenarios
  with clear, actionable error messages.

- **SC-004**: Conditional breakpoints evaluate expressions accurately, only
  triggering when conditions are true.

- **SC-005**: AI assistants can complete a full breakpoint workflow (set, wait,
  inspect, remove) without requiring manual intervention.

- **SC-006**: Breakpoint operations function correctly for multi-threaded
  applications, reporting which thread triggered the breakpoint.

## Clarifications

### Session 2026-01-18

- Q: Should `breakpoint_set` support column position for lambda/inline breakpoints? → A: Yes, full column support using PDB sequence points
- Q: Should the system support exception breakpoints? → A: Yes, add as P6 priority feature for breaking on first-chance/second-chance exceptions by type

## Assumptions

- A debug session is already established (via 001-debug-session feature) before
  breakpoint operations can be used.
- Source files and debug symbols are available for the debuggee.
- The debuggee is a .NET application with standard symbol formats (PDB).
- Conditional expressions use C# syntax compatible with the debuggee context.
- Breakpoint IDs are stable within a debug session but not across sessions.
