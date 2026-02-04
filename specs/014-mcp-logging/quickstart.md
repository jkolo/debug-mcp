# Quickstart: MCP Protocol Logging

**Feature**: 014-mcp-logging

## Overview

This feature replaces native console/stderr logging with MCP protocol logging. MCP clients receive structured log messages via `notifications/message` notifications.

## Usage

### Basic Usage (MCP logging only)

```bash
debug-mcp
```

Logs are sent to connected MCP clients. No stderr output.

### With Stderr Logging

```bash
debug-mcp --stderr-logging
# or
debug-mcp -s
```

Logs are sent to both MCP clients and stderr (useful for debugging the debugger).

### Controlling Log Level (from MCP client)

Send `logging/setLevel` request:

```json
{
  "method": "logging/setLevel",
  "params": { "level": "debug" }
}
```

## Log Levels

| Level | When to Use |
|-------|-------------|
| debug | Detailed debugging (tool invocations, state changes) |
| info | Normal operations (attach, launch, disconnect) |
| warning | Recoverable issues (timeouts, fallbacks) |
| error | Operation failures (attach failed, launch failed) |
| critical | Severe failures requiring attention |

## Receiving Logs (MCP Client)

Subscribe to `notifications/message`:

```json
{
  "method": "notifications/message",
  "params": {
    "level": "info",
    "logger": "DebugMcp.DebugSession",
    "data": "Attaching to process 1234"
  }
}
```

## Structured Log Data

Some log messages include structured data:

```json
{
  "level": "info",
  "logger": "DebugMcp.DebugSession",
  "data": {
    "message": "Successfully attached to process",
    "processId": 1234,
    "processName": "myapp",
    "runtimeVersion": ".NET 10.0"
  }
}
```

## Implementation Notes

### For Developers

Existing logging code continues to work unchanged:

```csharp
// No changes needed - uses existing ILogger infrastructure
_logger.AttachingToProcess(processId);
_logger.AttachedToProcess(processId, processName, runtimeVersion);
```

### Adding New Log Messages

Add new log messages in `Infrastructure/Logging.cs`:

```csharp
[LoggerMessage(
    EventId = 1007,
    Level = LogLevel.Information,
    Message = "New operation {OperationName}")]
public static partial void NewOperation(this ILogger logger, string operationName);
```

Then use in your code:

```csharp
_logger.NewOperation("example");
```

The message automatically flows to MCP clients.

## Troubleshooting

### Logs not appearing in MCP client

1. Check client supports `notifications/message`
2. Verify log level - default is "info", debug messages need explicit `logging/setLevel`
3. Enable stderr logging (`-s`) to verify logs are generated

### Large log messages truncated

Payloads exceeding 64KB are truncated with `[truncated]` suffix. This is by design to prevent protocol issues.

### stderr shows logs but MCP client doesn't

Ensure the MCP client is properly connected and handling notifications. Some clients may buffer or filter notifications.
