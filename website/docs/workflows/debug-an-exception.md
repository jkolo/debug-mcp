---
title: Debug an Exception
sidebar_position: 2
---

# Workflow: Debug an Exception

This guide walks through using debug-mcp to catch, analyze, and diagnose exceptions in a .NET application — from simple crashes to complex exception chains.

## Scenario

Your .NET application throws an exception at runtime. You want an AI agent to catch it at the throw site, inspect the state, and identify the root cause.

## Steps

### 1. Launch the application

> "Launch `/app/MyService.dll` and stop at the entry point"

The process starts and pauses at the entry point.

<details>
<summary>Tool call details</summary>

**Request** (`debug_launch`):
```json
{
  "program": "/app/MyService.dll",
  "stop_at_entry": true
}
```

</details>

### 2. Set an exception breakpoint

> "Break on any NullReferenceException"

The debugger will pause the instant a matching exception is thrown — before any catch block runs.

<details>
<summary>Tool call details</summary>

**Request** (`breakpoint_set_exception`):
```json
{
  "exception_type": "System.NullReferenceException",
  "break_on_first_chance": true,
  "include_subtypes": true
}
```

</details>

### 3. Continue and wait for the exception

> "Continue and wait for the exception to fire"

The application runs normally until the exception is thrown.

<details>
<summary>Tool call details</summary>

**Request** (`debug_continue`):
```json
{}
```

Then:

**Request** (`breakpoint_wait`):
```json
{
  "timeout_ms": 60000
}
```

</details>

When the exception fires, you get the exact location:

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

### 4. Quick diagnosis with autopsy

For a fast overview, use `include_autopsy: true` on `breakpoint_wait` (step 3). This returns the exception, stack frames, and local variables in a single response.

<details>
<summary>Tool call details</summary>

**Request** (`breakpoint_wait`):
```json
{
  "timeout_ms": 60000,
  "include_autopsy": true
}
```

The response includes the full autopsy:
```json
{
  "hit": true,
  "autopsy": {
    "exception": {
      "type": "System.NullReferenceException",
      "message": "Object reference not set to an instance of an object.",
      "isFirstChance": true
    },
    "frames": [
      {
        "index": 0,
        "function": "MyApp.Services.UserService.GetUser",
        "location": { "file": "/app/Services/UserService.cs", "line": 42 },
        "variables": {
          "locals": [
            { "name": "user", "type": "User", "value": "null" },
            { "name": "userId", "type": "string", "value": "\"abc123\"" }
          ]
        }
      }
    ]
  }
}
```

</details>

### 5. Deep dive with exception_get_context

If you need more detail — more frames, more variables, inner exception chains — call `exception_get_context`:

> "Get the full exception context with 20 frames and inner exceptions"

<details>
<summary>Tool call details</summary>

**Request** (`exception_get_context`):
```json
{
  "max_frames": 20,
  "include_variables_for_frames": 3,
  "max_inner_exceptions": 5
}
```

</details>

This returns up to 20 stack frames, local variables for the top 3 frames, and up to 5 levels of inner exceptions.

### 6. Manual exploration

For fine-grained investigation, use individual tools:

> "Show me the stack trace"

<details>
<summary>Tool call details</summary>

**Request** (`stacktrace_get`):
```json
{}
```

</details>

> "Show me the variables in the top frame"

<details>
<summary>Tool call details</summary>

**Request** (`variables_get`):
```json
{
  "frame_index": 0,
  "scope": "all"
}
```

</details>

> "Evaluate `this._repository.FindById(userId)`"

<details>
<summary>Tool call details</summary>

**Request** (`evaluate`):
```json
{
  "expression": "this._repository.FindById(userId)"
}
```

</details>

### 7. Disconnect

> "Stop debugging"

<details>
<summary>Tool call details</summary>

**Request** (`debug_disconnect`):
```json
{
  "terminate": true
}
```

</details>

## Choosing Your Approach

| Approach | Tool calls | Best for |
|----------|-----------|----------|
| **Autopsy** (`breakpoint_wait` + `include_autopsy`) | 1 call | Quick diagnosis — get everything at once |
| **Deep dive** (`exception_get_context`) | 1 call | More detail — control frame count, variable depth, inner chain |
| **Manual** (`stacktrace_get` + `variables_get` + `evaluate`) | 3+ calls | Fine-grained exploration after initial autopsy |

## Summary

| Step | Tool | Purpose |
|------|------|---------|
| 1 | `debug_launch` | Start the app under debugger |
| 2 | `breakpoint_set_exception` | Catch exceptions at the throw site |
| 3 | `debug_continue` + `breakpoint_wait` | Run and wait for the exception |
| 4 | `breakpoint_wait` (with autopsy) | Quick one-call diagnosis |
| 5 | `exception_get_context` | Deep dive with full context |
| 6 | `stacktrace_get` / `variables_get` / `evaluate` | Manual exploration |
| 7 | `debug_disconnect` | Clean up |

## Tips

- **Use `break_on_first_chance: true`** to catch exceptions before any catch block handles them. If you wait for unhandled, the original call stack may be unwound.
- **Use `include_subtypes: true`** to catch derived exception types. Setting a breakpoint on `System.Exception` catches all exceptions.
- **Inner exceptions reveal root causes.** An `InvalidOperationException` might wrap an `IOException` which wraps a `SocketException`. Set `max_inner_exceptions` high enough to see the full chain.
- **Variables are collected per-frame.** Set `include_variables_for_frames: 3` to see locals in the throwing frame plus its two callers.
- **Combine with tracepoints.** Set tracepoints on key methods with log messages, then set an exception breakpoint. When the exception fires, the tracepoint log shows what happened leading up to it.
- If the crash only happens with specific input, use `debug_launch` with `args` to pass that input.
