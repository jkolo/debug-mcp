# Feature Specification: MCP Tool Annotations & Best Practices

**Feature Branch**: `024-mcp-best-practices`
**Created**: 2026-02-10
**Status**: Draft
**Input**: Add MCP tool annotations (Title, ReadOnly, Destructive, Idempotent, OpenWorld) to all 34 tools, improve descriptions for the 10 most-used tools with response examples following Anthropic's Advanced Tool Use best practices, and add automated tests that verify annotation correctness against the classification table.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - AI Client Makes Safer Tool Selections Using Annotations (Priority: P1)

An AI client (e.g., Claude) connects to the debug-mcp server and retrieves the list of available tools. Each tool now exposes metadata annotations that indicate whether the tool is read-only, destructive, idempotent, and whether it operates in a closed domain. The AI client uses these annotations to make safer decisions — for example, it can freely invoke read-only tools without asking the user for confirmation, while prompting for explicit approval before calling destructive tools like `debug_launch` or `debug_disconnect`.

**Why this priority**: This is the core value of the feature. Without annotations, AI clients treat all 34 tools equally — they have no way to distinguish a harmless `threads_list` from a destructive `debug_disconnect`. Adding annotations is the minimum viable change that immediately improves AI safety and decision-making.

**Independent Test**: Can be fully tested by connecting an MCP client, listing tools, and verifying that each tool's metadata includes the correct `ReadOnly`, `Destructive`, `Idempotent`, and `OpenWorld` values according to the classification table.

**Acceptance Scenarios**:

1. **Given** the MCP server is running, **When** a client lists available tools, **Then** each tool's metadata includes `ReadOnly`, `Destructive`, `Idempotent`, and `OpenWorld` boolean fields with values matching the classification table below.
2. **Given** the MCP server is running, **When** a client inspects a read-only tool (e.g., `breakpoint_list`), **Then** it reports `ReadOnly=true`, `Destructive=false`.
3. **Given** the MCP server is running, **When** a client inspects a destructive tool (e.g., `debug_disconnect`), **Then** it reports `ReadOnly=false`, `Destructive=true`.
4. **Given** the MCP server is running, **When** a client inspects any tool, **Then** `OpenWorld` is `false` for all 34 tools (closed domain).

---

### User Story 2 - Human-Readable Tool Titles in Client UI (Priority: P1)

A human user views the list of available debugger tools in their MCP client's UI (e.g., a tool picker or sidebar). Instead of seeing raw snake_case names like `breakpoint_set_exception`, each tool now displays a human-friendly title like "Set Exception Breakpoint". This makes the tool list scannable and reduces cognitive load when selecting tools.

**Why this priority**: Titles are a trivial addition (one string per tool) but significantly improve the human experience. They ship alongside annotations with zero additional risk.

**Independent Test**: Can be fully tested by listing tools and verifying each has a non-empty `Title` field that is human-readable (title case, no underscores, concise).

**Acceptance Scenarios**:

1. **Given** the MCP server is running, **When** a client lists available tools, **Then** each tool has a non-empty `Title` field.
2. **Given** the MCP server is running, **When** a client inspects any tool, **Then** the `Title` is in title case (e.g., "List Breakpoints", not "list breakpoints" or "breakpoint_list").
3. **Given** all 34 tools have titles, **When** a human scans the tool list, **Then** each title uniquely identifies the tool's purpose in 2-4 words.

---

### User Story 3 - AI Client Uses Enhanced Descriptions with Response Examples (Priority: P2)

An AI client reads the enhanced descriptions for the 10 most-used tools. Each description now documents what the tool returns (field names and types), states preconditions (e.g., "requires the process to be paused"), and includes a concrete JSON response example showing the actual shape of the returned data. The AI uses this richer context to select the right tool on the first attempt more often, parse responses correctly, and reduce wasted tool calls.

**Why this priority**: While annotations (P1) help the AI decide *safety*, enhanced descriptions with response examples help the AI decide *correctness* — picking the right tool for the task and knowing exactly what to expect back. This is valuable but requires more effort per tool and is less critical than safety annotations.

**Independent Test**: Can be tested by reading each enhanced tool's description and verifying it contains: (a) return format documentation, (b) precondition statements where applicable, (c) at least one concrete JSON response example showing the success response shape.

**Acceptance Scenarios**:

1. **Given** the MCP server is running, **When** a client reads the description of `evaluate`, **Then** the description includes what fields are returned (e.g., `value`, `type`, `hasChildren`), states the precondition that the process must be paused, and shows a JSON response example.
2. **Given** the MCP server is running, **When** a client reads the description of `debug_launch`, **Then** the description documents the returned session object fields, mentions that it starts a new process, and shows a JSON response example.
3. **Given** the MCP server is running, **When** a client reads the description of any of the 10 enhanced tools, **Then** the description is at least 2 sentences long, includes return format information, and contains a response example showing field names and representative values.

---

### User Story 4 - Annotation Correctness is Verified by Automated Tests (Priority: P2)

