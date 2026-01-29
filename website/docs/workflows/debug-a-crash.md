---
title: Debug a Crash
sidebar_position: 1
---

# Workflow: Debug a Crash

This guide walks through using debug-mcp to find the root cause of a crash (unhandled exception) in a .NET application. This is the most common debugging workflow.

## Scenario

Your .NET application throws a `NullReferenceException` at runtime. You want an AI agent to find exactly where it happens, inspect the state, and identify the root cause.

## Steps

### 1. Launch the application

```json
// debug_launch
{
  "program": "/app/MyService.dll",
  "stop_at_entry": true
}
```

The process starts and immediately stops at the entry point.

### 2. Set an exception breakpoint

```json
// breakpoint_set_exception
{
  "exception_type": "System.NullReferenceException",
  "break_on_first_chance": true,
  "include_subtypes": true
}
```

This tells the debugger to break the moment a `NullReferenceException` is thrown — before any catch block runs.

### 3. Continue execution

```json
// debug_continue
{}
```

The application runs normally until the exception is thrown.

### 4. Wait for the exception

```json
// breakpoint_wait
{
  "timeout_ms": 60000
}
```

Blocks until the exception breakpoint fires. When it does, you get the exact location:

```json
{
  "hit": true,
  "location": {
    "file": "/app/Services/UserService.cs",
    "line": 42,
    "function": "GetUser"
  }
}
```

### 5. Get the stack trace

```json
// stacktrace_get
{}
```

See the full call chain that led to the exception:

```json
{
  "frames": [
    { "index": 0, "function": "GetUser", "file": "UserService.cs", "line": 42 },
    { "index": 1, "function": "HandleRequest", "file": "RequestHandler.cs", "line": 15 },
    { "index": 2, "function": "ProcessAsync", "file": "Pipeline.cs", "line": 88 }
  ]
}
```

### 6. Inspect variables at the crash site

```json
// variables_get
{
  "frame_index": 0,
  "scope": "all"
}
```

See what values the local variables have:

```json
{
  "variables": [
    { "name": "userId", "type": "string", "value": "\"abc123\"" },
    { "name": "user", "type": "User", "value": "null" }
  ]
}
```

The `user` variable is `null` — that's the source of the `NullReferenceException`.

### 7. Evaluate an expression to understand why

```json
// evaluate
{
  "expression": "this._repository.FindById(userId)"
}
```

```json
{
  "result": "null",
  "type": "User"
}
```

The repository returns `null` for this userId — the code doesn't handle this case.

### 8. Disconnect

```json
// debug_disconnect
{
  "terminate": true
}
```

## Summary

| Step | Tool | Purpose |
|------|------|---------|
| 1 | `debug_launch` | Start the app under debugger |
| 2 | `breakpoint_set_exception` | Catch the exception at the throw site |
| 3 | `debug_continue` | Let the app run |
| 4 | `breakpoint_wait` | Wait for the exception |
| 5 | `stacktrace_get` | See the call chain |
| 6 | `variables_get` | Inspect local state |
| 7 | `evaluate` | Test hypotheses |
| 8 | `debug_disconnect` | Clean up |

## Tips

- Use `break_on_first_chance: true` to catch exceptions before any catch block handles them.
- Use `include_subtypes: true` to catch derived exception types (e.g., setting a breakpoint on `System.Exception` catches all exceptions).
- If the crash only happens with specific input, use `debug_launch` with `args` to pass that input.
