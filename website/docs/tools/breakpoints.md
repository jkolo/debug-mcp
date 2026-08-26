---
title: Breakpoints
sidebar_position: 2
---

# Breakpoints

Breakpoint tools let you set and manage breakpoints — the primary mechanism for stopping execution at specific points in your code.

import AsciinemaPlayer from '@site/src/components/AsciinemaPlayer';

<AsciinemaPlayer src="/casts/breakpoint-workflow.cast" rows={24} cols={120} idleTimeLimit={2} speed={1.5} />

## When to Use

Use breakpoint tools to control where execution pauses. You can break on specific source lines, function entries, exception types, or conditions. After continuing execution, either subscribe to the `debugger/breakpointHit` [notification](#breakpoint-notifications) or read the `debugger://breakpoints` resource to see whether/what fired.

**Typical flow:** `breakpoint_set` → `debug_continue` → *(`debugger/breakpointHit` notification arrives)* → *(inspect state)*

## Tools

**On this page:**
[`breakpoint_set`](#breakpoint_set) | [`breakpoint_remove`](#breakpoint_remove) | [`breakpoint_enable`](#breakpoint_enable) | [`breakpoint_set_exception`](#breakpoint_set_exception) | [`tracepoint_set`](#tracepoint_set) | [`exception_get_context`](#exception_get_context) | [Notifications](#breakpoint-notifications)

### breakpoint_set

Set a breakpoint in source code.

**Requires:** Active session (running or paused)

**When to use:** You know the file and line (or function name) where you want execution to stop. Supports conditional breakpoints (break only when a condition is true), hit-count breakpoints (break after N hits), and logpoints (log without breaking).

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `file` | string | Yes* | Source file path |
| `line` | integer | Yes* | Line number (1-based) |
| `column` | integer | No | Column for specific position |
| `function` | string | No* | Full method name (alternative to file/line) |
| `condition` | string | No | Condition expression |
| `hit_count` | integer | No | Break after N hits |
| `log_message` | string | No | Log message (logpoint — doesn't break) |

*Either `file`+`line` or `function` is required.

**Examples:**

Basic breakpoint:
```json
{
  "file": "Services/UserService.cs",
  "line": 42
}
```

Conditional breakpoint:
```json
{
  "file": "Services/UserService.cs",
  "line": 42,
  "condition": "userId == null || userId.Length == 0"
}
```

Function breakpoint:
```json
{
  "function": "MyApp.Services.UserService.GetUser"
}
```

Logpoint (logs without breaking):
```json
{
  "file": "Services/UserService.cs",
  "line": 42,
  "log_message": "GetUser called with userId={userId}"
}
```

**Response:**
```json
{
  "id": 1,
  "verified": true,
  "location": {
    "file": "/app/Services/UserService.cs",
    "line": 42
  }
}
```

If the module isn't loaded yet:
```json
{
  "id": 2,
  "verified": false,
  "message": "Breakpoint pending, module not loaded"
}
```

**Real-world use case:** An AI agent investigating a bug sets a conditional breakpoint that only fires when the problematic input is received: `condition: "order.Total < 0"`. This avoids stopping on every call and goes straight to the bug.

---

### breakpoint_remove

Remove a breakpoint.

**Requires:** Active session (running or paused)

**When to use:** A breakpoint is no longer needed. Removing it avoids unnecessary stops.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | integer | Yes | Breakpoint ID |

**Response:**
```json
{
  "success": true,
  "id": 1
}
```

---

## Listing breakpoints

There is no `breakpoint_list` tool. Read the **`debugger://breakpoints`** MCP resource instead
— it returns both regular breakpoints and tracepoints (each with `id`, `type`, `location`,
`enabled`, `verified`, `state`, `hitCount`, `condition`, `logMessage`, and tracepoint-specific
fields like `hitCountMultiple`/`maxNotifications`/`notificationsSent`) plus exception
breakpoints, in one call, without needing a round-trip tool call.

---

### breakpoint_enable

Enable or disable a breakpoint without removing it.

**Requires:** Active session (running or paused)

**When to use:** Temporarily disable a breakpoint you might need later, without losing its configuration.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | string | Yes | Breakpoint ID |
| `enabled` | boolean | No | True to enable, false to disable (default: true) |

**Example (disable):**
```json
{
  "id": "bp-550e8400-e29b-41d4-a716-446655440000",
  "enabled": false
}
```

**Response:**
```json
{
  "success": true,
  "breakpoint": {
    "id": "bp-550e8400-e29b-41d4-a716-446655440000",
    "location": {
      "file": "/app/Services/UserService.cs",
      "line": 42
    },
    "state": "bound",
    "enabled": false,
    "verified": true,
    "hitCount": 3
  }
}
```

---

### breakpoint_set_exception

Set an exception breakpoint to break when specific exception types are thrown.

**Requires:** Active session (running or paused)

**When to use:** You want to catch exceptions as they happen — before any catch block runs. This is essential for debugging crashes, unhandled exceptions, or finding where unexpected exceptions originate.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `exception_type` | string | Yes | Full exception type name (e.g., `System.NullReferenceException`) |
| `break_on_first_chance` | boolean | No | Break on first-chance exception (default: true) |
| `break_on_second_chance` | boolean | No | Break on unhandled exception (default: true) |
| `include_subtypes` | boolean | No | Also break on derived exception types (default: true) |

**Example:**
```json
{
  "exception_type": "System.NullReferenceException",
  "break_on_first_chance": true,
  "include_subtypes": true
}
```

**Response:**
```json
{
  "success": true,
  "breakpoint": {
    "id": "ex-550e8400-e29b-41d4-a716-446655440000",
    "exceptionType": "System.NullReferenceException",
    "breakOnFirstChance": true,
    "breakOnSecondChance": true,
    "includeSubtypes": true,
    "enabled": true,
    "verified": true,
    "hitCount": 0
  }
}
```

**Real-world use case:** An AI agent debugging a crash sets `breakpoint_set_exception` for `System.InvalidOperationException` with `include_subtypes: true`. When the app throws the exception, execution stops at the exact throw site — not in a catch block three layers up.

---

### tracepoint_set

Set a tracepoint (non-blocking observation point) at a source location.

**Requires:** Active session (running or paused)

**When to use:** You want to observe code execution without stopping it. Tracepoints send MCP notifications when code passes through, letting you trace execution flow, log variable values, or count how often code paths are hit — all without pausing the application.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `file` | string | Yes | Source file path |
| `line` | integer | Yes | Line number (1-based) |
| `column` | integer | No | Column for targeting specific sequence point (lambda/inline) |
| `log_message` | string | No | Log message template with `{expression}` placeholders |
| `hit_count_multiple` | integer | No | Notify only every Nth hit (0 = every hit) |
| `max_notifications` | integer | No | Auto-disable after N notifications (0 = unlimited) |

**Examples:**

Basic tracepoint:
```json
{
  "file": "Services/OrderService.cs",
  "line": 55
}
```

Tracepoint with log message:
```json
{
  "file": "Services/OrderService.cs",
  "line": 55,
  "log_message": "Processing order {orderId}, total: {order.Total}"
}
```

High-frequency tracepoint with filtering:
```json
{
  "file": "Services/DataProcessor.cs",
  "line": 120,
  "log_message": "Processed item {i} of {total}",
  "hit_count_multiple": 100,
  "max_notifications": 10
}
```

**Response:**
```json
{
  "success": true,
  "tracepoint": {
    "id": "tp-550e8400-e29b-41d4-a716-446655440000",
    "type": "tracepoint",
    "location": {
      "file": "/app/Services/OrderService.cs",
      "line": 55
    },
    "state": "bound",
    "enabled": true,
    "logMessage": "Processing order {orderId}, total: {order.Total}"
  }
}
```

**MCP Notification (sent when tracepoint hit):**
```json
{
  "method": "debugger/breakpointHit",
  "params": {
    "breakpointId": "tp-550e8400-e29b-41d4-a716-446655440000",
    "type": "tracepoint",
    "location": {
      "file": "/app/Services/OrderService.cs",
      "line": 55
    },
    "threadId": 5,
    "timestamp": "2024-01-15T10:30:45.123Z",
    "hitCount": 42,
    "logMessage": "Processing order ORD-12345, total: 299.99"
  }
}
```

**Log message expressions:**
- Use `{variableName}` to interpolate variable values
- Use `{object.Property}` to access properties
- Use `{{` and `}}` for literal braces
- Errors show as `<error: ExceptionType>`

**Real-world use case:** An AI agent investigating slow order processing sets tracepoints at key stages with `hit_count_multiple: 1000`. After running a load test, the notifications reveal that 95% of time is spent in the validation step — without ever stopping the application.

**Managing tracepoints:** Read the `debugger://breakpoints` resource to see all tracepoints (they appear with `type: "tracepoint"`). Use `breakpoint_enable` to temporarily disable notifications. Use `breakpoint_remove` to delete.

---

### exception_get_context

Get full exception context when paused at an exception.

**Requires:** Paused session (at an exception)

**When to use:** After an exception breakpoint fires (or the process stops on an unhandled exception), use this to get everything at once: exception details, inner exception chain, stack frames with source locations, and local variables in the throwing frame. This is the "exception autopsy" tool — one call replaces multiple `stacktrace_get` + `variables_get` + `evaluate` calls.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `max_frames` | integer | No | Maximum stack frames to return (1–100, default: 10) |
| `include_variables_for_frames` | integer | No | Number of top frames to include local variables for (0–10, default: 1). Set to 0 to skip variable collection. |
| `max_inner_exceptions` | integer | No | Maximum inner exception chain depth (0–20, default: 5). Set to 0 to skip inner exceptions. |

**Example request:**
```json
{
  "max_frames": 5,
  "include_variables_for_frames": 2,
  "max_inner_exceptions": 3
}
```

**Example response:**
```json
{
  "threadId": 5,
  "exception": {
    "type": "System.InvalidOperationException",
    "message": "Sequence contains no matching element",
    "isFirstChance": true,
    "stackTraceString": "   at System.Linq.Enumerable.First[TSource](...)\n   at MyApp.Services.UserService.GetUser(String userId) in /app/Services/UserService.cs:line 42"
  },
  "innerExceptions": [
    {
      "type": "System.Collections.Generic.KeyNotFoundException",
      "message": "The given key 'user-123' was not present in the dictionary.",
      "depth": 1
    }
  ],
  "innerExceptionsTruncated": false,
  "frames": [
    {
      "index": 0,
      "function": "System.Linq.Enumerable.First",
      "module": "System.Linq.dll",
      "isExternal": true,
      "location": null,
      "arguments": null,
      "variables": null
    },
    {
      "index": 1,
      "function": "MyApp.Services.UserService.GetUser",
      "module": "MyApp.dll",
      "isExternal": false,
      "location": {
        "file": "/app/Services/UserService.cs",
        "line": 42,
        "column": 8,
        "functionName": "GetUser",
        "moduleName": "MyApp.dll"
      },
      "arguments": [
        {
          "name": "userId",
          "type": "System.String",
          "value": "\"user-123\"",
          "scope": "Argument",
          "hasChildren": false
        }
      ],
      "variables": {
        "locals": [
          {
            "name": "users",
            "type": "System.Collections.Generic.List<User>",
            "value": "Count = 0",
            "scope": "Local",
            "hasChildren": true,
            "childrenCount": 0
          }
        ],
        "errors": null
      }
    },
    {
      "index": 2,
      "function": "MyApp.Controllers.UserController.HandleRequest",
      "module": "MyApp.dll",
      "isExternal": false,
      "location": {
        "file": "/app/Controllers/UserController.cs",
        "line": 15,
        "column": 12,
        "functionName": "HandleRequest",
        "moduleName": "MyApp.dll"
      },
      "arguments": null,
      "variables": null
    }
  ],
  "totalFrames": 12,
  "throwingFrameIndex": 0
}
```

**Real-world use case:** An AI agent catches a `NullReferenceException` via a `debugger/breakpointHit` notification. Instead of making 3 separate calls to get the stack trace, variables, and exception details, it calls `exception_get_context` once. The response shows that `users` is an empty list and the code called `.First()` without checking — the agent immediately identifies the fix: use `.FirstOrDefault()` with a null check.

---

## Breakpoint Notifications

The debugger sends **push notifications** via MCP when breakpoints and tracepoints are hit —
there is no polling tool for this. This enables event-driven debugging workflows: continue
execution, then wait for the `debugger/breakpointHit` notification to arrive (or read the
`debugger://breakpoints` resource afterward to confirm hit counts).

### Notification Method

**Method:** `debugger/breakpointHit`

This notification is sent whenever:
- A blocking breakpoint is hit (execution pauses)
- A tracepoint is hit (execution continues)
- An exception breakpoint is triggered

### Notification Format

```json
{
  "method": "debugger/breakpointHit",
  "params": {
    "breakpointId": "bp-550e8400-e29b-41d4-a716-446655440000",
    "type": "blocking",
    "location": {
      "file": "/app/Services/UserService.cs",
      "line": 42,
      "column": 8,
      "functionName": "GetUser",
      "moduleName": "MyApp.dll"
    },
    "threadId": 5,
    "timestamp": "2024-01-15T10:30:45.123Z",
    "hitCount": 1,
    "logMessage": null,
    "exceptionInfo": null
  }
}
```

### Type Field

| Value | Description |
|-------|-------------|
| `blocking` | Regular breakpoint — execution is paused |
| `tracepoint` | Tracepoint — execution continues, notification only |

### Exception Info (when applicable)

For exception breakpoints, the notification includes exception details:

```json
{
  "exceptionInfo": {
    "type": "System.NullReferenceException",
    "message": "Object reference not set to an instance of an object.",
    "isFirstChance": true,
    "stackTrace": "   at MyApp.Services.UserService.GetUser(String userId)..."
  }
}
```

### Notifications vs. Polling the Resource

| Approach | Use Case |
|----------|----------|
| `debugger/breakpointHit` notification | Event-driven agents — react the instant a breakpoint or tracepoint fires, no polling loop |
| `debugger://breakpoints` resource | Point-in-time inspection — confirm current hit counts, or check state after missing a notification |

Both reflect the same underlying hits: the notification is pushed the moment a breakpoint or
tracepoint fires; the resource reflects the same `hitCount` the next time it's read. Use
whichever fits your workflow — most agents subscribe to the notification and only fall back to
reading the resource if they need to double-check state.
