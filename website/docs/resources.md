---
title: MCP Resources
sidebar_position: 8
---

# MCP Resources

MCP resources expose debugger state as **read-only, subscribable data views**. Unlike tools (which are request-response), resources push updates to subscribed clients whenever the underlying state changes.

## Resources vs. Tools

| | Tools | Resources |
|-|-------|-----------|
| **Pattern** | Request → Response | Subscribe → Push updates |
| **Use case** | Perform actions, query on demand | Monitor state changes |
| **Example** | `debug_state` returns current state | `debugger://session` pushes on every state change |
| **Best for** | One-off queries, actions | Dashboards, watchers, event-driven agents |

## Available Resources

Resources are only available when a debug session is active. When no session exists, the resource list is empty.

---

### debugger://session

Current debug session state, process info, and pause reason.

**MIME Type:** `application/json`

**Example response:**
```json
{
  "processId": 12345,
  "processName": "MyApp",
  "executablePath": "/app/MyApp.dll",
  "runtimeVersion": "10.0.0",
  "state": "Paused",
  "launchMode": "Launch",
  "attachedAt": "2026-02-06T10:30:00Z",
  "pauseReason": "Breakpoint",
  "currentLocation": {
    "file": "/app/Services/UserService.cs",
    "line": 42,
    "column": 8,
    "functionName": "GetUser",
    "moduleName": "MyApp.dll"
  },
  "activeThreadId": 5,
  "commandLineArgs": ["--environment", "Development"],
  "workingDirectory": "/app"
}
```

**Updates when:** Session state changes (pause, resume, step, disconnect), current location changes.

---

### debugger://breakpoints

All active breakpoints, tracepoints, and exception breakpoints with their status and hit counts.

**MIME Type:** `application/json`

**Example response:**
```json
{
  "breakpoints": [
    {
      "id": "bp-550e8400-e29b-41d4-a716-446655440000",
      "type": "Breakpoint",
      "file": "/app/Services/UserService.cs",
      "line": 42,
      "enabled": true,
      "verified": true,
      "state": "Bound",
      "hitCount": 3,
      "condition": "userId != null",
      "logMessage": null
    },
    {
      "id": "tp-660f9511-f3ac-52e5-b827-557766551111",
      "type": "Tracepoint",
      "file": "/app/Services/OrderService.cs",
      "line": 55,
      "enabled": true,
      "verified": true,
      "state": "Bound",
      "hitCount": 142,
      "logMessage": "Processing order {orderId}",
      "hitCountMultiple": 100,
      "maxNotifications": 0,
      "notificationsSent": 1
    }
  ],
  "exceptionBreakpoints": [
    {
      "id": "ebp-770a0622-a4bd-63f6-c938-668877662222",
      "exceptionType": "System.NullReferenceException",
      "breakOnFirstChance": true,
      "breakOnSecondChance": true,
      "includeSubtypes": true,
      "enabled": true,
      "verified": true,
      "hitCount": 0
    }
  ]
}
```

**Updates when:** Breakpoints added, removed, enabled/disabled, or hit count changes.

---

### debugger://threads

Thread list with states and current locations.

**MIME Type:** `application/json`

**Example response:**
```json
{
  "threads": [
    {
      "id": 1,
      "name": "Main",
      "state": "Suspended",
      "isCurrent": true,
      "location": {
        "file": "/app/Services/UserService.cs",
        "line": 42,
        "column": 8,
        "functionName": "GetUser",
        "moduleName": "MyApp.dll"
      }
    },
    {
      "id": 5,
      "name": "Worker Thread #1",
      "state": "Suspended",
      "isCurrent": false,
      "location": {
        "file": "/app/Services/BackgroundJob.cs",
        "line": 18,
        "column": 4,
        "functionName": "Execute",
        "moduleName": "MyApp.dll"
      }
    }
  ],
  "stale": false,
  "capturedAt": "2026-02-06T10:30:00Z"
}
```

**Stale data:** When the process is running, thread data may be stale (captured at the last pause). The `stale` field indicates whether the data is current, and `capturedAt` shows when it was last captured.

**Updates when:** Process pauses, resumes, or thread states change.

---

### debugger://source/\{+file\}

Source file contents for a given path.

**MIME Type:** `text/plain`

**URI example:** `debugger://source//app/Services/UserService.cs`

**Response:** Plain text file contents (no JSON wrapper).

**Security:** Only source files that belong to loaded modules are accessible. If a file isn't part of the debugged application, the request will fail.

**Updates when:** This resource is static — it doesn't change during a session.

---

## Subscribing to Resources

MCP clients can subscribe to resources to receive push notifications when data changes:

1. **Subscribe:** Send `resources/subscribe` with the resource URI
2. **Receive updates:** The server sends `notifications/resources/updated` when the resource changes
3. **Read new data:** Fetch the resource again to get the updated content
4. **Unsubscribe:** Send `resources/unsubscribe` when no longer interested

Updates are coalesced — if the same resource changes multiple times in quick succession, you receive a single notification rather than one per change.

## Lifecycle

```
No Session          → resources/list returns []
Session Starts      → resources/list returns all 4 resources
                    → notifications/resources/list_changed sent
Session Active      → resources available, updates sent on changes
Session Disconnects → resources/list returns []
                    → notifications/resources/list_changed sent
```
