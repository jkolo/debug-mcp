# Feature Specification: Comprehensive E2E Test Coverage

**Feature Branch**: `009-comprehensive-e2e-coverage`
**Created**: 2026-01-28
**Status**: Draft
**Input**: User description: "Expand Reqnroll E2E test suite from 28 to ~100 scenarios achieving >80% code coverage across all debugger functionality"

## Clarifications

### Session 2026-01-28

- Q: How should new test target code be organized across the TestLib projects? → A: Spread across existing TestLib projects by category. Libraries renamed from TestLib1-10 to descriptive names: BaseTypes, Collections, Exceptions, Recursion, Expressions, Threading, AsyncOps, MemoryStructs, ComplexObjects, Scenarios.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Expression Evaluation Coverage (Priority: P1)

A developer debugging a .NET application needs to evaluate expressions in the paused context — simple arithmetic, property access, method calls, null-conditional operators, and failing expressions that produce errors.

**Why this priority**: Expression evaluation (`evaluate` tool) is a core debugging activity with zero E2E coverage today. It has complex interactions with stopped state and type resolution.

**Independent Test**: Can be tested by attaching to TestTargetApp, hitting a breakpoint, and evaluating various expressions against in-scope variables.

**Acceptance Scenarios**:

1. **Given** a paused debug session at a breakpoint, **When** the user evaluates a simple arithmetic expression, **Then** the correct numeric result is returned
2. **Given** a paused debug session with local variables, **When** the user evaluates a property access expression, **Then** the property value is returned
3. **Given** a paused debug session, **When** the user evaluates a method call expression, **Then** the method return value is returned
4. **Given** a paused debug session, **When** the user evaluates a null-conditional expression on a null object, **Then** null is returned without error
5. **Given** a paused debug session, **When** the user evaluates an invalid expression, **Then** a meaningful error message is returned

---

### User Story 2 - Advanced Breakpoint Scenarios (Priority: P1)

A developer needs to use breakpoints beyond simple line breaks — exception breakpoints, conditional breakpoints, multiple simultaneous breakpoints, enable/disable toggling, and breakpoint removal.

**Why this priority**: Breakpoints are the primary debugging mechanism. Current tests cover basics but miss conditional, exception, and lifecycle scenarios.

**Independent Test**: Can be tested by setting various breakpoint types, running the target, and verifying hit behavior.

**Acceptance Scenarios**:

1. **Given** a debug session, **When** the user sets a conditional breakpoint with a condition expression, **Then** the breakpoint only triggers when the condition is true
2. **Given** a debug session, **When** the user sets an exception breakpoint for a specific exception type, **Then** execution pauses when that exception is thrown
3. **Given** multiple breakpoints are set, **When** the user disables one and continues execution, **Then** only enabled breakpoints trigger
4. **Given** multiple breakpoints are set at different locations, **When** execution continues, **Then** breakpoints hit in source order
5. **Given** a breakpoint is set, **When** the user removes it and continues, **Then** execution does not pause at that location

---

### User Story 3 - Memory and Object Inspection (Priority: P1)

A developer needs to inspect object memory layout, read raw memory, analyze object references, and inspect objects at various nesting depths — including collections, generics, null values, and deeply nested structures.

**Why this priority**: Memory and object inspection tools (`memory_read`, `object_inspect`, `references_get`, `layout_get`) have zero E2E coverage. They represent 4 of 25 tools.

**Independent Test**: Can be tested by pausing at a breakpoint where complex objects are in scope and inspecting their contents.

**Acceptance Scenarios**:

1. **Given** a paused debug session with a complex object in scope, **When** the user inspects the object with depth 1, **Then** top-level fields are returned
2. **Given** a paused debug session with a nested object, **When** the user inspects with depth 3, **Then** three levels of nested fields are returned
3. **Given** a paused debug session with a null reference, **When** the user inspects the null object, **Then** null is indicated without crash
4. **Given** a paused debug session, **When** the user reads memory at a valid address, **Then** raw bytes are returned in the requested format
5. **Given** a paused debug session with an object, **When** the user analyzes outbound references, **Then** referenced objects are listed
6. **Given** a paused debug session, **When** the user gets type layout for a struct, **Then** field offsets and sizes are returned

---

### User Story 4 - Module and Type Operations (Priority: P2)

