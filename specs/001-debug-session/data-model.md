# Data Model: Debug Session Management

**Feature**: 001-debug-session
**Date**: 2026-01-17

## Overview

This document defines the domain entities for debug session management in DotnetMcp.

---

## Entities

### DebugSession

Represents an active debugging connection to a .NET process.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `processId` | int | Yes | OS process ID of the debuggee |
| `processName` | string | Yes | Name of the executable (e.g., "MyApp") |
| `executablePath` | string | Yes | Full path to the executable |
| `runtimeVersion` | string | Yes | .NET runtime version (e.g., ".NET 8.0.1") |
| `attachedAt` | DateTime | Yes | UTC timestamp when session started |
| `state` | SessionState | Yes | Current execution state |
| `launchMode` | LaunchMode | Yes | How the session was started |
| `commandLineArgs` | string[] | No | Arguments passed (launch mode only) |
| `workingDirectory` | string | No | Working directory (launch mode only) |

**Validation Rules**:
- `processId` must be > 0
- `processName` must not be empty
- `runtimeVersion` must be a valid .NET version string
- `attachedAt` must be in the past

**State Transitions**:
```
[No Session] --attach/launch--> Running --pause/breakpoint--> Paused
                                   ^                            |
                                   +-------continue/step--------+
                                   |
Running/Paused --disconnect/exit--> [No Session]
```

---

### SessionState

Enumeration of possible debug session states.

| Value | Description | Valid Operations |
|-------|-------------|------------------|
| `Disconnected` | No active session | attach, launch |
| `Running` | Process executing | pause, disconnect |
| `Paused` | Process stopped | continue, step, inspect, disconnect |

**When Paused**, additional context is available:

| Field | Type | Description |
|-------|------|-------------|
| `pauseReason` | PauseReason | Why execution stopped |
| `currentLocation` | SourceLocation | Where execution stopped |
| `activeThreadId` | int | Thread that caused the pause |

---

### PauseReason

Enumeration of reasons why execution paused.

| Value | Description |
|-------|-------------|
| `Breakpoint` | Hit a breakpoint |
| `Step` | Completed a step operation |
| `Exception` | Exception thrown |
| `Pause` | User requested pause |
| `Entry` | Stopped at entry point (launch with stopAtEntry) |

---

### LaunchMode

How the debug session was initiated.

| Value | Description |
|-------|-------------|
| `Attach` | Connected to existing process |
| `Launch` | Started process under debugger |

---

### SourceLocation

Represents a position in source code.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | string | Yes | Absolute path to source file |
| `line` | int | Yes | 1-based line number |
| `column` | int | No | 1-based column number |
| `functionName` | string | No | Name of containing function |
| `moduleName` | string | No | Name of containing module/assembly |

---

### ProcessInfo

Information about a debuggable .NET process.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `pid` | int | Yes | Process ID |
| `name` | string | Yes | Process name |
| `executablePath` | string | Yes | Path to executable |
| `commandLine` | string | No | Full command line |
| `runtimeVersion` | string | No | Detected .NET version |
| `isManaged` | bool | Yes | True if .NET process detected |

---

## Response Schemas

### AttachResponse

Returned by `debug_attach` on success.

```json
{
  "success": true,
  "session": {
    "processId": 12345,
    "processName": "MyApp",
    "runtimeVersion": ".NET 8.0.1",
    "state": "running",
    "launchMode": "attach",
    "attachedAt": "2026-01-17T10:30:00Z"
  }
}
```

### LaunchResponse

Returned by `debug_launch` on success.

```json
{
  "success": true,
  "session": {
    "processId": 12346,
    "processName": "MyApp",
    "executablePath": "/app/MyApp.dll",
    "runtimeVersion": ".NET 8.0.1",
    "state": "paused",
    "launchMode": "launch",
    "attachedAt": "2026-01-17T10:30:00Z",
    "commandLineArgs": ["--verbose"],
    "workingDirectory": "/app"
  },
  "pauseReason": "entry"
}
```

### StateResponse

Returned by `debug_state`.

**When disconnected**:
```json
{
  "state": "disconnected"
}
```

**When running**:
```json
{
  "state": "running",
  "processId": 12345,
  "processName": "MyApp",
  "attachedAt": "2026-01-17T10:30:00Z"
}
```

**When paused**:
```json
{
  "state": "paused",
  "processId": 12345,
  "processName": "MyApp",
  "attachedAt": "2026-01-17T10:30:00Z",
  "pauseReason": "breakpoint",
  "activeThreadId": 1,
  "currentLocation": {
    "file": "/app/Services/UserService.cs",
    "line": 42,
    "column": 5,
    "functionName": "GetUser",
    "moduleName": "MyApp"
  }
}
```

### DisconnectResponse

Returned by `debug_disconnect`.

```json
{
  "success": true,
  "message": "Disconnected from process 12345",
  "processTerminated": false
}
```

---

## Error Response Schema

All tools return this structure when `isError: true`:

```json
{
  "error": {
    "code": "PROCESS_NOT_FOUND",
    "message": "No process found with PID 99999",
    "details": {
      "pid": 99999
    }
  }
}
```

### Error Codes

| Code | Description |
|------|-------------|
| `PROCESS_NOT_FOUND` | PID does not exist |
| `NOT_DOTNET_PROCESS` | Process is not a .NET application |
| `PERMISSION_DENIED` | Insufficient privileges to debug |
| `SESSION_ACTIVE` | A debug session is already active |
| `NO_SESSION` | No active session to operate on |
| `ATTACH_FAILED` | ICorDebug attach failed |
| `LAUNCH_FAILED` | Process launch failed |
| `INVALID_PATH` | Executable path invalid or not found |
| `TIMEOUT` | Operation timed out |
