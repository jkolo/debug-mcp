---
title: Breakpoints
sidebar_position: 2
---

# Breakpoints

Breakpoint tools let you set, manage, and wait for breakpoints — the primary mechanism for stopping execution at specific points in your code.

import AsciinemaPlayer from '@site/src/components/AsciinemaPlayer';

<AsciinemaPlayer src="/casts/breakpoint-workflow.cast" rows={24} cols={120} idleTimeLimit={2} speed={1.5} />

## When to Use

Use breakpoint tools to control where execution pauses. You can break on specific source lines, function entries, exception types, or conditions. Use `breakpoint_wait` to block until a breakpoint is actually hit.

**Typical flow:** `breakpoint_set` → `debug_continue` → `breakpoint_wait` → *(inspect state)*

## Tools

### breakpoint_set

Set a breakpoint in source code.

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

### breakpoint_list

List all breakpoints.

**When to use:** Review which breakpoints are set, their hit counts, and whether they're verified (bound to code).

**Parameters:** None

**Response:**
```json
{
  "breakpoints": [
    {
      "id": 1,
      "verified": true,
      "enabled": true,
      "file": "/app/Services/UserService.cs",
      "line": 42,
      "hit_count": 3,
      "condition": null
    },
    {
      "id": 2,
      "verified": false,
      "enabled": true,
      "file": "/app/Services/OrderService.cs",
      "line": 100,
      "hit_count": 0,
      "condition": "order.Total > 1000"
    }
  ]
}
```

---

### breakpoint_enable

Enable or disable a breakpoint without removing it.

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

### breakpoint_wait

Wait for any breakpoint to be hit.

**When to use:** After setting breakpoints and continuing execution, use this to block until a breakpoint fires. Without this, you'd have to poll `debug_state`.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `timeout_ms` | integer | No | Timeout in milliseconds (default: 30000) |
| `breakpoint_id` | integer | No | Wait for a specific breakpoint |

**Example:**
```json
{
  "timeout_ms": 60000,
  "breakpoint_id": 1
}
```

**Response (hit):**
```json
{
  "hit": true,
  "breakpoint_id": 1,
  "thread_id": 5,
  "location": {
    "file": "/app/Services/UserService.cs",
    "line": 42,
    "column": 8,
    "function": "GetUser",
    "module": "MyApp.dll"
  },
  "hit_count": 1
}
```

**Response (timeout):**
```json
{
  "hit": false,
  "reason": "timeout",
  "message": "No breakpoint hit within 60000ms"
}
```