A developer needs to search for types across modules, browse type members, filter types by namespace and kind, and search with wildcards.

**Why this priority**: Module operations (`modules_search`, `types_get`, `members_get`) have minimal E2E coverage (only listing). Type browsing and search are commonly used for exploring unfamiliar code.

**Independent Test**: Can be tested by attaching to TestTargetApp (which loads 11 modules) and searching/browsing types.

**Acceptance Scenarios**:

1. **Given** a debug session attached to TestTargetApp, **When** the user searches for types matching "*Util", **Then** all 10 Util classes are found (BaseTypesUtil, CollectionsUtil, etc.)
2. **Given** a debug session, **When** the user gets types for a specific module filtered by namespace, **Then** only matching types are returned
3. **Given** a debug session, **When** the user gets members of a type filtered by kind "methods", **Then** only methods are returned
4. **Given** a debug session, **When** the user searches with a wildcard pattern "Test*", **Then** matching types and methods are found
5. **Given** a debug session, **When** the user gets members including inherited members, **Then** base type members are included

---

### User Story 5 - Complex Stepping Scenarios (Priority: P2)

A developer needs to step through exception handlers, step across assembly boundaries, step into property getters and lambda expressions, and step out from deeply nested call chains.

**Why this priority**: Current stepping tests cover basic step-in/over/out but miss cross-assembly and exception handler stepping which are common real-world scenarios.

**Independent Test**: Can be tested by setting breakpoints before exception/property/lambda code and stepping through.

**Acceptance Scenarios**:

1. **Given** a breakpoint inside a try block that throws, **When** the user steps over the throw statement, **Then** execution lands in the catch block
2. **Given** a breakpoint in TestTargetApp calling TestLib code, **When** the user steps into the call, **Then** execution lands in the TestLib assembly source
3. **Given** execution paused at a deeply nested call (3+ frames deep), **When** the user steps out repeatedly, **Then** each step-out returns to the caller frame
4. **Given** a breakpoint before a property access, **When** the user steps into the property, **Then** execution enters the getter body

---

### User Story 6 - Stack Trace and Thread Scenarios (Priority: P2)

A developer needs to inspect stack traces in various contexts — deep recursion, multiple threads, and cross-assembly call chains. Thread listing must show all managed threads.

**Why this priority**: Stack trace tests exist but miss deep recursion and thread listing. `threads_list` tool has zero E2E coverage.

**Independent Test**: Can be tested by running code with recursion/threads and inspecting stack state.

**Acceptance Scenarios**:

1. **Given** a breakpoint inside a recursive method at depth 10, **When** the user requests stack trace, **Then** all 10+ frames are returned in correct order
2. **Given** a debug session with the target running multiple threads, **When** the user lists threads, **Then** all managed threads are shown
3. **Given** a cross-assembly call chain (TestTargetApp → Scenarios → ComplexObjects), **When** the user requests stack trace, **Then** frames from multiple assemblies are shown

---

### User Story 7 - Session Management Edge Cases (Priority: P2)

A developer encounters edge cases in session management — disconnecting during execution, process exit during debugging, state queries at various session stages, and pause/continue operations.

**Why this priority**: Session lifecycle tests cover happy paths but miss error recovery and edge cases that affect reliability.

**Independent Test**: Can be tested by deliberately triggering error conditions and verifying graceful handling.

**Acceptance Scenarios**:

1. **Given** no active debug session, **When** the user queries debug state, **Then** a "no session" state is returned
2. **Given** an active debug session, **When** the target process exits normally, **Then** the session reports process terminated
3. **Given** a disconnected session, **When** the user attempts to set a breakpoint, **Then** a meaningful error is returned
4. **Given** a running (not paused) debug session, **When** the user pauses execution, **Then** the process stops and state becomes paused

---

### User Story 8 - Multi-Step Debugging Workflows (Priority: P3)

A developer performs complex debugging workflows that combine multiple operations — set breakpoint, hit it, inspect variables, step, inspect again, evaluate expression, continue to next breakpoint.

**Why this priority**: Real debugging is a multi-step workflow. Testing individual operations alone misses integration issues between steps.

**Independent Test**: Can be tested as end-to-end scenarios that chain multiple debugger operations.

**Acceptance Scenarios**:

