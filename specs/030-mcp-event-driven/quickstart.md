# Quickstart & Verification: MCP Event-Driven Debugger Interface (030)

## Prerequisites

```bash
cd /home/jurek/src/Own/debug-mcp.net
git checkout 030-mcp-event-driven
dotnet build
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
# All tests pass before implementation begins
```

---

## Verification Scenario 1: Tools removed — contract tests pass

**Goal**: Verify that 6 tools are absent from the server's tool list.

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ToolAnnotationTests"
```

**Expected**: `ToolAnnotationTests` passes with count assertion `HaveCount(35)` and no entries for `breakpoint_wait`, `breakpoint_list`, `debug_state`, `threads_list`, `modules_list`, `snapshot_list`.

---

## Verification Scenario 2: debugger://modules resource readable

**Goal**: Verify new resource returns module list.

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ModulesResourceTests"
```

**Expected**: Unit test reads `debugger://modules` from `DebuggerResourceProvider.GetModulesJson()` and receives valid JSON with `modules` array and `count` field.

---

## Verification Scenario 3: debugger://snapshots resource readable and updates

**Goal**: Verify new resource returns snapshot list and sends notification on change.

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~SnapshotsResourceTests"
```

**Expected**:
- `GetSnapshotsJson()` returns `{"snapshots":[],"count":0}` when no snapshots
- After `SnapshotStore.Add(...)`, `Changed` event fires
- After subscribing to `debugger://snapshots`, `NotifyResourceUpdated` is called

---

## Verification Scenario 4: breakpointHit notification includes locals

**Goal**: Verify notification payload contains locals when process is paused.

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~BreakpointNotificationLocalsTests"
```

**Expected**:
- `BreakpointNotification` with `Locals` populated (at least one variable)
- `BreakpointNotification` with `LocalsError = "timeout"` when evaluation times out (mock 100ms exceeded)
- `BreakpointNotification` with `Locals = null, LocalsError = null` for tracepoints (locals not fetched)

---

## Verification Scenario 5: sessionStateChanged notification fired

**Goal**: Verify `debugger/sessionStateChanged` notification is sent on state transitions.

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~SessionStateNotificationTests"
```

**Expected**:
- Raising `StateChanged(Running → Paused)` in ProcessDebugger mock → MCP notification with `newState: "Paused"`, `pauseReason: "Breakpoint"`, `location` populated
- Raising `StateChanged(Paused → Running)` → notification with `newState: "Running"`, `location: null`
- Raising `StateChanged(any → Disconnected)` → notification with `newState: "Disconnected"`

---

## Verification Scenario 6: process_read_output and process_write_input are synchronous

**Goal**: Verify tools have `string` return type (not `Task<string>`).

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ProcessIoAsyncTests"
```

**Expected**: Reflection test verifies that `ProcessReadOutputTool.ReadOutput()` and `ProcessWriteInputTool.WriteInput()` return `string` not `Task<string>`.

---

## Verification Scenario 7: WaitForBreakpointAsync removed from interface

**Goal**: Verify `IBreakpointManager` no longer has polling method.

```bash
grep -r "WaitForBreakpointAsync" DebugMcp/
# Expected: no results (or only in old commit history)

grep -r "_pendingHit\|_hitWaiter\|_hitLock" DebugMcp/
# Expected: no results
```

---

## Full Test Suite

```bash
dotnet build
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
# Expected: all tests pass (no regressions)
```

---

## Manual Smoke Test (optional, requires test app)

```bash
# Start debug-mcp server
dotnet run --project DebugMcp -- tests/DebugTestApp/bin/Debug/net10.0/DebugTestApp.dll

# In another terminal, verify resources via MCP protocol:
# 1. resources/list → should include debugger://modules and debugger://snapshots
# 2. resources/read {uri: "debugger://modules"} → {"modules": [], "count": 0}
# 3. tools/list → should NOT include breakpoint_wait, breakpoint_list, debug_state, threads_list, modules_list, snapshot_list
# 4. tools/list count → 35
```
