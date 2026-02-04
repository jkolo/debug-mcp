# Research: MCP Protocol Logging

**Feature**: 014-mcp-logging
**Date**: 2026-02-04

## Research Questions

### R1: How does MCP SDK support sending log notifications?

**Decision**: Use `SendNotificationAsync` with `NotificationMethods.LoggingMessageNotification` ("notifications/message")

**Rationale**:
- The MCP C# SDK provides `McpServer.SendNotificationAsync<TParams>()` method inherited from `McpSession`
- The constant `NotificationMethods.LoggingMessageNotification = "notifications/message"` is defined in the SDK
- The SDK includes `LoggingMessageNotificationParams` for structured log data
- The `McpServer.LoggingLevel` property tracks the client-requested minimum log level

**Alternatives Considered**:
- Direct JSON-RPC message writing: Rejected - SDK provides proper abstraction
- Custom notification method: Rejected - must use standard MCP `notifications/message`

**Source**: [MCP C# SDK - McpServer.Methods.cs](https://github.com/modelcontextprotocol/csharp-sdk)

### R2: How to integrate with Microsoft.Extensions.Logging?

**Decision**: Implement custom `ILoggerProvider` that wraps `McpServer` and sends notifications

**Rationale**:
- Current codebase uses `ILogger<T>` throughout (34 files inject logger)
- `ILoggerProvider` pattern allows transparent logging without code changes
- Can register multiple providers (MCP + Console) simultaneously
- Existing `Logging.cs` with `[LoggerMessage]` attributes continues to work unchanged

**Alternatives Considered**:
- Replace all `ILogger` calls with direct MCP calls: Rejected - massive code change, breaks abstraction
- Wrapper service around ILogger: Rejected - adds unnecessary indirection

**Implementation Pattern**:
```csharp
public class McpLoggerProvider : ILoggerProvider
{
    private readonly McpServer _server;
    public ILogger CreateLogger(string categoryName) => new McpLogger(_server, categoryName);
}

public class McpLogger : ILogger
{
    public void Log<TState>(...)
    {
        // Send via _server.SendNotificationAsync(NotificationMethods.LoggingMessageNotification, ...)
    }
}
```

### R3: How to handle log level filtering?

**Decision**: Respect `McpServer.LoggingLevel` property + local minimum level config

**Rationale**:
- MCP protocol defines `logging/setLevel` request that clients send
- SDK automatically updates `McpServer.LoggingLevel` when client sends this request
- Default to "info" level per spec clarification (FR-011)
- Map .NET `LogLevel` to MCP log levels (debug, info, notice, warning, error, critical, alert, emergency)

**Log Level Mapping**:
| .NET LogLevel | MCP Level |
|---------------|-----------|
| Trace | debug |
| Debug | debug |
| Information | info |
| Warning | warning |
| Error | error |
| Critical | critical |

### R4: How to declare logging capability?

**Decision**: Configure via `McpServerOptions.Capabilities.Logging`

**Rationale**:
- MCP protocol requires servers to declare `logging` capability
- SDK's `McpServerOptions` has `Capabilities` property for this
- Must be set during server configuration in `AddMcpServer()`

**Implementation**:
```csharp
builder.Services.AddMcpServer(options =>
{
    options.Capabilities.Logging = new LoggingCapability();
});
```

### R5: How to implement CLI flag for stderr logging?

**Decision**: Add `--stderr-logging` / `-s` option using System.CommandLine

**Rationale**:
- Project already uses System.CommandLine for CLI parsing
- Flag controls whether ConsoleLoggerProvider is registered
- Default: stderr logging disabled when MCP client connected

**Implementation**:
```csharp
var stderrOption = new Option<bool>(
    aliases: ["--stderr-logging", "-s"],
    description: "Enable stderr logging alongside MCP logging",
    getDefaultValue: () => false);
```

### R6: How to handle 64KB payload limit?

**Decision**: Truncate log message data at 64KB with `[truncated]` indicator

**Rationale**:
- Per spec clarification (FR-010), must truncate at 64KB
- Truncation applies to the serialized JSON data payload
- Add `[truncated]` suffix to indicate data was cut

**Implementation**:
```csharp
const int MaxPayloadBytes = 64 * 1024;
if (serializedData.Length > MaxPayloadBytes)
{
    serializedData = serializedData[..(MaxPayloadBytes - 12)] + "[truncated]";
}
```

### R7: How to ensure non-blocking log delivery?

**Decision**: Use fire-and-forget pattern with error suppression

**Rationale**:
- FR-007 requires log notifications must not block debugger operations
- `SendNotificationAsync` returns Task - don't await it in hot path
- Catch and suppress exceptions to prevent logging failures from crashing debugger

**Implementation**:
```csharp
_ = Task.Run(async () =>
{
    try { await _server.SendNotificationAsync(...); }
    catch { /* Log failures should not crash debugger */ }
});
```

## Summary

All research questions resolved. Key findings:
1. MCP SDK provides all necessary primitives (`SendNotificationAsync`, `LoggingLevel`, `LoggingMessageNotificationParams`)
2. Implementation follows standard `ILoggerProvider` pattern - minimal code changes
3. CLI flag via existing System.CommandLine infrastructure
4. Payload truncation and async delivery handle edge cases
