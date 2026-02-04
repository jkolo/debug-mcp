# MCP Logging Contract

**Feature**: 014-mcp-logging
**Protocol Version**: 2025-03-26

## Server Capability Declaration

The server MUST declare logging capability during initialization:

```json
{
  "capabilities": {
    "logging": {}
  }
}
```

## Notification: notifications/message

Sent by server to client when a log event occurs.

### Request

**Method**: `notifications/message`

**Params Schema**:
```json
{
  "type": "object",
  "properties": {
    "level": {
      "type": "string",
      "enum": ["debug", "info", "notice", "warning", "error", "critical", "alert", "emergency"],
      "description": "Log severity level (RFC 5424)"
    },
    "logger": {
      "type": "string",
      "description": "Logger category name (e.g., 'DebugMcp.DebugSession')"
    },
    "data": {
      "type": ["string", "object"],
      "description": "Log message content, either string or structured JSON"
    }
  },
  "required": ["level", "data"]
}
```

### Examples

**Simple message**:
```json
{
  "jsonrpc": "2.0",
  "method": "notifications/message",
  "params": {
    "level": "info",
    "logger": "DebugMcp.DebugSession",
    "data": "Attaching to process 1234"
  }
}
```

**Structured data**:
```json
{
  "jsonrpc": "2.0",
  "method": "notifications/message",
  "params": {
    "level": "info",
    "logger": "DebugMcp.DebugSession",
    "data": {
      "message": "Successfully attached to process",
      "processId": 1234,
      "processName": "myapp",
      "runtimeVersion": ".NET 10.0"
    }
  }
}
```

**Error with details**:
```json
{
  "jsonrpc": "2.0",
  "method": "notifications/message",
  "params": {
    "level": "error",
    "logger": "DebugMcp.ProcessDebugger",
    "data": {
      "message": "Failed to attach to process",
      "processId": 1234,
      "errorCode": "E_ACCESSDENIED",
      "errorMessage": "Access denied"
    }
  }
}
```

**Truncated payload**:
```json
{
  "jsonrpc": "2.0",
  "method": "notifications/message",
  "params": {
    "level": "debug",
    "logger": "DebugMcp.Tools",
    "data": "Very long message content...[truncated]"
  }
}
```

## Request: logging/setLevel

Client requests to change minimum log level.

### Request

**Method**: `logging/setLevel`

**Params Schema**:
```json
{
  "type": "object",
  "properties": {
    "level": {
      "type": "string",
      "enum": ["debug", "info", "notice", "warning", "error", "critical", "alert", "emergency"],
      "description": "Minimum log level to receive"
    }
  },
  "required": ["level"]
}
```

### Response

**Result Schema**:
```json
{
  "type": "object",
  "properties": {},
  "additionalProperties": false
}
```

Empty object on success.

### Example

**Request**:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "logging/setLevel",
  "params": {
    "level": "warning"
  }
}
```

**Response**:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {}
}
```

## Logger Categories

| Category | Description |
|----------|-------------|
| `DebugMcp.DebugSession` | Session lifecycle events (attach, launch, disconnect) |
| `DebugMcp.ProcessDebugger` | Low-level debugger operations |
| `DebugMcp.Tools` | MCP tool invocations and completions |

## Constraints

| Constraint | Value | Behavior |
|------------|-------|----------|
| Max payload size | 64 KB | Truncate data with `[truncated]` suffix |
| Default level | info | Until client sends `logging/setLevel` |
| Delivery | Async | Fire-and-forget, non-blocking |
