# Data Model: MCP Resources for Debugger State

**Feature**: 019-mcp-resources | **Date**: 2026-02-05

## Resource Entities

### SessionResource (JSON response for `debugger://session`)

| Field | Type | Description |
|-------|------|-------------|
| processId | int | OS process ID |
| processName | string | Executable name |
| executablePath | string | Full path to executable |
| runtimeVersion | string | .NET runtime version |
| state | string | "Running" / "Paused" / "Disconnected" |
| launchMode | string | "Attach" / "Launch" |
| attachedAt | string (ISO 8601) | Session start timestamp (DateTimeOffset) |
| pauseReason | string? | "Breakpoint" / "Step" / "Exception" / "Pause" / "Entry" (null if running) |
| currentLocation | SourceLocationDto? | Current source location (null if running) |
| activeThreadId | int? | Thread that triggered pause (null if running) |
| commandLineArgs | string[]? | Launch arguments (null if attached) |
| workingDirectory | string? | Working directory (null if attached) |

### SourceLocationDto (nested in session/threads)

| Field | Type | Description |
|-------|------|-------------|
| file | string | Absolute path to source file |
| line | int | 1-based line number |
| column | int? | 1-based column (optional) |
| functionName | string? | Containing function name |
| moduleName | string? | Containing assembly name |

### BreakpointsResource (JSON response for `debugger://breakpoints`)

| Field | Type | Description |
|-------|------|-------------|
| breakpoints | BreakpointDto[] | All regular breakpoints and tracepoints |
| exceptionBreakpoints | ExceptionBreakpointDto[] | All exception breakpoints |

### BreakpointDto (element of breakpoints array)

| Field | Type | Description |
|-------|------|-------------|
| id | string | Breakpoint ID (bp-{guid} or tp-{guid}) |
| type | string | "Breakpoint" / "Tracepoint" |
| file | string | Source file path |
| line | int | 1-based line number |
| column | int? | Column (optional) |
| enabled | bool | User-controlled enable state |
| verified | bool | Bound to executable code |
| state | string | "Pending" / "Bound" / "Disabled" |
| hitCount | int | Number of times hit |
| condition | string? | Condition expression (null if unconditional) |
| logMessage | string? | Tracepoint log template (null for breakpoints) |
| hitCountMultiple | int | Notify every Nth hit (0 = every) |
| maxNotifications | int | Auto-disable limit (0 = unlimited) |
| notificationsSent | int | Count of notifications sent |

### ExceptionBreakpointDto (element of exceptionBreakpoints array)

| Field | Type | Description |
|-------|------|-------------|
| id | string | Exception breakpoint ID (ex-{guid}) |
| exceptionType | string | Full type name |
| breakOnFirstChance | bool | Break on throw |
| breakOnSecondChance | bool | Break on unhandled |
| includeSubtypes | bool | Match derived types |
| enabled | bool | User-controlled enable |
| verified | bool | Type found in loaded assemblies |
| hitCount | int | Number of times triggered |

### ThreadsResource (JSON response for `debugger://threads`)

| Field | Type | Description |
|-------|------|-------------|
| threads | ThreadDto[] | All managed threads |
| stale | bool | True if process is running (data from last pause) |
| capturedAt | string (ISO 8601) | Timestamp when snapshot was taken (DateTimeOffset) |

### ThreadDto (element of threads array)

| Field | Type | Description |
|-------|------|-------------|
| id | int | OS thread ID |
| name | string? | Thread name (null if unnamed) |
| state | string | Thread state (Running/Suspended/etc.) |
| isCurrent | bool | Active/pause thread flag |
| location | SourceLocationDto? | Current location (null if unavailable) |

### SourceResource (text/plain response for `debugger://source/{file}`)

Plain text file content. No JSON wrapper. Returned as `text/plain` with the raw source code.

## Service Entities (Internal)

### ResourceNotifier

Manages debounced notifications and subscription tracking.

| Field | Type | Description |
|-------|------|-------------|
| _subscriptions | ConcurrentDictionary<string, bool> | URI → subscribed |
| _debounceTimers | ConcurrentDictionary<string, Timer> | URI → debounce timer |
| _mcpServer | IMcpServer | For sending notifications |
| _debounceMs | int | Debounce window (300ms default) |

### AllowedSourcePaths

Tracks PDB-referenced source file paths for security boundary.

| Field | Type | Description |
|-------|------|-------------|
| _paths | ConcurrentDictionary<string, string> | file path → module path |
| _moduleToFiles | ConcurrentDictionary<string, HashSet<string>> | module → file paths (for cleanup on unload) |

### ThreadSnapshotCache

Caches last-known thread list for stale serving.

| Field | Type | Description |
|-------|------|-------------|
| _threads | IReadOnlyList<ThreadInfo> | Last known thread list |
| _capturedAt | DateTimeOffset | When snapshot was taken |

## State Transitions

### Resource List Lifecycle

```
No Session → Session Active → Session Disconnected
    │              │                    │
    │              │                    └─ resources/list returns []
    │              │                       notify: list_changed
    │              │
    │              └─ resources/list returns [session, breakpoints, threads, source/{file}]
    │                 notify: list_changed
    │
    └─ resources/list returns []
```

### Thread Snapshot State

```
Paused → Running → Paused
  │         │         │
  │         │         └─ Update cache, stale=false
  │         │
  │         └─ Serve cached data, stale=true
  │
  └─ Fresh data from GetThreads(), stale=false, update cache
```
