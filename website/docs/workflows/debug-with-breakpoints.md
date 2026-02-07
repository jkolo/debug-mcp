---
title: Debug with Breakpoints
sidebar_position: 1
---

# Workflow: Debug with Breakpoints

This guide walks through the most common debugging workflow: setting a breakpoint, waiting for it to hit, inspecting variables, stepping through code, and evaluating expressions.

## Scenario

You have a .NET application with a bug in a specific method. You know roughly where the problem is — you want to stop execution there, look at the state, and step through the code to find the issue.

## Steps

### 1. Launch the application

> "Launch `/app/MyApp.dll` and stop at the entry point"

The process starts and pauses at `Main()`, giving you a chance to set breakpoints before any code runs.

<details>
<summary>Tool call details</summary>

**Request** (`debug_launch`):
```json
{
  "program": "/app/MyApp.dll",
  "stop_at_entry": true
}
```

</details>

### 2. Set a breakpoint

> "Set a breakpoint at line 42 in UserService.cs"

The breakpoint is bound to the source location. When execution reaches this line, the process will pause.

<details>
<summary>Tool call details</summary>

**Request** (`breakpoint_set`):
```json
{
  "file": "Services/UserService.cs",
  "line": 42
}
```

</details>

### 3. Continue and wait

> "Continue execution and wait for the breakpoint"

The application runs until line 42 is hit.

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
  "timeout_ms": 30000
}
```

</details>

### 4. Inspect variables

> "Show me the local variables"

See what values the variables have at the breakpoint.

<details>
<summary>Tool call details</summary>

**Request** (`variables_get`):
```json
{
  "scope": "all"
}
```

**Response:**
```json
{
  "variables": [
    { "name": "userId", "type": "string", "value": "\"abc123\"" },
    { "name": "user", "type": "User", "value": "null" }
  ]
}
```

</details>

### 5. Step through code

> "Step over to the next line"

Move through the code line by line to observe how state changes.

<details>
<summary>Tool call details</summary>

**Request** (`debug_step`):
```json
{
  "action": "over"
}
```

Use `"action": "into"` to enter a method call, or `"action": "out"` to finish the current method and return to the caller.

</details>

### 6. Evaluate expressions

> "What is the value of `items.Where(i => i.IsActive).Count()`?"

Test hypotheses about the bug by evaluating arbitrary C# expressions.

<details>
<summary>Tool call details</summary>

**Request** (`evaluate`):
```json
{
  "expression": "items.Where(i => i.IsActive).Count()"
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

## Summary

| Step | Tool | Purpose |
|------|------|---------|
| 1 | `debug_launch` | Start the app under debugger |
| 2 | `breakpoint_set` | Set a breakpoint at the suspected location |
| 3 | `debug_continue` + `breakpoint_wait` | Run until the breakpoint hits |
| 4 | `variables_get` | Inspect local variables |
| 5 | `debug_step` | Step through code line by line |
| 6 | `evaluate` | Test hypotheses with expressions |
| 7 | `debug_disconnect` | Clean up |

## Tips

- **Use conditional breakpoints** to avoid stopping on every call: `"condition": "userId == null"`.
- **Use `breakpoint_set` with `function`** when you know the method name but not the line number.
- **Inspect `this`** to see the state of the current object: `"scope": "this"`.
- **Expand children** with `variables_get` + `"expand": "this._repository"` to drill into nested objects.
- **Use `object_inspect`** for detailed information including field offsets, sizes, and memory addresses.
