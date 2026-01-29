---
title: Inspection
sidebar_position: 4
---

# Inspection

Inspection tools let you examine the runtime state of a stopped process — threads, stack traces, local variables, and expression evaluation.

## When to Use

Use inspection tools after the process is stopped (at a breakpoint or after a step). These tools answer "what is happening?" and "what values do things have?" — the core of debugging.

**Typical flow:** `stacktrace_get` → `variables_get` → `evaluate` (for complex expressions) → `object_inspect` (for deep object details)

## Tools

### threads_list

List all managed threads.

**When to use:** See what threads exist, which one is current, and where each thread is executing. Useful for debugging multi-threaded issues.

**Parameters:** None

**Response:**
```json
{
  "threads": [
    {
      "id": 1,
      "name": "Main Thread",
      "state": "running",
      "is_current": false
    },
    {
      "id": 5,
      "name": ".NET ThreadPool Worker",
      "state": "stopped",
      "is_current": true,
      "location": {
        "function": "GetUser",
        "file": "UserService.cs",
        "line": 42
      }
    },
    {
      "id": 8,
      "name": "Background Worker",
      "state": "waiting"
    }
  ]
}
```

---

### stacktrace_get

Get the stack trace for a thread.

**When to use:** Understand the call chain that led to the current point. Shows you which methods called which, with source file and line information.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `thread_id` | integer | No | Thread ID (default: current) |
| `start_frame` | integer | No | Start from frame N (default: 0) |
| `max_frames` | integer | No | Max frames to return (default: 20) |

**Response:**
```json
{
  "thread_id": 5,
  "total_frames": 15,
  "frames": [
    {
      "index": 0,
      "function": "GetUser",
      "file": "/app/Services/UserService.cs",
      "line": 42,
      "column": 12,
      "module": "MyApp.dll",
      "arguments": [
        { "name": "userId", "type": "string", "value": "\"abc123\"" }
      ]
    },
    {
      "index": 1,
      "function": "Get",
      "file": "/app/Controllers/UserController.cs",
      "line": 28,
      "module": "MyApp.dll"
    },
    {
      "index": 2,
      "function": "InvokeAction",
      "module": "Microsoft.AspNetCore.Mvc.Core.dll",
      "is_external": true
    }
  ]
}
```

**Real-world use case:** After hitting an exception breakpoint, an AI agent calls `stacktrace_get` to see the full call chain. It identifies that the exception originated in `UserService.GetUser` (frame 0), called from `UserController.Get` (frame 1).

---

### variables_get

Get variables for a stack frame.

**When to use:** Inspect local variables, method arguments, and `this` at a specific point in the call stack. Use `expand` to drill into object fields.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `thread_id` | integer | No | Thread ID (default: current) |
| `frame_index` | integer | No | Frame index (default: 0 = top) |
| `scope` | string | No | `locals`, `arguments`, `this`, or `all` (default: `all`) |
| `expand` | string | No | Variable path to expand children |

**Response:**
```json
{
  "variables": [
    {
      "name": "this",
      "type": "MyApp.Services.UserService",
      "value": "{UserService}",
      "has_children": true,
      "children_count": 3
    },
    {
      "name": "userId",
      "type": "string",
      "value": "\"\"",
      "has_children": false,
      "scope": "argument"
    },
    {
      "name": "user",
      "type": "MyApp.Models.User",
      "value": "null",
      "has_children": false,
      "scope": "local"
    }
  ]
}
```

**Expanding children:**
```json
{
  "expand": "this._repository"
}
```

**Response:**
```json
{
  "variables": [
    {
      "name": "_connectionString",
      "type": "string",
      "value": "\"Server=localhost;...\"",
      "parent": "this._repository"
    },
    {
      "name": "_logger",
      "type": "ILogger<UserRepository>",
      "value": "{Logger}",
      "has_children": true,
      "parent": "this._repository"
    }
  ]
}
```

---

### evaluate

Evaluate a C# expression in the context of a stopped thread.

**When to use:** Compute values that aren't directly visible as local variables — call methods, access properties, run LINQ queries, or test hypotheses about the bug.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `expression` | string | Yes | C# expression to evaluate |
| `thread_id` | integer | No | Thread context |
| `frame_index` | integer | No | Stack frame context |
| `format` | string | No | Output format: `default`, `hex`, `binary` |

**Examples:**

Simple variable:
```json
{ "expression": "userId" }
```

Method call:
```json
{ "expression": "user?.GetFullName()" }
```

Complex expression:
```json
{ "expression": "users.Where(u => u.IsActive).Count()" }
```

**Response:**
```json
{
  "result": "\"John Doe\"",
  "type": "string",
  "has_children": false
}
```

**Error response:**
```json
{
  "error": true,
  "message": "Object reference not set to an instance of an object",
  "type": "NullReferenceException"
}
```

**Real-world use case:** An AI agent suspects a LINQ query returns wrong results. It uses `evaluate` to run `orders.Where(o => o.Status == "Pending").ToList()` and inspects the returned collection to confirm the bug hypothesis.

---

### object_inspect

Inspect a heap object's contents including all fields, sizes, and addresses.

**When to use:** Get detailed information about an object beyond what `variables_get` shows — field offsets, memory addresses, sizes, and deep nested expansion.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `object_ref` | string | Yes | Object reference (variable name or expression) |
| `depth` | integer | No | Max depth for nested expansion (1-10, default: 1) |
| `thread_id` | integer | No | Thread ID (default: current) |
| `frame_index` | integer | No | Frame index (default: 0) |

**Example:**
```json
{
  "object_ref": "customer",
  "depth": 2
}
```

**Response:**
```json
{
  "success": true,
  "inspection": {
    "address": "0x00007FF8A1234560",
    "typeName": "MyApp.Models.Customer",
    "size": 48,
    "fields": [
      {
        "name": "Id",
        "typeName": "System.Int32",
        "value": "42",
        "offset": 8,
        "size": 4,
        "hasChildren": false
      },
      {
        "name": "Name",
        "typeName": "System.String",
        "value": "\"John Doe\"",
        "offset": 16,
        "size": 8,
        "hasChildren": true
      },
      {
        "name": "Orders",
        "typeName": "System.Collections.Generic.List`1[MyApp.Models.Order]",
        "value": "Count = 3",
        "offset": 24,
        "size": 8,
        "hasChildren": true
      }
    ],
    "isNull": false,
    "hasCircularRef": false,
    "truncated": false
  }
}
```

**Errors:**
- `NOT_PAUSED` — Process must be paused
- `INVALID_REFERENCE` — Cannot resolve object reference
- `DEPTH_EXCEEDED` — Expansion depth exceeded limit
