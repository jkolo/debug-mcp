---
title: Session Management
sidebar_position: 1
---

# Session Management

Session tools control the debugging session lifecycle — launching processes, attaching to running applications, querying state, and disconnecting.

## When to Use

Use session tools to start and end debugging sessions. Every debugging workflow begins with either `debug_launch` (to start a new process) or `debug_attach` (to connect to a running process), and ends with `debug_disconnect`.

**Typical flow:** `debug_launch` or `debug_attach` → *(set breakpoints, inspect, etc.)* → `debug_disconnect`

## Tools

### debug_launch

Launch a .NET application under the debugger.

**Requires:** No active session

**When to use:** Starting a fresh debugging session with a .NET application. Use `stop_at_entry` to pause immediately at the entry point — useful when you want to set breakpoints before any code runs.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `program` | string | Yes | Path to .NET DLL or project file |
| `args` | string[] | No | Command line arguments |
| `cwd` | string | No | Working directory |
| `env` | object | No | Environment variables |
| `stop_at_entry` | boolean | No | Break on entry point (default: false) |

**Example request:**
```json
{
  "program": "/app/MyService.dll",
  "args": ["--environment", "Development"],
  "cwd": "/app",
  "env": {
    "ASPNETCORE_URLS": "http://localhost:5000"
  },
  "stop_at_entry": true
}
```

**Example response:**
```json
{
  "success": true,
  "pid": 12345,
  "state": "stopped",
  "message": "Process launched and stopped at entry point"
}
```

**Real-world use case:** An AI agent needs to debug a web API that throws an exception on a specific endpoint. It launches the service with `stop_at_entry: true`, sets an exception breakpoint for `NullReferenceException`, then continues execution and sends a request to trigger the bug.

---

### debug_attach

Attach to a running .NET process.

**Requires:** No active session

**When to use:** Debugging an application that's already running — a long-lived service, a web server, or a process you can't easily restart.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `pid` | integer | Yes | Process ID to attach to |

**Example request:**
```json
{
  "pid": 12345
}
```

**Example response:**
```json
{
  "success": true,
  "pid": 12345,
  "state": "running",
  "process_name": "MyService",
  "runtime_version": ".NET 8.0.1"
}
```

**Errors:**
- `process_not_found` — No process with given PID
- `not_managed` — Process is not running .NET
- `already_attached` — A debugger is already attached

**Real-world use case:** A production service is consuming excessive memory. An AI agent attaches to the running process, pauses it, inspects the heap via `object_inspect`, then detaches without terminating the service.

---

### debug_disconnect

End the debugging session.

**Requires:** Active session (running or paused)

**When to use:** Finishing a debugging session. By default, the process continues running after detaching. Set `terminate: true` to kill it.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `terminate` | boolean | No | Kill the process (default: false) |

**Example request:**
```json
{
  "terminate": false
}
```

**Example response:**
```json
{
  "success": true,
  "message": "Detached from process 12345"
}
```

---

### debug_state

Get the current debugging state.

**Requires:** No session needed (works anytime)

**When to use:** Check whether the process is running, stopped at a breakpoint, or has exited. This is the first tool to call when you need to understand what's happening before performing other operations.

**Parameters:** None

**Example response:**
```json
{
  "state": "stopped",
  "reason": "breakpoint",
  "thread_id": 5,
  "breakpoint_id": 1,
  "location": {
    "file": "/app/Services/UserService.cs",
    "line": 42,
    "column": 12,
    "function": "GetUser"
  }
}
```

**State values:**

| State | Description |
|-------|-------------|
| `not_attached` | No debugging session active |
| `running` | Process is executing |
| `stopped` | Process is paused |
| `exited` | Process has terminated |

**Reason values (when stopped):**

| Reason | Description |
|--------|-------------|
| `breakpoint` | Hit a breakpoint |
| `step` | Step operation completed |
| `pause` | User requested pause |
| `exception` | Exception thrown |
| `entry_point` | Stopped at program entry |
