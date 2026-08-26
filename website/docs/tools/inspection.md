---
title: Inspection
sidebar_position: 4
---

# Inspection

Inspection tools let you examine the runtime state of a stopped process — stack traces, local variables, expression evaluation, and object/collection summaries.

## When to Use

Use inspection tools after the process is stopped (at a breakpoint or after a step). These tools answer "what is happening?" and "what values do things have?" — the core of debugging.

**Typical flow:** `stacktrace_get` → `variables_get` → `evaluate` (for complex expressions) → `object_inspect` (for deep object details)

## Managed threads

There is no `threads_list` tool. Read the **`debugger://threads`** MCP resource instead — it
returns the same managed-thread list (id, name, state, whether it's current, and its current
location if stopped), without needing a round-trip tool call. The resource also carries a
`stale` flag and a `capturedAt` timestamp so a client can tell whether the snapshot is still
fresh relative to the current debug state.

## Tools

### stacktrace_get

Get the stack trace for a thread.

**Requires:** Paused session

**When to use:** Understand the call chain that led to the current point. Shows you which methods called which, with source file and line information.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `thread_id` | integer | No | Thread ID (default: current) |
| `start_frame` | integer | No | Start from frame N (default: 0) |
| `max_frames` | integer | No | Max frames to return (default: 20) |
| `timeout_ms` | integer | No | Maximum time to wait for the stack trace, in milliseconds (default: 30000). Accepted for consistency with the other inspection tools; the underlying call is synchronous and returns immediately, so this parameter currently has no effect. |

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

**Requires:** Paused session

**When to use:** Inspect local variables, method arguments, and `this` at a specific point in the call stack. Use `expand` to drill into object fields.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `thread_id` | integer | No | Thread ID (default: current) |
| `frame_index` | integer | No | Frame index (default: 0 = top) |
| `scope` | string | No | `locals`, `arguments`, `this`, or `all` (default: `all`) |
| `expand` | string | No | Variable path to expand children |
| `timeout_ms` | integer | No | Maximum time to wait for variable retrieval, in milliseconds (default: 30000). Accepted for consistency with the other inspection tools; the underlying call is synchronous and returns immediately, so this parameter currently has no effect. |

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

**Requires:** Paused session

**When to use:** Compute values that aren't directly visible as local variables — call methods, access properties, run LINQ queries, or test hypotheses about the bug.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `expression` | string | Yes | C# expression to evaluate |
| `thread_id` | integer | No | Thread context |
| `frame_index` | integer | No | Stack frame context |
| `format` | string | No | Output format: `default`, `hex`, `binary` |
| `timeout_ms` | integer | No | Evaluation timeout in ms (default: 5000) |

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

### evaluate_safe

Evaluate a C# expression in safe mode — static analysis blocks method calls, object construction, and assignments before they ever reach the debugged process.

**Requires:** Paused session

**When to use:** You're an autonomous agent evaluating expressions without a human reviewing each one first. `evaluate` will run arbitrary code (including side-effecting method calls); `evaluate_safe` statically rejects anything beyond member reads, property access, arithmetic, comparisons (`==`,`!=`,`<`,`>`,`<=`,`>=`), logical operators (`&&`,`||`,`!`), the ternary operator (`?:`), indexers, null-conditional access (`?.`,`?[]`), and a small allowlist of known-safe methods. Blocked: any other method call, `new T()`, and assignments.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `expression` | string | Yes | C# expression to evaluate safely |
| `thread_id` | integer | No | Thread context (default: current thread) |
| `frame_index` | integer | No | Stack frame context, 0 = top (default: 0) |
| `timeout_ms` | integer | No | Evaluation timeout in ms, applied only if the expression passes the safety check (default: 5000) |

**Example:**
```json
{ "expression": "order.Total > 0 && order.Customer?.IsActive == true" }
```

**Response (allowed):**
```json
{
  "success": true,
  "value": "42",
  "type": "System.Int32",
  "has_children": false
}
```

**Response (rejected):**
```json
{
  "success": false,
  "error": {
    "code": "safe_eval_rejected",
    "message": "Method call 'DeleteAllOrders()' is not on the safe-eval allowlist",
    "details": {
      "rejection_category": "MethodCall",
      "offending_expression": "order.DeleteAllOrders()",
      "allowed_operations": "member reads, property access, arithmetic (+,-,*,/,%), comparisons (==,!=,<,>,<=,>=), logical (&&,||,!), ternary (?:), indexers, null-conditional (?.,?[]), and methods on the safe-eval allowlist"
    }
  }
}
```

**Real-world use case:** An autonomous agent testing a hypothesis about a null-check bug evaluates `order.Customer?.Address?.PostalCode` in a loop across many breakpoint hits without a human confirming each expression — `evaluate_safe` guarantees none of those expressions can mutate state or call into arbitrary code.

---

### object_inspect

Inspect a heap object's contents including all fields, sizes, and addresses.

**Requires:** Paused session

