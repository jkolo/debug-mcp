# Feature Specification: MCP Protocol Logging

**Feature Branch**: `014-mcp-logging`
**Created**: 2026-02-04
**Status**: Draft
**Input**: User description: "przerób logowanie z natywnego na konsolę na logowanie przy pomocy MCP"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - MCP Client Receives Debug Logs (Priority: P1)

An MCP client (e.g., Claude Desktop, VS Code extension) connects to the debugger server and receives structured log messages about debugger operations, enabling users to monitor debugger activity without accessing stderr directly.

**Why this priority**: This is the core value proposition - MCP clients can display debugger logs in their native UI, improving the debugging experience and making the tool more integrated with the MCP ecosystem.

**Independent Test**: Can be fully tested by connecting any MCP client, performing debug operations (attach, set breakpoint, continue), and verifying log notifications are received with correct severity levels and structured data.

**Acceptance Scenarios**:

1. **Given** an MCP client is connected to the debugger server, **When** the server attaches to a process, **Then** the client receives a log notification with level "info" containing the process ID and attachment status.
2. **Given** an MCP client is connected and a debug session is active, **When** a breakpoint is hit, **Then** the client receives a log notification with level "debug" containing breakpoint location and thread information.
3. **Given** an MCP client is connected, **When** an operation fails (e.g., attach fails), **Then** the client receives a log notification with level "error" containing the error code and descriptive message.

---

### User Story 2 - Client Controls Log Verbosity (Priority: P2)

An MCP client can request to change the minimum log level, allowing users to filter out noise and focus on relevant messages (e.g., only errors during production debugging, or full debug output during development).

**Why this priority**: Log filtering is essential for usability but requires the core logging mechanism to work first.

**Independent Test**: Can be fully tested by setting log level to "error", performing operations that would generate debug/info logs, and verifying only error-level messages are received.

**Acceptance Scenarios**:

1. **Given** an MCP client is connected, **When** the client sends a `logging/setLevel` request with level "error", **Then** subsequent debug and info level messages are not sent to the client.
2. **Given** a client has set minimum level to "warning", **When** operations generate debug, info, warning, and error messages, **Then** the client receives only warning and error notifications.

---

### User Story 3 - Backward Compatibility with Stderr (Priority: P3)

For scenarios where MCP logging is not available or for debugging the debugger itself, logs continue to be written to stderr as a fallback, ensuring the tool remains usable in all environments.

**Why this priority**: Fallback behavior is important for robustness but is secondary to the main MCP logging feature.

**Independent Test**: Can be fully tested by running the debugger without an MCP client and verifying logs appear on stderr.

**Acceptance Scenarios**:

1. **Given** the debugger is started and logging to stderr is enabled, **When** debug operations occur, **Then** log messages appear on stderr with appropriate formatting.
2. **Given** MCP logging is active, **When** an MCP client disconnects, **Then** logging continues to stderr without interruption.

---

### Edge Cases

- What happens when the MCP client disconnects mid-operation? (Logs should not cause errors, continue to stderr if enabled)
- How does the system handle high-frequency log messages? (Should not block debugger operations)
- What happens if a log message contains very large data payloads? (Truncate at 64KB with indicator that truncation occurred)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST send log messages to connected MCP clients using the `notifications/message` protocol method.
- **FR-002**: System MUST declare the `logging` capability in server capabilities.
- **FR-003**: System MUST support all standard MCP log levels: debug, info, notice, warning, error, critical, alert, emergency.
- **FR-004**: System MUST include a logger name (category) in each log notification to identify the source component.
- **FR-005**: System MUST support the `logging/setLevel` request to allow clients to control minimum log verbosity.
- **FR-006**: System MUST provide a CLI flag to control stderr logging behavior (enable/disable alongside MCP output).
- **FR-007**: Log notifications MUST NOT block debugger operations (asynchronous delivery).
- **FR-008**: Log messages MUST NOT contain sensitive information such as credentials, secrets, or personally identifiable information.
- **FR-009**: System MUST map existing log categories (DebugSession, ProcessDebugger, Tools) to MCP logger names.
- **FR-010**: System MUST truncate log message payloads exceeding 64KB to prevent protocol issues.
- **FR-011**: System MUST use "info" as the default minimum log level for MCP notifications until client requests a different level.

### Key Entities

- **Log Notification**: A structured message containing level, logger name, and arbitrary JSON-serializable data sent via MCP.
- **Log Level**: Severity classification following RFC 5424 (debug through emergency).
- **Logger Category**: Named component identifier (e.g., "DebugMcp.DebugSession", "DebugMcp.Tools").

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing log points in the codebase send notifications to connected MCP clients.
- **SC-002**: Log level filtering correctly prevents lower-priority messages from being sent when minimum level is set.
- **SC-003**: Debugger operations complete without measurable delay due to logging (logging is non-blocking).
- **SC-004**: MCP clients (e.g., Claude Desktop) display received log messages in their UI when connected to the debugger server.
- **SC-005**: Existing stderr logging functionality remains available as a configuration option.

## Clarifications

### Session 2026-02-04

- Q: What is the default stderr logging behavior when MCP client connects? → A: User-configurable via CLI flag
- Q: What is the maximum log payload size? → A: 64KB limit with truncation
- Q: What is the default minimum log level for MCP clients? → A: "info" level

## Assumptions

- The ModelContextProtocol C# SDK provides `IMcpServer.AsClientLoggerProvider()` or equivalent API for sending log notifications.
- MCP clients support the `notifications/message` method for receiving log messages.
- The current logging infrastructure uses Microsoft.Extensions.Logging, which can be extended with additional providers.
