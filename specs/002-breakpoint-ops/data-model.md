# Data Model: Breakpoint Operations

**Feature**: 002-breakpoint-ops
**Date**: 2026-01-17
**Extends**: 001-debug-session data model

## Overview

This document defines domain entities for breakpoint management in DotnetMcp,
building on the session management entities from 001-debug-session.

---

## Entities

### Breakpoint

Represents a debugging pause point in source code.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique identifier (UUID format) |
| `location` | BreakpointLocation | Yes | Source location (file, line) |
| `state` | BreakpointState | Yes | Current state (pending/bound/disabled) |
| `enabled` | bool | Yes | User-controlled enable flag |
| `verified` | bool | Yes | True if bound to executable code |
| `condition` | string | No | Optional condition expression (C# syntax) |
| `hitCount` | int | Yes | Number of times breakpoint has been hit |
| `message` | string | No | Status message (e.g., why unverified) |

**Validation Rules**:
- `id` must be non-empty UUID string
- `hitCount` must be >= 0
- `condition` if present must be valid C# expression

---

### BreakpointLocation

Represents a position in source code with optional column for lambda targeting.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | string | Yes | Absolute path to source file |
| `line` | int | Yes | 1-based line number |
| `column` | int | No | 1-based column (for targeting lambdas/inline statements) |
| `endLine` | int | No | End line from PDB sequence point |
| `endColumn` | int | No | End column from PDB sequence point |
| `functionName` | string | No | Name of containing function |
| `moduleName` | string | No | Name of containing module/assembly |

**Validation Rules**:
- `file` must be absolute path
- `line` must be >= 1
- `column` if present must be >= 1 (used to select specific sequence point on line)
- When multiple sequence points exist on same line, column selects the target

**Column Usage (Lambda Targeting)**:
```csharp
// Line 42: items.Where(x => x.Active).Select(x => x.Name)
//          ^col5                     ^col30
// column=5 breaks on Where predicate, column=30 breaks on Select predicate
```

---

### BreakpointState

Enumeration of breakpoint lifecycle states.

| Value | Description |
|-------|-------------|
| `pending` | Location specified but module not loaded |
| `bound` | Successfully bound to IL code |
| `disabled` | Explicitly disabled by user |

---

### BreakpointHit

Information about a triggered breakpoint event.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `breakpointId` | string | Yes | ID of the hit breakpoint |
| `threadId` | int | Yes | Thread that hit the breakpoint |
| `timestamp` | DateTime | Yes | UTC time when breakpoint was hit |
| `location` | BreakpointLocation | Yes | Exact location of the hit |
| `hitCount` | int | Yes | Hit count at time of hit |

---

### ExceptionBreakpoint

Breakpoint that triggers on exception throws (P6 priority feature).

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique identifier (UUID format) |
| `exceptionType` | string | Yes | Full type name (e.g., "System.NullReferenceException") |
| `breakOnFirstChance` | bool | Yes | Break when thrown (before catch handlers) |
| `breakOnSecondChance` | bool | Yes | Break when unhandled |
| `includeSubtypes` | bool | Yes | Match derived exception types |
| `enabled` | bool | Yes | User-controlled enable flag |
| `verified` | bool | Yes | True if exception type exists in loaded assemblies |
| `hitCount` | int | Yes | Number of times triggered |

**Validation Rules**:
- `exceptionType` must be valid .NET type name format
- At least one of `breakOnFirstChance` or `breakOnSecondChance` must be true

---

### ExceptionInfo

Information about an exception that triggered an exception breakpoint.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `type` | string | Yes | Exception type name |
| `message` | string | Yes | Exception message |
| `isFirstChance` | bool | Yes | True if first-chance, false if unhandled |
| `stackTrace` | string | No | Exception stack trace if available |

---

## Response Schemas

### SetBreakpointResponse

Returned by `breakpoint_set` on success.

```json
{
  "success": true,
  "breakpoint": {
    "id": "bp-550e8400-e29b-41d4-a716-446655440000",
    "location": {
      "file": "/app/Services/UserService.cs",
      "line": 42,
      "functionName": "GetUser",
      "moduleName": "MyApp"
    },
    "state": "bound",
    "enabled": true,
    "verified": true,
    "hitCount": 0
  }
}
```

**When pending** (module not loaded):

```json
{
  "success": true,
  "breakpoint": {
    "id": "bp-550e8400-e29b-41d4-a716-446655440001",
    "location": {
      "file": "/app/Services/LazyService.cs",
      "line": 15
    },
    "state": "pending",
    "enabled": true,
    "verified": false,
    "hitCount": 0,
    "message": "Module not yet loaded; breakpoint will bind when module loads"
  }
}
```

**With condition**:

```json
{
  "success": true,
  "breakpoint": {
    "id": "bp-550e8400-e29b-41d4-a716-446655440002",
    "location": {
      "file": "/app/Services/UserService.cs",
      "line": 50
    },
    "state": "bound",
    "enabled": true,
    "verified": true,
    "condition": "userId > 100",
    "hitCount": 0
  }
}
```

---

### RemoveBreakpointResponse

Returned by `breakpoint_remove` on success.

```json
{
  "success": true,
  "message": "Breakpoint bp-550e8400-e29b-41d4-a716-446655440000 removed"
}
```

---

### ListBreakpointsResponse

Returned by `breakpoint_list`.

```json
{
  "breakpoints": [
    {
      "id": "bp-550e8400-e29b-41d4-a716-446655440000",
      "location": {
        "file": "/app/Services/UserService.cs",
        "line": 42,
        "functionName": "GetUser",
        "moduleName": "MyApp"
      },
      "state": "bound",
      "enabled": true,
      "verified": true,
      "hitCount": 3
    },
    {
      "id": "bp-550e8400-e29b-41d4-a716-446655440001",
      "location": {
        "file": "/app/Services/LazyService.cs",
        "line": 15
      },
      "state": "pending",
      "enabled": true,
      "verified": false,
      "hitCount": 0,
      "message": "Module not yet loaded"
    }
  ],
  "count": 2
}
```

**Empty list**:

```json
{
  "breakpoints": [],
  "count": 0
}
```

---

### WaitBreakpointResponse

Returned by `breakpoint_wait` when a breakpoint is hit.

```json
{
  "hit": true,
  "breakpointId": "bp-550e8400-e29b-41d4-a716-446655440000",
  "threadId": 1,
  "timestamp": "2026-01-17T10:30:45.123Z",
  "location": {
    "file": "/app/Services/UserService.cs",
    "line": 42,
    "column": 5,
    "functionName": "GetUser",
    "moduleName": "MyApp"
  },
  "hitCount": 4,
  "stackFrame": {
    "functionName": "GetUser",
    "file": "/app/Services/UserService.cs",
    "line": 42
  }
}
```

**Timeout**:

```json
{
  "hit": false,
  "timeout": true,
  "message": "No breakpoint hit within 30000ms"
}
```

---

## Error Response Schema

All tools return this structure when `isError: true`:

```json
{
  "error": {
    "code": "BREAKPOINT_NOT_FOUND",
    "message": "No breakpoint with ID bp-invalid-id",
    "details": {
      "requestedId": "bp-invalid-id"
    }
  }
}
```

### Error Codes

| Code | Description |
|------|-------------|
| `NO_SESSION` | No active debug session (use debug_attach/launch first) |
| `INVALID_FILE` | Source file path not found in any loaded module |
| `INVALID_LINE` | Line does not contain executable code |
| `INVALID_CONDITION` | Condition expression has syntax error |
| `BREAKPOINT_NOT_FOUND` | Breakpoint ID does not exist |
| `BREAKPOINT_EXISTS` | Breakpoint already exists at this location (returns existing ID) |
| `EVAL_FAILED` | Condition evaluation failed at runtime |
| `TIMEOUT` | Wait operation timed out |

---

## State Relationships

### Breakpoint Lifecycle

```
[No Breakpoint]
      │
      │ breakpoint_set(file, line)
      ▼
┌─────────────┐     Module loads     ┌─────────────┐
│   pending   │ ──────────────────→  │    bound    │
│ (unverified)│                      │  (verified) │
└─────────────┘                      └─────────────┘
      │                                    │
      │ breakpoint_remove                  │ breakpoint_remove
      ▼                                    ▼
[No Breakpoint]                     [No Breakpoint]
```

### Session-Breakpoint Dependency

```
DebugSession (from 001-debug-session)
      │
      │ 1:N relationship
      ▼
┌──────────────────┐
│   Breakpoint[]   │
│ (per session)    │
└──────────────────┘

When session disconnects:
- All breakpoints are removed
- No breakpoints persist across sessions
```

### Wait Queue Flow

```
breakpoint_wait(timeout)
      │
      ▼
┌──────────────────┐
│  Wait for:       │
│  - Hit event     │
│  - OR timeout    │
└──────────────────┘
      │
      ├── Hit event received
      │         ▼
      │   Return: hit=true, location, threadId
      │
      └── Timeout expired
                ▼
          Return: hit=false, timeout=true
```
