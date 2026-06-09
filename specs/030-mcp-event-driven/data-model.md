# Data Model: MCP Event-Driven Debugger Interface (030)

## Modified Entities

### BreakpointNotification (record) — rozszerzona

**File**: `DebugMcp/Models/Breakpoints/BreakpointNotification.cs`

**Current**:
```
BreakpointNotification(
  BreakpointId: string,
  Type: BreakpointType,
  Location: NotificationLocation,
  ThreadId: int,
  Timestamp: DateTimeOffset,
  HitCount: int,
  LogMessage: string?,
  ExceptionInfo: ExceptionInfo?
)
```

**New fields** (added):
```
Locals: IReadOnlyList<VariableSummary>?,   // locals from top frame, null if unavailable
LocalsError: string?                        // "timeout"/"unavailable" if locals fetch failed
```

**Validation rules**:
- `Locals` i `LocalsError` wzajemnie wyłączne: jeśli `LocalsError` != null, `Locals` == null
- `Locals` może być pustą listą (brak zmiennych w scope) — to nie jest błąd
- Maksymalny rozmiar `Locals`: ograniczony przez 100 ms evaluation budget i max 20 items

---

### VariableSummary (record) — nowy

**File**: `DebugMcp/Models/Breakpoints/VariableSummary.cs`

```
VariableSummary(
  Name: string,        // variable name, e.g. "count"
  Type: string,        // type name, e.g. "int", "System.String"  
  Value: string,       // string representation, e.g. "42", "\"hello\""
  HasChildren: bool    // true for objects/collections with sub-members
)
```

**Validation rules**:
- `Name` nie może być null/empty
- `Value` może być `null` (gdy wartość jest null) lub `"<eval error>"` gdy ewaluacja się nie powiodła

---

## New Entities

### SessionStateChangedNotification (anonymous type w McpResourceNotifier)

**Notification method**: `"debugger/sessionStateChanged"`

**Payload schema**:
```
{
  newState: string,         // "Running" | "Paused" | "Disconnected" | "Starting"
  oldState: string,         // same enum values
  pauseReason: string?,     // "Breakpoint" | "Step" | "Exception" | "UserPause" | null
  location: {               // present when newState == "Paused"
    file: string,
    line: int,
    column: int?,
    functionName: string?,
    moduleName: string?
  } | null,
  activeThreadId: int?,     // present when newState == "Paused"
  timestamp: string         // ISO 8601 DateTimeOffset
}
```

**Triggers**: Wywołany gdy `IProcessDebugger.StateChanged` event fires — launch, continue, step complete, pause, exception pause, disconnect.

---

### ModuleResource — schema debugger://modules

**Resource URI**: `debugger://modules`  
**MIME Type**: `application/json`  
**Method**: `DebuggerResourceProvider.GetModulesJson()`

**Payload schema**:
```json
{
  "modules": [
    {
      "name": "DebugTestApp.dll",
      "fullName": "DebugTestApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
      "path": "/home/user/bin/DebugTestApp.dll",
      "version": "1.0.0.0",
      "isManaged": true,
      "isDynamic": false,
      "hasSymbols": true,
      "symbolStatus": "Loaded",
      "symbolStatusDetail": null,
      "baseAddress": "0x7f8b4a000000",
      "size": 524288
    }
  ],
  "count": 1
}
```

**Update triggers**: `IProcessDebugger.ModuleLoaded` i `ModuleUnloaded` events → `NotifyResourceUpdated("debugger://modules")` z debounce 300 ms.

**Availability**: Tylko gdy aktywna sesja. Gdy brak sesji: `{"modules":[],"count":0}` (nie rzuca błędu).

---

### SnapshotResource — schema debugger://snapshots

**Resource URI**: `debugger://snapshots`  
**MIME Type**: `application/json`  
**Method**: `DebuggerResourceProvider.GetSnapshotsJson()`

**Payload schema**:
```json
{
  "snapshots": [
    {
      "id": "snap-a1b2c3d4-...",
      "label": "Before loop assignment",
      "createdAt": "2026-06-09T10:05:00.000Z",
      "threadId": 1,
      "frameIndex": 0,
      "functionName": "Program.ProcessItems",
      "variableCount": 12
    }
  ],
  "count": 1
}
```

**Update triggers**: `ISnapshotStore.Changed` event (nowe) → `NotifyResourceUpdated("debugger://snapshots")`.  
**Availability**: Zawsze dostępny (snapshots nie wymagają aktywnej sesji). Po disconnect: snapshots są czyszczone przez `SnapshotService.OnStateChanged(Disconnected)`.

---

### SnapshotChangedEventArgs (new EventArgs)

**File**: `DebugMcp/Services/Snapshots/SnapshotChangedEventArgs.cs`

```csharp
public sealed class SnapshotChangedEventArgs : EventArgs
{
    public required string SnapshotId { get; init; }
    public required SnapshotChangeKind Kind { get; init; }  // Added | Removed | Cleared
}

public enum SnapshotChangeKind { Added, Removed, Cleared }
```

---

## Modified Interfaces

### ISnapshotStore — dodany event

**File**: `DebugMcp/Services/Snapshots/ISnapshotStore.cs`

**Added**:
```csharp
event EventHandler<SnapshotChangedEventArgs>? Changed;
```

**Implementacja w SnapshotStore**: Wywołać `Changed?.Invoke(this, new SnapshotChangedEventArgs(...))` w `Add()`, `Remove()`, `Clear()`.

---

### IBreakpointManager — usunięta metoda

**File**: `DebugMcp/Services/Breakpoints/IBreakpointManager.cs`

**Removed**:
```csharp
Task<BreakpointHit?> WaitForBreakpointAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
```

**Reason**: Jedynym konsumentem był `BreakpointWaitTool` który jest usuwany.

---

## Removed Artifacts

| Artefakt | Plik | Powód |
|----------|------|-------|
| `BreakpointWaitTool` | `DebugMcp/Tools/BreakpointWaitTool.cs` | Zastąpiony przez `debugger/breakpointHit` notification |
| `BreakpointListTool` | `DebugMcp/Tools/BreakpointListTool.cs` | Zastąpiony przez `debugger://breakpoints` resource |
| `DebugStateTool` | `DebugMcp/Tools/DebugStateTool.cs` | Zastąpiony przez `debugger://session` resource |
| `ThreadsListTool` | `DebugMcp/Tools/ThreadsListTool.cs` | Zastąpiony przez `debugger://threads` resource |
| `ModulesListTool` | `DebugMcp/Tools/ModulesListTool.cs` | Zastąpiony przez `debugger://modules` resource |
| `SnapshotListTool` | `DebugMcp/Tools/SnapshotListTool.cs` | Zastąpiony przez `debugger://snapshots` resource |
| `WaitForBreakpointAsync` | `IBreakpointManager`, `BreakpointManager` | Polling mechanism dla usuniętego tool |
| `_pendingHit` | `BreakpointManager` | Polling state dla usuniętego tool |
| `_hitWaiter` | `BreakpointManager` | Polling state dla usuniętego tool |
| `_hitLock` | `BreakpointManager` | Polling state dla usuniętego tool |