A developer modifies tool annotations (e.g., changing a tool from read-only to destructive, or adding a new tool). The test suite includes automated tests that verify every tool's annotations match the expected classification table. If a developer accidentally marks a destructive tool as `ReadOnly = true`, or forgets to add annotations to a new tool, the tests catch it immediately and fail with a clear error message indicating which tool has incorrect annotations.

**Why this priority**: Annotations are safety-critical metadata — an AI client trusts `ReadOnly = true` to mean the tool is safe to call without user confirmation. If an annotation is wrong, the AI could invoke destructive operations silently. Automated tests prevent annotation drift and catch regressions before they ship. This is P2 because annotations must exist first (P1) before they can be tested.

**Independent Test**: Can be fully tested by running the test suite and verifying that: (a) every registered tool is covered by an annotation test, (b) the test asserts the expected values for all 5 annotation properties, and (c) intentionally wrong annotations cause test failures.

**Acceptance Scenarios**:

1. **Given** all 34 tools have annotations, **When** the test suite runs, **Then** every tool is verified against the classification table with assertions for `Title`, `ReadOnly`, `Destructive`, `Idempotent`, and `OpenWorld`.
2. **Given** a tool's annotation is intentionally wrong (e.g., `breakpoint_list` marked `Destructive = true`), **When** the test suite runs, **Then** the test for that tool fails with a message identifying the tool name and the mismatched property.
3. **Given** a new tool is added without annotations, **When** the test suite runs, **Then** a test detects the tool is not covered by annotation tests and reports it as a missing coverage item.
4. **Given** the test suite passes, **When** a reviewer audits the test data, **Then** the expected annotation values in the test match 1:1 with the classification table in the spec.

---

### Edge Cases

- What happens when an MCP client does not support tool annotations? The annotations are purely additive metadata in the MCP protocol — clients that don't understand them simply ignore the extra fields. No breaking change.
- What happens when a tool's behavior is context-dependent (e.g., `evaluate` is read-only for pure expressions but could trigger side effects for expressions like `obj.Dispose()`)? The annotation reflects the tool's *design intent* (read-only inspection), not all possible user inputs. The description should note this nuance.
- What if the SDK version is upgraded and annotation property names change? The `[McpServerTool]` attribute properties are part of the ModelContextProtocol SDK's public API surface. If they change, it would be a breaking change requiring a spec-level update.
- What happens when a new tool is added but the developer forgets to add it to the annotation tests? The test suite MUST include a coverage check that discovers all registered tools and verifies each has a corresponding annotation test entry. A missing tool causes a test failure.
- What if a response example in a description becomes outdated after a code change? Response examples are illustrative (showing field names and representative shapes), not contractual. They document the typical response structure, not exact runtime values.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every tool MUST have a `Title` property set on its `[McpServerTool]` attribute — a human-readable name in title case (2-4 words), uniquely identifying the tool's purpose.

- **FR-002**: Every tool MUST have `ReadOnly`, `Destructive`, `Idempotent`, and `OpenWorld` properties set on its `[McpServerTool]` attribute, matching the classification in the table below.

- **FR-003**: All 34 tools MUST set `OpenWorld = false` because they operate exclusively on the debug session and loaded workspace (a closed domain).

- **FR-004**: Read-only tools (21 tools) MUST set `ReadOnly = true` and `Destructive = false`.

- **FR-005**: State-modifying non-destructive tools (8 tools) MUST set `ReadOnly = false` and `Destructive = false`.

- **FR-006**: Destructive tools (5 tools) MUST set `ReadOnly = false` and `Destructive = true`.

- **FR-007**: The `Idempotent` property MUST reflect whether repeated identical calls produce the same result. Tools that create new resources or consume input are non-idempotent; tools that return current state or set absolute values are idempotent.

- **FR-008**: The following 10 tools MUST have enhanced `[Description]` text that documents: (a) what the tool returns (key field names), (b) preconditions (e.g., "process must be paused"), and (c) at least one concrete JSON response example showing the success response shape: `debug_launch`, `breakpoint_set`, `breakpoint_wait`, `debug_continue`, `debug_step`, `variables_get`, `evaluate`, `stacktrace_get`, `exception_get_context`, `debug_disconnect`.

- **FR-009**: Each of the 10 enhanced tool descriptions MUST include a JSON response example that shows the actual field names and representative values the tool returns on success. The example MUST be embedded directly in the `[Description]` text (not in a separate document) so that AI clients can read it from the tool listing.

- **FR-010**: The test suite MUST include annotation verification tests that assert, for every one of the 34 tools, that the `Title`, `ReadOnly`, `Destructive`, `Idempotent`, and `OpenWorld` values match the classification table in this spec. Each tool MUST have a dedicated test assertion.

- **FR-011**: The annotation verification tests MUST include a coverage check that discovers all tools registered with the MCP server and verifies each one has a corresponding annotation test entry. If a new tool is registered but not covered by an annotation test, the coverage check MUST fail.

