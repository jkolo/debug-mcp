# Research: Debug Launch

**Feature**: 007-debug-launch
**Date**: 2026-01-27

## Executive Summary

This research covers the DbgShim APIs required to launch .NET processes under debugger control. The implementation uses `CreateProcessForLaunch`, `RegisterForRuntimeStartup`, and `ResumeProcess` from the dbgshim native library.

---

## 1. DbgShim Launch APIs

### Decision: Use DbgShim CreateProcessForLaunch + RegisterForRuntimeStartup Pattern

**Rationale**: This is the official Microsoft-supported pattern for launching processes under debugger control. It ensures the debugger is attached before any managed code executes.

**Alternatives considered**:
- `ICorDebug.CreateProcess`: Lower-level, requires more manual work, less portable
- External process start + attach: Race condition, cannot guarantee pre-execution attachment
- DAP-based launch: Violates Native First principle

### Key APIs

| API | Purpose | Notes |
|-----|---------|-------|
| `CreateProcessForLaunch` | Start process suspended | Returns PID and resume handle |
| `RegisterForRuntimeStartup` | Get callback when CLR loads | Provides ICorDebug instance |
| `ResumeProcess` | Allow suspended process to run | Called after debugger setup |
| `UnregisterForRuntimeStartup` | Cleanup registration | Called during disconnect |

---

## 2. CreateProcessForLaunch API

**Signature:**
```c
HRESULT CreateProcessForLaunch (
    [in]  LPWSTR lpCommandLine,       // Command line to execute
    [in]  BOOL bSuspendProcess,       // TRUE to start suspended
    [in]  LPVOID lpEnvironment,       // Environment block
    [in]  LPCWSTR lpCurrentDirectory, // Working directory
    [out] PDWORD pProcessId,          // Receives process ID
    [out] HANDLE *pResumeHandle       // Handle for ResumeProcess
);
```

**Key Characteristics:**
- Cross-platform (Windows, Linux, macOS)
- When `bSuspendProcess=TRUE`, process starts but doesn't execute user code
- `pResumeHandle` is used with `ResumeProcess()` after debugger initialization
- For .NET DLLs: command line is `"dotnet /path/to/app.dll [args]"`

---

## 3. RegisterForRuntimeStartup API

**Signature:**
```c
HRESULT RegisterForRuntimeStartup (
    [in]  DWORD dwProcessId,            // Process ID from CreateProcessForLaunch
    [in]  PSTARTUP_CALLBACK pfnCallback, // Callback when runtime loads
    [in]  PVOID parameter,              // User data for callback
    [out] PVOID *ppUnregisterToken      // Token for unregistration
);
```

**Callback Signature:**
```c
typedef VOID (*PSTARTUP_CALLBACK)(
    IUnknown *pCordb,   // ICorDebug instance (cast from IUnknown)
    PVOID parameter,    // User data
    HRESULT hr          // S_OK or error code
);
```

**Critical Behavior:**
- Non-blocking: returns immediately
- Callback invoked when CLR module loads during early initialization
- Runtime is **blocked** until callback returns
- Only first coreclr module instance supported per process

**Callback HRESULT values:**
- `S_OK`: pCordb is valid ICorDebug*
- `CORDBG_E_DEBUG_COMPONENT_MISSING`: Missing mscordbi.dll
- `CORDBG_E_INCOMPATIBLE_PROTOCOL`: Version mismatch
- `E_FAIL`: Unable to provide ICorDebug

---

## 4. Complete Launch Workflow

```text
┌────────────────────────────────────────────────────────────────┐
│ 1. CreateProcessForLaunch("dotnet app.dll", suspended=TRUE)    │
│    → Process created but suspended, PID and resumeHandle       │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│ 2. RegisterForRuntimeStartup(pid, callback, userData, &token)  │
│    → Returns immediately, callback registered                  │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│ 3. ResumeProcess(resumeHandle)                                 │
│    → Process begins executing, loads CLR                       │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│ 4. Callback invoked (CLR has loaded)                           │
│    - Cast pCordb to ICorDebug*                                 │
│    - Initialize: corDebug.Initialize()                         │
│    - Set handler: corDebug.SetManagedHandler(callback)         │
│    - (Optional) Set entry breakpoint for stopAtEntry           │
│    - Callback returns (runtime resumes)                        │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│ 5. Debug events flow through managed callback                  │
│    - Breakpoint hit, Entry breakpoint for stopAtEntry          │
│    - Process paused or running based on settings               │
└────────────────────────────────────────────────────────────────┘
```