**When to use:** Get detailed information about an object beyond what `variables_get` shows — field offsets, memory addresses, sizes, and deep nested expansion.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `object_ref` | string | Yes | Object reference (variable name or expression) |
| `depth` | integer | No | Max depth for nested expansion (1-10, default: 1) |
| `thread_id` | integer | No | Thread ID (default: current) |
| `frame_index` | integer | No | Frame index (default: 0) |
| `timeout_ms` | integer | No | Maximum time to wait for the object inspection, in milliseconds (default: 30000) |

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

---

### object_summarize

Summarize an object's fields in a single call: non-default valued fields, null fields, and anomalous fields.

**Requires:** Paused session

**When to use:** You want a quick read on an object's state without wading through every field via `object_inspect`. `object_summarize` buckets fields into "has a value", "is null", and "interesting" (flagged anomalies: empty strings, `NaN`, `Infinity`, default/unset `DateTime`, empty GUIDs). Collection-typed fields show their element count and type inline instead of expanding.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `expression` | string | Yes | Variable name or expression evaluating to an object |
| `max_preview_items` | integer | No | Max collection elements to preview inline for collection-typed fields (1-50, default: 5) |
| `thread_id` | integer | No | Thread context (default: current thread) |
| `frame_index` | integer | No | Stack frame context, 0 = top (default: 0) |
| `timeout_ms` | integer | No | Evaluation timeout in ms (default: 5000) |

**Example:**
```json
{ "expression": "customer" }
```

**Response:**
```json
{
  "success": true,
  "summary": {
    "typeName": "MyApp.Models.Customer",
    "size": 48,
    "isNull": false,
    "totalFieldCount": 6,
    "inaccessibleFieldCount": 0,
    "fields": [
      { "name": "Id", "type": "System.Int32", "value": "42" },
      { "name": "Orders", "type": "List<Order>", "value": "Count = 3", "collectionCount": 3, "collectionElementType": "MyApp.Models.Order" }
    ],
    "nullFields": ["MiddleName"],
    "interestingFields": [
      { "name": "Email", "type": "System.String", "value": "\"\"", "reason": "empty string" },
      { "name": "LastLogin", "type": "System.DateTime", "value": "0001-01-01T00:00:00", "reason": "default DateTime" }
    ]
  }
}
```

**Real-world use case:** An AI agent inspects a `Customer` object suspected of having incomplete data. Instead of scanning through every one of 20 fields with `object_inspect`, `object_summarize` immediately surfaces `Email` as an empty string and `LastLogin` as a default `DateTime` — the two anomalies worth investigating.

---

### collection_analyze

Analyze a collection variable and return a structured summary: count, element types, null count, first/last previews, and numeric statistics.

**Requires:** Paused session

**When to use:** You have an array, `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, or similar, and need to understand its shape without dumping every element. Replaces the 5-50+ calls it would otherwise take to page through a large collection via `variables_get`/`object_inspect`.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `expression` | string | Yes | Variable name or expression evaluating to a collection |
| `max_preview_items` | integer | No | Number of first/last elements to include in the preview (1-50, default: 5) |
| `thread_id` | integer | No | Thread context (default: current thread) |
| `frame_index` | integer | No | Stack frame context, 0 = top (default: 0) |
| `timeout_ms` | integer | No | Evaluation timeout in ms (default: 5000) |

**Example:**
```json
{ "expression": "orders", "max_preview_items": 3 }
```

**Response:**
```json
{
  "success": true,
  "summary": {
    "count": 1200,
    "elementType": "MyApp.Models.Order",
    "collectionType": "System.Collections.Generic.List`1[MyApp.Models.Order]",
    "kind": "List",
    "nullCount": 0,
    "numericStats": null,
    "typeDistribution": null,
    "firstElements": [
      { "index": 0, "value": "{Order Id=1}", "type": "MyApp.Models.Order" }
    ],
    "lastElements": [
      { "index": 1199, "value": "{Order Id=1200}", "type": "MyApp.Models.Order" }
    ],
    "keyValuePairs": null,
    "isSampled": false
  }
}
```

**Response (numeric collection):**
```json
{
  "success": true,
  "summary": {
    "count": 500,
    "elementType": "System.Double",
    "collectionType": "System.Double[]",
    "kind": "Array",
    "nullCount": 0,
    "numericStats": { "min": "-3.2", "max": "912.7", "average": "104.6" },
    "firstElements": [ { "index": 0, "value": "12.4", "type": "System.Double" } ],
    "lastElements": [ { "index": 499, "value": "88.1", "type": "System.Double" } ],
    "isSampled": false
  }
}
```

Large previews are subject to the standard 256 KB response size budget; when trimmed, a `truncation` object is included alongside `summary` — the collection's `firstElements`, `lastElements`, `typeDistribution`, and `keyValuePairs` each get an independent share of that budget.

**Errors:**
- `not_collection` — Expression is not a recognized collection type
- `NOT_PAUSED` — Process must be paused
- `variable_unavailable` — Expression could not be resolved in the current scope

**Real-world use case:** An AI agent investigating a performance regression calls `collection_analyze` on a 10,000-element list to instantly see it's `IsSampled: true` with a skewed numeric distribution, instead of paging through elements one `variables_get` call at a time.