1. **Given** a debug session, **When** the user sets a breakpoint, continues, inspects variables at the hit, steps over, and inspects again, **Then** variable values reflect the step
2. **Given** a debug session with two breakpoints, **When** the user continues past the first and stops at the second, **Then** variables at the second location are correct
3. **Given** a paused session, **When** the user evaluates an expression, modifies a variable via evaluate, then inspects, **Then** the modified value is reflected

---

### User Story 9 - Variable Inspection Edge Cases (Priority: P2)

A developer inspects variables with complex types — collections (List, Dictionary), generic types, large arrays, strings with special characters, and enum values.

**Why this priority**: Current variable inspection tests cover basic types but miss complex .NET type system scenarios.

**Independent Test**: Can be tested by pausing at code with complex variable types in scope.

**Acceptance Scenarios**:

1. **Given** a paused debug session with a List in scope, **When** the user inspects variables, **Then** the collection type and count are shown
2. **Given** a Dictionary in scope, **When** the user inspects it, **Then** key-value pairs are accessible
3. **Given** a string containing special characters (newlines, unicode), **When** the user inspects it, **Then** the full string value is returned correctly
4. **Given** an enum variable in scope, **When** the user inspects it, **Then** the enum name and numeric value are shown
5. **Given** a nullable value type that is null, **When** the user inspects it, **Then** null is indicated

---

### Edge Cases

- What happens when evaluating an expression that causes a side effect (modifies state)?
- How does the debugger handle stepping when JIT optimization inlines a method?
- What happens when reading memory at an invalid/unmapped address?
- How does breakpoint_wait behave when no breakpoint is ever hit (timeout)?
- What happens when listing variables in a frame with no locals (e.g., native frame)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Test suite MUST cover all 25 MCP tools with at least one E2E scenario each
- **FR-002**: Test suite MUST achieve >80% code coverage of the core debugger service
- **FR-003**: Test suite MUST include at least 90 Reqnroll scenarios total (current 28 + ~62 new)
- **FR-004**: Tests MUST use existing Reqnroll/Gherkin infrastructure in the E2E test project
- **FR-005**: Tests MUST reuse existing step definitions where applicable and add new ones only as needed
- **FR-006**: Tests MUST run serially due to single-session debugging limitation
- **FR-007**: TestTargetApp MUST be extended with additional source files for scenarios requiring specific code constructs, placed in the categorized library projects: BaseTypes (enums, structs), Collections (List, Dict, arrays), Exceptions (try/catch/throw), Recursion (recursive methods), Expressions (eval targets), Threading (thread scenarios), AsyncOps (async operations), MemoryStructs (structs, layout), ComplexObjects (deeply nested), Scenarios (top-level entry)
- **FR-008**: New test target code MUST be placed in the appropriate categorized library project matching the scenario domain
- **FR-009**: Tests MUST handle cleanup (disconnect/terminate) even when assertions fail
- **FR-010**: Error-path tests MUST verify that meaningful error messages are returned (not crashes or hangs)

### Key Entities

- **Test Scenario**: A Gherkin scenario exercising one or more debugger operations against the test target
- **Step Definition**: Reusable method implementing a Given/When/Then step, shared across scenarios
- **Test Target Code**: Source code in TestTargetApp specifically designed to exercise debugger features (e.g., recursive methods, collection initialization, exception throwing)
- **DebuggerContext**: Shared state object holding session state, last results, and test target process info

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Total E2E scenario count reaches 90 or more (from current 28)
- **SC-002**: All 25 debugger tools have at least one dedicated E2E test scenario
- **SC-003**: Code coverage of the core debugger service exceeds 80%
- **SC-004**: All E2E tests pass consistently (no flaky tests) in both Debug and Release configurations
- **SC-005**: Test execution completes within 5 minutes total for the full E2E suite
- **SC-006**: No test leaves orphaned debugger processes after completion

## Assumptions

- TestTargetApp can be extended with new source files for specific test scenarios without impacting existing tests
- The debugger supports all inspection operations described when the process is in a stopped state
- Expression evaluation supports basic C# expressions (arithmetic, property access, method calls)
- Memory read operations work on managed heap addresses obtained from object inspection
- The existing Reqnroll test infrastructure (hooks, context injection, process management) is sufficient for the expanded test suite
