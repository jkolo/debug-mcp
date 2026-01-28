# Feature Specification: Reqnroll E2E Tests

**Feature Branch**: `008-reqnroll-e2e-tests`
**Created**: 2026-01-28
**Status**: Draft
**Input**: User description: "Dodanie testów E2E z użyciem Reqnroll (następca SpecFlow) do projektu DotnetMcp. Testy powinny być pisane w Gherkin (Given/When/Then) i pokrywać główne scenariusze debuggera: attach/detach, breakpointy, stepping, inspekcję zmiennych, launch. Feature files w naturalnym języku angielskim."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Debug Session Lifecycle (Priority: P1)

A developer using the MCP debugger can attach to a running .NET process, perform debugging operations, and cleanly detach. This is the foundational scenario — without session management, no other debugging features work.

**Why this priority**: Session lifecycle (attach/detach/launch) is the prerequisite for all other debugger features. Every other scenario depends on having an active debug session.

**Independent Test**: Can be fully tested by launching or attaching to a test target process, verifying the session is active, and disconnecting — delivers confidence that the debugger can establish and tear down sessions.

**Acceptance Scenarios**:

1. **Given** a running .NET process, **When** the debugger attaches to it by PID, **Then** the session state becomes "attached" and the process continues running
2. **Given** an active debug session, **When** the debugger detaches, **Then** the session state becomes "disconnected" and the target process continues running independently
3. **Given** a .NET application DLL path, **When** the debugger launches it with stop-at-entry enabled, **Then** the session state becomes "paused" at the entry point
4. **Given** a launched paused session, **When** the debugger continues execution, **Then** the session state becomes "running"

---

### User Story 2 - Breakpoint Management (Priority: P1)

A developer can set breakpoints at specific source locations, have the debugger pause when execution reaches those locations, and manage breakpoint lifecycle (set, hit, remove). Conditional breakpoints based on hit count are also supported.

**Why this priority**: Breakpoints are the primary mechanism for controlling program execution during debugging. They are essential to virtually every debugging workflow.

**Independent Test**: Can be tested by attaching to a test target, setting a breakpoint on a known source line, triggering execution of that line, and verifying the debugger pauses at the correct location.

**Acceptance Scenarios**:

1. **Given** an active debug session, **When** a breakpoint is set on a specific source file and line, **Then** the breakpoint is registered and reported as "bound"
2. **Given** a bound breakpoint, **When** the target process executes the breakpoint line, **Then** the debugger pauses and reports the breakpoint hit with correct location
3. **Given** a conditional breakpoint with "hitCount == 3", **When** the breakpoint location is executed 5 times, **Then** the debugger only pauses on the 3rd execution
4. **Given** a bound breakpoint, **When** the breakpoint is removed, **Then** subsequent execution of that line does not pause the debugger

---

### User Story 3 - Stepping Through Code (Priority: P2)

A developer paused at a breakpoint can step through code line-by-line using step-over, step-into, and step-out operations to understand program flow.

**Why this priority**: Stepping is the second most common debugging operation after breakpoints. It allows fine-grained control of execution flow once paused.

**Independent Test**: Can be tested by pausing at a breakpoint, performing step-over, and verifying the debugger moves to the next line in the same method.

**Acceptance Scenarios**:

1. **Given** a paused debug session at a breakpoint, **When** a step-over operation is performed, **Then** the debugger pauses at the next line in the current method
2. **Given** a paused session at a line with a method call, **When** a step-into operation is performed, **Then** the debugger pauses at the first line inside the called method
3. **Given** a paused session inside a called method, **When** a step-out operation is performed, **Then** the debugger pauses at the line after the call site in the calling method

---

### User Story 4 - Variable and Object Inspection (Priority: P2)

A developer paused at a breakpoint can inspect local variables, method arguments, and object fields to understand the current program state.

**Why this priority**: Inspection is the primary reason developers use debuggers — to see what values variables hold at a specific point in execution.

**Independent Test**: Can be tested by pausing at a breakpoint in a method with known variables, requesting variable inspection, and verifying correct names, types, and values.

**Acceptance Scenarios**:

1. **Given** a paused debug session, **When** local variables are requested, **Then** the debugger returns variable names, types, and values for the current frame
2. **Given** a paused session with an object variable, **When** the object is inspected, **Then** the debugger returns all field names, types, and values of the object
3. **Given** a paused session, **When** a C# expression is evaluated, **Then** the debugger returns the expression result with its type and value

---

### User Story 5 - Stack Trace Inspection (Priority: P3)

A developer paused at a breakpoint can view the call stack to understand how execution arrived at the current location.

**Why this priority**: Stack traces provide essential context for understanding program flow and are frequently used alongside variable inspection.

**Independent Test**: Can be tested by pausing at a breakpoint inside a nested method call and verifying the stack trace contains all expected frames with correct method names and locations.

**Acceptance Scenarios**:

1. **Given** a paused debug session, **When** a stack trace is requested, **Then** the debugger returns ordered frames with method names and source locations
2. **Given** a stack trace with multiple frames, **When** a specific frame is selected, **Then** variables from that frame's scope are accessible for inspection

---

### Edge Cases

- What happens when a feature file references a source file that doesn't exist in the test target?
- How does the test behave when the target process exits unexpectedly during a scenario?
- What happens when multiple breakpoints are set on the same line?
- How does stepping behave at the end of a method (last line)?
- What happens when evaluating an expression that throws an exception?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a Reqnroll test project with Gherkin feature files written in English
- **FR-002**: Feature files MUST use Given/When/Then syntax to describe debugger scenarios in natural language
- **FR-003**: Step definitions MUST bind Gherkin steps to the existing debugger API (session management, breakpoint management, process debugging)
- **FR-004**: Tests MUST cover session lifecycle scenarios: attach, detach, launch with stop-at-entry, continue after launch
- **FR-005**: Tests MUST cover breakpoint scenarios: set, hit, conditional (hit count), remove
- **FR-006**: Tests MUST cover stepping scenarios: step-over, step-into, step-out
- **FR-007**: Tests MUST cover inspection scenarios: local variables, object fields, expression evaluation, stack trace
- **FR-008**: Tests MUST use the existing test target application as the process being debugged
- **FR-009**: Each Gherkin scenario MUST be independently executable (no ordering dependencies between scenarios)
- **FR-010**: Step definitions MUST share test infrastructure (process lifecycle, session management) via Reqnroll hooks or dependency injection

### Key Entities

- **Feature File**: A `.feature` file containing Gherkin scenarios that describe debugger behavior in natural language
- **Step Definition**: Code that binds Gherkin steps (Given/When/Then) to actual debugger operations
- **Test Target**: The existing test target application used as the process being debugged during E2E tests
- **Scenario Context**: Shared state within a single scenario (active session, set breakpoints, current pause location)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All Gherkin scenarios pass when executed against the debugger
- **SC-002**: Feature files are readable by a non-developer and accurately describe debugger behavior
- **SC-003**: At least 5 feature files cover the 5 main debugger areas (session lifecycle, breakpoints, stepping, inspection, stack traces)
- **SC-004**: Each scenario completes within 30 seconds
- **SC-005**: Adding a new test scenario for an existing feature area requires only writing Gherkin steps (no new infrastructure code)

## Assumptions

- Reqnroll is the chosen BDD framework (successor to SpecFlow, compatible with current .NET version)
- Feature files are written in English as specified by the user
- The existing test target application and its command-based interface (stdin commands) are sufficient as test targets
- Tests run in the same CI/testing environment as existing integration tests
- Scenarios within a single feature file share a test target process but each scenario gets a fresh debug session
