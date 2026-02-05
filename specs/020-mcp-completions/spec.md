# Feature Specification: MCP Completions for Debugger Expressions

**Feature Branch**: `020-mcp-completions`
**Created**: 2026-02-06
**Status**: Draft
**Input**: User description: "przygotujmy mcp completions" (prepare MCP completions for expression autocompletion)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Variable Name Completion (Priority: P1)

An LLM client is helping a user debug their .NET application. The user wants to examine a variable but doesn't remember its exact name. The LLM uses MCP completion to get suggestions for variable names available in the current scope, providing accurate completions without hallucinating non-existent variables.

**Why this priority**: This is the core use case — variable names are the most commonly needed completions when writing evaluation expressions. Without this, LLM clients must guess variable names, leading to errors.

**Independent Test**: Attach to a paused process, request completions for an empty or partial variable name prefix, verify returned completions match actual variables in the current stack frame scope.

**Acceptance Scenarios**:

1. **Given** a debug session is paused at a breakpoint, **When** the LLM requests completions for prefix "" (empty), **Then** the system returns all local variables, parameters, and `this` (if in instance method).

2. **Given** a debug session is paused with variables `customer`, `customerId`, `count`, **When** the LLM requests completions for prefix "cust", **Then** the system returns `customer` and `customerId` but not `count`.

3. **Given** no active debug session, **When** the LLM requests completions, **Then** the system returns an empty completion list (not an error).

---

### User Story 2 - Object Member Completion (Priority: P1)

An LLM client needs to access a property or method of an object during debugging. After typing the object name and a dot, the LLM requests completions for available members (properties, fields, methods) of that object's type.

**Why this priority**: Equally critical as variable completion — users frequently need to drill into object properties. This prevents LLM from guessing member names that don't exist.

**Independent Test**: With a paused session and an object variable, request completions for "objectName." and verify returned members match the object's actual type members.

**Acceptance Scenarios**:

1. **Given** a variable `user` of type `User` with properties `Name`, `Email`, `Id`, **When** the LLM requests completions for "user.", **Then** the system returns `Name`, `Email`, `Id` and other accessible members.

2. **Given** a variable `list` of type `List<string>`, **When** the LLM requests completions for "list.", **Then** the system returns members like `Count`, `Add`, `Contains`, `First`, etc.

3. **Given** a partial member name "user.Na", **When** the LLM requests completions, **Then** the system returns `Name` (and any other members starting with "Na").

---

### User Story 3 - Static Type Member Completion (Priority: P2)

An LLM client needs to access static members of a type (e.g., `DateTime.Now`, `Math.PI`). The LLM requests completions for a type name followed by a dot to get available static members.

**Why this priority**: Common but less frequent than instance member access. Static members are used for utility methods and constants.

**Independent Test**: Request completions for "DateTime." and verify static members like `Now`, `UtcNow`, `Today`, `Parse` are returned.

**Acceptance Scenarios**:

1. **Given** a debug session is active, **When** the LLM requests completions for "Math.", **Then** the system returns static members like `PI`, `E`, `Max`, `Min`, `Abs`.

2. **Given** a debug session is active, **When** the LLM requests completions for "String.", **Then** the system returns static members like `Empty`, `IsNullOrEmpty`, `Join`, `Format`.

---

### User Story 4 - Namespace-Qualified Type Completion (Priority: P3)

An LLM client needs to reference a fully qualified type name. The LLM requests completions for namespace prefixes to discover available types.

**Why this priority**: Less common scenario — most debugging involves local variables and their members. Useful for advanced scenarios where type discovery is needed.

**Independent Test**: Request completions for "System.Collections." and verify types like `Generic`, `ArrayList`, `Hashtable` are suggested.

**Acceptance Scenarios**:

1. **Given** a debug session is active, **When** the LLM requests completions for "System.", **Then** the system returns child namespaces like `Collections`, `IO`, `Text`, and types like `String`, `Int32`.

2. **Given** a debug session is active, **When** the LLM requests completions for "System.Linq.Enumerable.", **Then** the system returns static extension methods like `Where`, `Select`, `First`.

---

### Edge Cases

- What happens when the expression contains syntax errors? — Return empty completions, not an error.
- How does the system handle very long completion lists (hundreds of members)? — Return all completions; client is responsible for filtering/limiting display.
- What happens when completion is requested for a non-existent variable? — Return empty completions.
- How are private/internal members handled? — Include them (debugger has full access).
- What happens during process running state (not paused)? — Return empty completions since scope context is unavailable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST implement the MCP `completion/complete` request handler.
- **FR-002**: System MUST return completions for variable names in the current stack frame scope when the argument reference is for a variable context.
- **FR-003**: System MUST return completions for object members (properties, fields, methods) when the expression ends with a dot after a valid object reference.
- **FR-004**: System MUST return completions for static type members when the expression ends with a dot after a type name.
- **FR-005**: System MUST filter completions based on the partial text after the last dot (prefix matching).
- **FR-006**: System MUST return an empty completion list (not an error) when no debug session is active.
- **FR-007**: System MUST return an empty completion list when the process is running (not paused).
- **FR-008**: System MUST include both public and non-public members in completions (debugger has elevated access).
- **FR-009**: System MUST support completion for the `evaluate` tool's `expression` argument.
- **FR-010**: System MUST advertise the Completions capability during MCP initialization.

### Key Entities

- **CompletionRef**: The MCP argument reference identifying what to complete (tool name + argument name).
- **CompletionContext**: The partial expression text and cursor position provided by the client.
- **CompletionResult**: List of completion values with optional descriptions and metadata.
- **Scope**: The current debugging context (stack frame, thread) determining available variables.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Completions for local variables are returned within 100ms for typical stack frames (under 50 variables).
- **SC-002**: Object member completions include at least 95% of the type's accessible members.
- **SC-003**: Prefix filtering correctly narrows results — requesting "cust" from `[customer, customerId, count]` returns exactly `[customer, customerId]`.
- **SC-004**: LLM clients can use completions to write valid evaluation expressions without guessing names, reducing `evaluate` call failures due to invalid names by at least 80%.
- **SC-005**: System handles completion requests gracefully in all states (no session, running, paused) without errors.

## Assumptions

- MCP SDK 0.7.0+ supports the Completions capability and `completion/complete` handler registration.
- Completions are requested via MCP's standard completion protocol, referencing tool arguments.
- The `evaluate` tool is the primary (and initially only) tool supporting argument completions.
- Completion evaluation uses the same debugger APIs as expression evaluation (ICorDebug).
- Client is responsible for UI presentation (filtering, sorting, limiting displayed results).

## Out of Scope

- Completions for prompt arguments (only tool arguments supported initially).
- Code completion outside of debugger expressions (e.g., for source file editing).
- Snippet/template completions.
- Sorting or ranking completions by relevance (returned in natural order).
- Caching of completion results across requests.

## Dependencies

- Feature 019 (MCP Resources) — for SDK 0.7.0+ compatibility patterns.
- Existing `evaluate` tool infrastructure for expression parsing.
- ICorDebug APIs for variable enumeration and type inspection.
