---
title: Debug Exceptions
sidebar_position: 5
---

# Workflow: Debug Exceptions

This guide walks through using debug-mcp's exception autopsy tools to catch, analyze, and diagnose exceptions end-to-end. It combines exception breakpoints with the `exception_get_context` tool to get a complete picture in minimal tool calls.

## Scenario

Your .NET application throws an `InvalidOperationException` somewhere deep in the service layer. You want an AI agent to catch the exception at the throw site, get the full context (exception chain, stack frames, local variables), and identify the root cause — all without manually stepping through code.

## Steps

### 1. Launch the application

```json
// debug_launch
{
  "program": "/app/MyService.dll",
  "stop_at_entry": true
}
```

The process starts and pauses at the entry point.

### 2. Set an exception breakpoint

```json
// breakpoint_set_exception
{
  "exception_type": "System.InvalidOperationException",
  "break_on_first_chance": true,
  "include_subtypes": true
}
```

```json
{
  "id": "ebp-550e8400-e29b-41d4-a716-446655440000",
  "exception_type": "System.InvalidOperationException",
  "enabled": true,
  "state": "active"
}
```

The debugger will now pause the instant this exception type (or any subtype) is thrown.

### 3. Continue execution

```json
// debug_continue
{}
```

The application runs normally. If it's interactive, you can send input to trigger the exception:

```json
// process_write_input
{
  "data": "process-order ORD-999\n"
}
```

### 4. Wait for the exception with autopsy

```json
// breakpoint_wait
{
  "timeout_ms": 60000,
  "include_autopsy": true
}
```

The `include_autopsy: true` flag tells the debugger to automatically collect exception context when the breakpoint fires. The response includes everything you need:

```json
{
  "hit": true,
  "breakpoint_id": "ebp-550e8400-e29b-41d4-a716-446655440000",
  "thread_id": 5,
  "location": {
    "file": "/app/Services/OrderService.cs",
    "line": 87,
    "function": "ValidateOrder"
  },
  "autopsy": {
    "exception": {
      "type": "System.InvalidOperationException",
      "message": "Order ORD-999 has no line items",
      "isFirstChance": true
    },
    "innerExceptions": [],
    "frames": [
      {
        "index": 0,
        "function": "MyApp.Services.OrderService.ValidateOrder",
        "module": "MyApp.dll",
        "location": { "file": "/app/Services/OrderService.cs", "line": 87 },
        "variables": {
          "locals": [
            { "name": "order", "type": "Order", "value": "{Id=ORD-999, LineItems=Count:0}" },
            { "name": "rules", "type": "ValidationRules", "value": "{RequireLineItems=true}" }
          ]
        }
      },
      {
        "index": 1,
        "function": "MyApp.Services.OrderService.ProcessOrder",
        "module": "MyApp.dll",
        "location": { "file": "/app/Services/OrderService.cs", "line": 42 }
      }
    ],
    "totalFrames": 8
  }
}
```

From this single response, you can see: the order has zero line items, the validation requires at least one, and the exception is thrown at line 87.

### 5. Deep dive with exception_get_context (optional)

If `breakpoint_wait` didn't include autopsy, or you need more detail (more frames, more variables, inner exceptions), call `exception_get_context` directly:

```json
// exception_get_context
{
  "max_frames": 20,
  "include_variables_for_frames": 3,
  "max_inner_exceptions": 5
}
```

This returns the full autopsy result with up to 20 stack frames, local variables for the top 3 frames, and up to 5 levels of inner exceptions.

### 6. Inspect deeper with evaluate (optional)

If you need to test a hypothesis about the root cause:

```json
// evaluate
{
  "expression": "order.CreatedAt"
}
```

```json
{
  "result": "\"2026-01-15T00:00:00Z\"",
  "type": "System.DateTimeOffset"
}
```

### 7. Disconnect

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
| 2 | `breakpoint_set_exception` | Catch exceptions at the throw site |
| 3 | `debug_continue` | Let the app run |
| 4 | `breakpoint_wait` | Wait for exception + get autopsy in one call |
| 5 | `exception_get_context` | Get full context (more frames, variables, inner exceptions) |
| 6 | `evaluate` | Test specific hypotheses |
| 7 | `debug_disconnect` | Clean up |

## Exception Autopsy vs. Manual Inspection

| Approach | Tool Calls | Best For |
|----------|-----------|----------|
| **Autopsy** (`breakpoint_wait` + `include_autopsy`) | 1 call | Quick diagnosis — get everything at once |
| **Deep dive** (`exception_get_context`) | 1 call | More detail — control frame count, variable depth, inner chain |
| **Manual** (`stacktrace_get` + `variables_get` + `evaluate`) | 3+ calls | Fine-grained exploration after initial autopsy |

## Tips

- **Use `include_autopsy: true` on `breakpoint_wait`** to get exception context automatically when a breakpoint fires. This saves a round-trip compared to calling `exception_get_context` separately.
- **Inner exceptions reveal root causes.** An `InvalidOperationException` might wrap an `IOException` which wraps a `SocketException`. Set `max_inner_exceptions` high enough to see the full chain.
- **Variables are collected per-frame.** Set `include_variables_for_frames: 3` to see locals in the throwing frame plus its two callers — often enough to understand the data flow.
- **First-chance vs. unhandled:** `break_on_first_chance: true` catches exceptions before any catch block. This is almost always what you want — if you wait for unhandled, the original call stack may be unwound.
- **Combine with tracepoints.** Set tracepoints on key methods with log messages, then set an exception breakpoint. When the exception fires, the tracepoint log shows what happened leading up to it.