---

## 5. stopAtEntry Implementation

### Decision: Use Module Load Event + Pause Pattern

**Rationale**: Simpler and more reliable than setting a breakpoint at Main(). The debugger pauses immediately when CLR initializes.

**Implementation:**
1. In the startup callback, call `process.Stop(0)` immediately
2. Set `SessionState.Paused` and `PauseReason.Entry`
3. Do NOT call `Continue()` automatically
4. User must explicitly call `debug_continue` to proceed

**Alternative (Entry Point Breakpoint):**
- More traditional approach
- Set IL breakpoint at offset 0 of entry method
- Requires resolving Main() method token
- More complex but matches IDE behavior

---

## 6. ClrDebug Integration

The project uses ClrDebug 0.3.4 which wraps the native DbgShim APIs:

```csharp
// Existing pattern in ProcessDebugger
var dbgShimHandle = Load(dbgshimPath);
_dbgShim = new DbgShim(dbgShimHandle);

// For launch (new)
uint processId;
IntPtr resumeHandle;
_dbgShim.CreateProcessForLaunch(
    commandLine,
    suspendProcess: true,
    environment,
    currentDirectory,
    out processId,
    out resumeHandle);

IntPtr unregisterToken;
_dbgShim.RegisterForRuntimeStartup(
    processId,
    startupCallback,
    userData,
    out unregisterToken);

_dbgShim.ResumeProcess(resumeHandle);
```

**Critical Implementation Notes:**
1. Keep callback delegate alive (prevent GC)
2. Handle callback on different thread
3. Store `unregisterToken` for cleanup
4. Store `resumeHandle` until resume is called

---

## 7. Command Line Construction

For launching .NET DLLs, the command line must include the dotnet runtime:

```text
"dotnet" "/full/path/to/app.dll" "arg1" "arg2"
```

**Edge cases:**
- Paths with spaces: wrap in quotes
- Arguments with special characters: proper escaping
- Self-contained apps: use the executable directly (no dotnet prefix)

---

## 8. Environment Variables

DbgShim accepts environment as a null-terminated Unicode string block:

```csharp
// Format: "VAR1=value1\0VAR2=value2\0\0"
string BuildEnvironmentBlock(Dictionary<string, string> env)
{
    var sb = new StringBuilder();
    foreach (var (key, value) in env)
    {
        sb.Append($"{key}={value}\0");
    }
    sb.Append('\0'); // Double null terminator
    return sb.ToString();
}
```

---

## 9. Error Handling

| Error | Cause | Recovery |
|-------|-------|----------|
| File not found | Invalid program path | Validate before launch |
| Invalid assembly | Not a .NET application | Check file header |
| Runtime not found | .NET not installed | Clear error message |
| Permission denied | Insufficient privileges | Suggest fix |
| Timeout | Process didn't start CLR | Increase timeout |

---

## 10. Implementation Checklist

- [ ] Add `CreateProcessForLaunch` call to ProcessDebugger
- [ ] Implement startup callback registration
- [ ] Handle callback on separate thread
- [ ] Build command line from program + args
- [ ] Build environment block from dictionary
- [ ] Implement stopAtEntry via immediate pause
- [ ] Store launch metadata (resumeHandle, unregisterToken)
- [ ] Handle cleanup on disconnect
- [ ] Add comprehensive error handling

---

## Sources

- [CreateProcessForLaunch Function - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/unmanaged-api/debugging/createprocessforlaunch-function)
- [RegisterForRuntimeStartup Function - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/unmanaged-api/debugging/registerforruntimestartup-function)
- [ResumeProcess Function - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/unmanaged-api/debugging/resumeprocess-function)
- [PSTARTUP_CALLBACK Function Pointer - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/unmanaged-api/debugging/pstartup_callback-function-pointer)
- [Writing a .NET Debugger - Low Level Design](https://lowleveldesign.org/2010/10/11/writing-a-net-debugger-part-1-starting-the-debugging-session/)
- [ClrDebug Repository](https://github.com/lordmilko/ClrDebug)
- [Samsung NetCoreDbg](https://github.com/Samsung/netcoredbg)
