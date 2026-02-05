# Quickstart: MCP Breakpoint Notifications

## Overview

This feature adds two capabilities:
1. **Push notifications** for breakpoint hits (no more polling with `breakpoint_wait`)
2. **Tracepoints** - non-blocking observation points with custom log messages

## Prerequisites

- Active debug session (via `debug_attach` or `debug_launch`)
- MCP client that supports receiving notifications

## 1. Receiving Breakpoint Notifications

When any breakpoint is hit, you'll receive a `debugger/breakpointHit` notification automatically:

```json
{
  "method": "debugger/breakpointHit",
  "params": {
    "breakpointId": "bp-1",
    "type": "blocking",
    "location": {
      "file": "/path/to/Program.cs",
      "line": 42,
      "functionName": "Main"
    },
    "threadId": 12345,
    "timestamp": "2026-02-05T10:30:00.123Z",
    "hitCount": 1
  }
}
```

This works alongside `breakpoint_wait` - you can use either or both.

## 2. Setting a Basic Tracepoint

Tracepoints don't pause execution - they just notify you when code passes through:

```json
// Call tracepoint_set
{
  "file": "Calculator.cs",
  "line": 25
}
```

Response:
```json
{
  "success": true,
  "tracepoint": {
    "id": "tp-1",
    "type": "tracepoint",
    "location": { "file": "/path/to/Calculator.cs", "line": 25 },
    "state": "bound",
    "enabled": true
  }
}
```

When that line executes, you receive:
```json
{
  "method": "debugger/breakpointHit",
  "params": {
    "breakpointId": "tp-1",
    "type": "tracepoint",
    "location": { "file": "/path/to/Calculator.cs", "line": 25 },
    "threadId": 12345,
    "timestamp": "2026-02-05T10:30:00.456Z",
    "hitCount": 1
  }
}
```

## 3. Tracepoint with Log Message

Capture variable values without stopping:

```json
// Call tracepoint_set
{
  "file": "Calculator.cs",
  "line": 25,
  "log_message": "Add({a}, {b}) = {result}"
}
```

When hit, the notification includes evaluated values:
```json
{
  "method": "debugger/breakpointHit",
  "params": {
    "breakpointId": "tp-2",
    "type": "tracepoint",
    "location": { "file": "/path/to/Calculator.cs", "line": 25 },
    "threadId": 12345,
    "timestamp": "2026-02-05T10:30:00.789Z",
    "hitCount": 1,
    "logMessage": "Add(10, 20) = 30"
  }
}
```

## 4. Filtering High-Frequency Tracepoints

For hot paths, limit notification frequency:

```json
// Call tracepoint_set
{
  "file": "Loop.cs",
  "line": 15,
  "log_message": "Iteration {i}",
  "hit_count_multiple": 100,
  "max_notifications": 10
}
```

- `hit_count_multiple: 100` → notify only every 100th hit
- `max_notifications: 10` → auto-disable after 10 notifications

## 5. Managing Tracepoints

Tracepoints appear in `breakpoint_list` with `type: "tracepoint"`:

```json
// Response from breakpoint_list
{
  "breakpoints": [
    {
      "id": "bp-1",
      "type": "blocking",
      "location": { "file": "Program.cs", "line": 10 },
      "enabled": true
    },
    {
      "id": "tp-1",
      "type": "tracepoint",
      "location": { "file": "Calculator.cs", "line": 25 },
      "enabled": true,
      "logMessage": "Add({a}, {b}) = {result}"
    }
  ]
}
```

Use existing tools to manage:
- `breakpoint_enable` / disable tracepoints
- `breakpoint_remove` to delete tracepoints

## Workflow Example: Debugging a Loop

```
1. debug_attach to process
2. tracepoint_set on loop body with log_message: "i={i}, sum={sum}"
3. Let the loop run
4. Receive notifications showing variable evolution
5. Spot anomaly in logMessage values
6. breakpoint_set at suspicious iteration
7. Inspect with variables_get when breakpoint hits
```

## Expression Evaluation Errors

If an expression fails to evaluate:

```json
{
  "logMessage": "Processing item: <error: NullReferenceException>"
}
```

The notification is still sent - other expressions are evaluated normally.
