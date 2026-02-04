# Data Model: MCP Protocol Logging

**Feature**: 014-mcp-logging
**Date**: 2026-02-04

## Entities

### LoggingMessageNotificationParams (MCP SDK - existing)

The MCP SDK provides this type for log message notifications:

| Field | Type | Description |
|-------|------|-------------|
| level | string | RFC 5424 severity: debug, info, notice, warning, error, critical, alert, emergency |
| logger | string? | Optional category name identifying source component |
| data | object | Arbitrary JSON-serializable log data |

### McpLogLevel (new enum)

Maps .NET LogLevel to MCP protocol levels:

| Value | .NET Equivalent | RFC 5424 |
|-------|-----------------|----------|
| Debug | Trace, Debug | 7 |
| Info | Information | 6 |
| Notice | (none) | 5 |
| Warning | Warning | 4 |
| Error | Error | 3 |
| Critical | Critical | 2 |
| Alert | (none) | 1 |
| Emergency | (none) | 0 |

### LoggingOptions (new class)

Configuration for MCP logging behavior:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| EnableStderr | bool | false | Write logs to stderr alongside MCP |
| DefaultMinLevel | McpLogLevel | Info | Initial MCP log level before client sets it |
| MaxPayloadBytes | int | 65536 | Maximum serialized data size before truncation |

## State Transitions

### Log Level State Machine

```
┌─────────────────────────────────────────────────────────────┐
│                      MCP Server Running                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐  logging/setLevel   ┌──────────────────┐  │
│  │ DefaultLevel │ ─────────────────── │ ClientLevel      │  │
│  │ (info)       │                     │ (from request)   │  │
│  └──────────────┘                     └──────────────────┘  │
│         │                                      │             │
│         ▼                                      ▼             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Filter: level >= currentMinLevel         │   │
│  │              Send: notifications/message              │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Data Flow

```
ILogger.Log(level, message, args)
         │
         ▼
┌─────────────────────────┐
│    McpLoggerProvider    │
│    (ILoggerProvider)    │
└───────────┬─────────────┘
            │ CreateLogger(category)
            ▼
┌─────────────────────────┐
│      McpLogger          │
│      (ILogger)          │
└───────────┬─────────────┘
            │ Log(level, eventId, state, exception, formatter)
            ▼
┌─────────────────────────┐     ┌─────────────────┐
│ Level >= MinLevel?      │─No──│ (discard)       │
└───────────┬─────────────┘     └─────────────────┘
            │ Yes
            ▼
┌─────────────────────────┐
│ Format message          │
│ Serialize to JSON       │
│ Truncate if > 64KB      │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ McpServer               │
│ .SendNotificationAsync  │
│ ("notifications/message")│
└───────────┬─────────────┘
            │ (fire-and-forget)
            ▼
┌─────────────────────────┐
│ MCP Client receives     │
│ log notification        │
└─────────────────────────┘
```

## Validation Rules

| Rule | Constraint | Error Handling |
|------|------------|----------------|
| Payload size | ≤ 64KB | Truncate with `[truncated]` indicator |
| Log level | Valid MCP level | Map unmapped .NET levels to nearest MCP level |
| Category name | Non-null | Default to "DebugMcp" if null |
| Exception data | Serializable | Extract message and stack trace only |

## Relationships

```
McpServer (1) ───────────── (1) McpLoggerProvider
                                      │
                                      │ creates
                                      ▼
                              (n) McpLogger instances
                                  (one per category)
```
