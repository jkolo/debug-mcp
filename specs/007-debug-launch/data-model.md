# Data Model: Debug Launch

**Feature**: 007-debug-launch
**Date**: 2026-01-27

## Entities

### Existing Entities (No Changes Required)

The existing data model already supports launch functionality:

#### ProcessInfo
*Location: `DotnetMcp/Models/ProcessInfo.cs`*

```csharp
public record ProcessInfo(
    int Pid,
    string Name,
    string ExecutablePath,
    bool IsManaged,
    string? CommandLine = null,
    string? RuntimeVersion = null);
```

**Usage**: Returned from `ProcessDebugger.LaunchAsync()` after successful launch.

---

#### DebugSession
*Location: `DotnetMcp/Models/DebugSession.cs`*

```csharp
public sealed class DebugSession
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string ExecutablePath { get; init; }
    public required string RuntimeVersion { get; init; }
    public required DateTime AttachedAt { get; init; }
    public SessionState State { get; set; }
    public required LaunchMode LaunchMode { get; init; }
    public string[]? CommandLineArgs { get; init; }    // Launch mode only
    public string? WorkingDirectory { get; init; }      // Launch mode only
    public PauseReason? PauseReason { get; set; }
    public SourceLocation? CurrentLocation { get; set; }
    public int? ActiveThreadId { get; set; }
}
```

**Usage**: Created by `DebugSessionManager.LaunchAsync()` with `LaunchMode.Launch`.

---

#### LaunchMode
*Location: `DotnetMcp/Models/LaunchMode.cs`*

```csharp
public enum LaunchMode
{
    Attach,  // Connected to existing process
    Launch   // Started process under debugger
}
```

---

#### SessionState
*Location: `DotnetMcp/Models/SessionState.cs`*

```csharp
public enum SessionState
{
    Running,
    Paused,
    Terminated
}
```

---

#### PauseReason
*Location: `DotnetMcp/Models/PauseReason.cs`*

```csharp
public enum PauseReason
{
    Breakpoint,
    Step,
    Exception,
    Pause,
    Entry    // Stopped at entry point (stopAtEntry=true)
}
```

**Note**: `Entry` is already defined for stopAtEntry functionality.

---

### Internal State (New)

#### LaunchState
*Location: `DotnetMcp/Services/ProcessDebugger.cs` (private fields)*

The following state must be tracked internally during launch:

| Field | Type | Purpose |
|-------|------|---------|
| `_resumeHandle` | `IntPtr` | Handle from `CreateProcessForLaunch` for `ResumeProcess` |
| `_unregisterToken` | `IntPtr` | Token from `RegisterForRuntimeStartup` for cleanup |
| `_startupCallbackDelegate` | `PSTARTUP_CALLBACK` | Delegate kept alive to prevent GC |
| `_launchCompletionSource` | `TaskCompletionSource<ProcessInfo>` | Async completion for launch operation |

**Lifecycle:**
1. Set during `LaunchAsync()`
2. Cleared during `DetachAsync()` using `UnregisterForRuntimeStartup()`

---

## Entity Relationships

```text
┌─────────────────────────────────────────────────────────────────┐
│                    MCP Client Request                            │
│               debug_launch(program, args, ...)                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    DebugLaunchTool                               │
│              (MCP parameter validation)                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  DebugSessionManager                             │
│            (Session state management)                            │
│      Creates DebugSession with LaunchMode.Launch                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    ProcessDebugger                               │
│          (ICorDebug + DbgShim integration)                       │
│      Returns ProcessInfo from native launch                      │
└─────────────────────────────────────────────────────────────────┘
```

---

## Validation Rules

### Program Path
- MUST exist on filesystem
- MUST be readable by current user
- MUST be .NET assembly (DLL or EXE)
- For DLLs: dotnet runtime must be available

### Working Directory
- If provided, MUST exist
- If provided, MUST be a directory (not file)
- If not provided, defaults to program's directory

### Environment Variables
- Keys: non-empty strings
- Values: any string (including empty)
- Must be serializable as JSON object

### Timeout
- Range: 1000-300000 milliseconds
- Default: 30000 (30 seconds)

---

## State Transitions

```text
                    ┌──────────────┐
                    │   Initial    │
                    │   (no sess)  │
                    └──────┬───────┘
                           │ debug_launch()
                           ▼
                    ┌──────────────┐
                    │   Launching  │
                    │  (internal)  │
                    └──────┬───────┘
                           │
           ┌───────────────┼───────────────┐
           │ stopAtEntry   │ !stopAtEntry  │ error
           │ = true        │ = false       │
           ▼               ▼               ▼
    ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
    │    Paused    │ │   Running    │ │    (none)    │
    │ Entry reason │ │              │ │   + error    │
    └──────────────┘ └──────────────┘ └──────────────┘
```
