---
title: Execution Control
sidebar_position: 3
---

# Execution Control

Execution tools control program flow — resuming, pausing, and stepping through code line by line.

## When to Use

Use execution tools after the process is stopped (at a breakpoint, after a step, or after a pause). Stepping tools let you move through code one line at a time, step into method calls, or step out of the current method.

**Typical flow:** *(process stopped)* → `debug_step` (over/into/out) → *(inspect)* → `debug_continue`

## Tools

### debug_continue

Resume execution.

**Requires:** Paused session

**When to use:** After inspecting state at a breakpoint or step, resume normal execution until the next breakpoint is hit or the process exits.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `thread_id` | integer | No | Resume specific thread only |

**Response:**
```json
{
  "success": true,
  "state": "running"
}
```

**Real-world use case:** After inspecting variables at a breakpoint, an AI agent calls `debug_continue` and then `breakpoint_wait` to advance to the next breakpoint in the workflow.

---

### debug_pause

Pause execution.

**Requires:** Active session (running)

**When to use:** The process is running and you need to stop it immediately — for example, to inspect what's happening in a loop or a long-running operation.

**Parameters:** None

**Response:**
```json
{
  "success": true,
  "state": "stopped",
  "threads": [
    { "id": 1, "location": "System.Threading.Thread.Sleep" },
    { "id": 5, "location": "MyApp.Services.UserService.GetUser" }
  ]
}
```

---

### debug_step

Step through code. Supports three modes: step over (next line), step into (enter method call), and step out (finish current method).

**Requires:** Paused session

**When to use:**
- **Step over:** Execute the current line and move to the next one. Method calls on the current line execute fully without stopping inside them.
- **Step into:** If the current line contains a method call, enter that method and stop at its first line.
- **Step out:** Run until the current method returns, then stop in the caller.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | string | Yes | `over`, `into`, or `out` |
| `thread_id` | integer | No | Thread to step (default: current) |

**Example (step over):**
```json
{
  "action": "over"
}
```

**Response:**
```json
{
  "success": true,
  "location": {
    "file": "/app/Services/UserService.cs",
    "line": 43,
    "function": "GetUser"
  }
}
```

**Example (step into):**
```json
{
  "action": "into"
}
```

**Response:**
```json
{
  "success": true,
  "location": {
    "file": "/app/Data/UserRepository.cs",
    "line": 15,
    "function": "FindById"
  }
}
```

**Example (step out):**
```json
{
  "action": "out"
}
```

**Response:**
```json
{
  "success": true,
  "location": {
    "file": "/app/Controllers/UserController.cs",
    "line": 28,
    "function": "Get"
  }
}
```

**Real-world use case:** An AI agent is stopped at a method that calls `ProcessOrder()`. It uses `step into` to enter the method, then steps over a few lines while inspecting variables, finds the bug, and uses `step out` to return to the caller.