- **FR-012**: If an annotation test fails, the error message MUST identify the tool name and the specific property that mismatched (e.g., "Tool 'breakpoint_list': expected ReadOnly=true, got ReadOnly=false").

### Tool Annotation Classification Table

#### Read-Only Tools (21 tools): `ReadOnly = true, Destructive = false`

| Tool Name | Title | Idempotent | Notes |
|---|---|---|---|
| `breakpoint_list` | List Breakpoints | true | Returns current breakpoint state |
| `breakpoint_wait` | Wait for Breakpoint Hit | false | Result depends on runtime execution; blocking |
| `debug_state` | Get Debug State | true | Returns current session state |
| `evaluate` | Evaluate Expression | false | Expression may have side effects; result depends on runtime state |
| `object_inspect` | Inspect Object | true | Returns object fields at current state |
| `variables_get` | Get Variables | true | Returns variables in current scope |
| `stacktrace_get` | Get Stack Trace | true | Returns current call stack |
| `threads_list` | List Threads | true | Returns current thread state |
| `modules_list` | List Modules | true | Returns loaded modules |
| `modules_search` | Search Modules | true | Searches loaded modules |
| `types_get` | Get Types | true | Returns types from module metadata |
| `members_get` | Get Type Members | true | Returns type members |
| `layout_get` | Get Memory Layout | true | Returns memory layout |
| `memory_read` | Read Memory | true | Returns raw memory bytes |
| `references_get` | Get Object References | true | Returns GC references |
| `exception_get_context` | Get Exception Context | true | Returns exception autopsy |
| `process_read_output` | Read Process Output | false | Consumes buffered output — subsequent calls return different results |
| `code_goto_definition` | Go to Definition | true | Returns source location |
| `code_find_usages` | Find Usages | true | Returns usage locations |
| `code_find_assignments` | Find Assignments | true | Returns assignment locations |
| `code_get_diagnostics` | Get Diagnostics | true | Returns compiler diagnostics |

#### State-Modifying Non-Destructive Tools (8 tools): `ReadOnly = false, Destructive = false`

| Tool Name | Title | Idempotent | Notes |
|---|---|---|---|
| `breakpoint_set` | Set Breakpoint | false | Creates a new breakpoint each call |
| `breakpoint_enable` | Enable/Disable Breakpoint | true | Sets absolute enabled state |
| `breakpoint_set_exception` | Set Exception Breakpoint | false | Creates a new exception breakpoint each call |
| `tracepoint_set` | Set Tracepoint | false | Creates a new tracepoint each call |
| `debug_continue` | Continue Execution | false | Resumes from current state; non-repeatable |
| `debug_step` | Step Through Code | false | Advances execution; non-repeatable |
| `debug_pause` | Pause Execution | true | Pausing an already-paused process is a no-op |
| `code_load` | Load Workspace | true | Loading an already-loaded workspace is a no-op |

#### Destructive Tools (5 tools): `ReadOnly = false, Destructive = true`

| Tool Name | Title | Idempotent | Notes |
|---|---|---|---|
| `debug_launch` | Launch Process | false | Starts a new OS process |
| `debug_attach` | Attach to Process | false | Attaches debugger to running process |
| `debug_disconnect` | Disconnect Debug Session | true | Disconnecting when already disconnected is a no-op |
| `breakpoint_remove` | Remove Breakpoint | false | Removing a non-existent breakpoint is an error, not a no-op |
| `process_write_input` | Write Process Input | false | Sends irreversible data to process stdin |

### Assumptions

- `evaluate` is classified as read-only because its *design intent* is inspection, even though user-provided expressions could theoretically trigger side effects (e.g., calling a method that mutates state). The description should note this nuance.
- `process_read_output` is classified as read-only because it reads from the process, but it is non-idempotent because it consumes buffered output (subsequent calls return different results).
- `breakpoint_remove` is classified as destructive because it permanently deletes a user-created resource. It is non-idempotent because removing an already-removed breakpoint returns an error rather than a no-op.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 34 tools expose `Title`, `ReadOnly`, `Destructive`, `Idempotent`, and `OpenWorld` annotations when listed by an MCP client — 100% coverage, zero tools missing any annotation.
- **SC-002**: All annotation values match the classification table above — zero mismatches when audited against the table.
- **SC-003**: All 10 designated tools have enhanced descriptions that include return format documentation, precondition statements, and a JSON response example — verified by reading each description.
- **SC-004**: The project builds successfully with no new warnings or errors introduced by annotation or description changes.
- **SC-005**: All existing unit and contract tests pass without modification — annotations are purely additive metadata and do not affect tool behavior.
- **SC-006**: An MCP client connecting to the server can distinguish read-only tools from destructive tools using only the annotation metadata, without parsing description text.
- **SC-007**: The annotation verification test suite covers all 34 tools — running the tests produces 34 tool-level assertions with zero failures.
- **SC-008**: The annotation test coverage check detects any tool not covered by annotation tests — if a hypothetical 35th tool is added without a test entry, the coverage check fails.
