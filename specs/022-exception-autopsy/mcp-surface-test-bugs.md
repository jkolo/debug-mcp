# MCP Surface Test — Bugs Found

Date: 2026-02-06
Branch: 022-exception-autopsy (after deadlock fix)

## Summary

Tested 26 scenarios across all MCP tool categories. Found 4 bugs (all fixed).

### Tools Tested (all categories)

| Category | Tools | Status |
|----------|-------|--------|
| Session | debug_launch, debug_state, debug_continue, debug_pause, debug_step, debug_disconnect | OK |
| Breakpoints | breakpoint_set, breakpoint_list, breakpoint_enable, breakpoint_remove, breakpoint_wait | OK |
| Exception breakpoints | breakpoint_set_exception, breakpoint_wait (include_autopsy) | OK (but see BUG-6, BUG-7, BUG-8) |
| Tracepoints | tracepoint_set | OK |
| Inspection | stacktrace_get, threads_list, variables_get, evaluate | OK |
| Memory | object_inspect, memory_read, layout_get, references_get | OK |
| Modules | modules_list, modules_search, types_get | OK |
| Code analysis | code_load, code_goto_definition, code_find_usages, code_find_assignments, code_get_diagnostics | OK |
| Process I/O | process_write_input, process_read_output | OK |
| Exception autopsy | exception_get_context | OK (but see BUG-6) |

---

## BUG-5 (FIXED): _missedDisconnect stale flag causes "Process exited during launch"

**Severity**: Critical (blocks all subsequent launches)
**Status**: Fixed during this test session

### Steps to reproduce
1. `debug_launch` with `stopAtEntry: true`
2. `debug_continue`
3. `debug_disconnect` with `terminateProcess: true`
4. `debug_launch` again → **FAIL**: "Process exited during launch"

### Root cause
After `DisconnectAsync` clears `_currentSession = null` in its `finally` block, a late `OnExitProcess` callback fires. `OnStateChanged` sees `_currentSession == null` and sets `_missedDisconnect = true`. The next `LaunchAsync` then falsely thinks the new process exited during launch.

### Fix
Reset `_missedDisconnect = false` at the start of `LaunchAsync` and `AttachAsync`, inside the initial `lock (_lock)` block (where we check `_currentSession != null`). The flag should only be relevant for the current operation.

**File**: `DebugMcp/Services/DebugSessionManager.cs`

---

## BUG-6 (FIXED): Empty exception message at first-chance exception

**Severity**: Medium
**Status**: Fixed

### Steps to reproduce
1. `debug_launch` TestTargetApp with `stopAtEntry: true`
2. `debug_continue`
3. `breakpoint_set_exception` for `System.InvalidOperationException`
4. `process_write_input` "exception\n"
5. `breakpoint_wait` with `include_autopsy: true`
6. Observe: `exceptionInfo.message` is `""` (empty string)
7. Also: `exception_get_context` returns `message: ""`

### Expected
Message should be `"Test exception from ExceptionTarget"` (the string passed to `new InvalidOperationException(...)`).

### Notes
At first-chance exception time, the CLR may not have finished constructing the exception object. The `_message` field might not be populated yet when the `OnException2` callback fires. The `LastExceptionInfo` fallback (BUG-3 fix) stores the message from callback event args, which itself may be empty at first-chance.

### Fix
Changed `GetExceptionInfo` from static to instance method and rewrote `TryGetExceptionMessage` to use `TryGetFieldValue("_message")` which reads the `_message` field directly from the `ICorDebugObjectValue` via type hierarchy traversal.

**File**: `DebugMcp/Services/ProcessDebugger.cs`

---

## BUG-7 (FIXED): breakpoint_remove doesn't handle exception breakpoint IDs

**Severity**: Medium
**Status**: Fixed

### Steps to reproduce
1. `breakpoint_set_exception` for `System.InvalidOperationException` → returns `id: "ebp-..."`
2. `breakpoint_remove` with that ID → **FAIL**: `BREAKPOINT_NOT_FOUND`

### Expected
Either `breakpoint_remove` should route `ebp-*` IDs to `RemoveExceptionBreakpointAsync`, or there should be a dedicated MCP tool for removing exception breakpoints.

### Fix
Added routing in `BreakpointRemoveTool.RemoveBreakpointAsync` — IDs starting with `ebp-` are routed to `RemoveExceptionBreakpointAsync` instead of `RemoveBreakpointAsync`.

**File**: `DebugMcp/Tools/BreakpointRemoveTool.cs`

---

## BUG-8 (FIXED): Exception breakpoints persist across sessions

**Severity**: Medium
**Status**: Fixed

### Steps to reproduce
1. `debug_launch` TestTargetApp
2. `breakpoint_set_exception` for `System.InvalidOperationException` → `ebp-xxx`
3. Trigger exception, breakpoint fires (hitCount: 1)
4. `debug_disconnect` with `terminateProcess: true`
5. `debug_launch` TestTargetApp (new session)
6. `breakpoint_list` → still shows `ebp-xxx` with hitCount: 1 from previous session

### Expected
All breakpoints (including exception breakpoints) should be cleared on session disconnect via `ClearAllBreakpointsAsync`.

### Root cause
`BreakpointManager` didn't subscribe to `StateChanged` events, so `ClearAllBreakpointsAsync` was never called on disconnect. (`BreakpointRegistry.Clear()` does clear both source and exception breakpoints — it just was never invoked.)

### Fix
Added `_processDebugger.StateChanged += OnStateChanged` subscription in `BreakpointManager` constructor. The handler calls `ClearAllBreakpointsAsync()` when `NewState == SessionState.Disconnected`.

**File**: `DebugMcp/Services/Breakpoints/BreakpointManager.cs`

---

## Non-bugs / Known Limitations

### Step location shows "Unknown" in Release builds
After `debug_step` (over/out), the location shows `file: "Unknown", line: 0`. This is a Release build PDB limitation — sequence points are missing for optimized code. Not a bug in our code. **Workaround**: Build with Debug configuration for full stepping support.

### Evaluate doesn't support complex expressions
`evaluate` fails on chained property access (`_currentUser.HomeAddress.City`) and arithmetic (`1 + 2`). Only simple variable names (`name`) and `this.field` work. This is a known limitation of the ICorDebug expression evaluator — not a new bug.

### Empty thread list at entry point
`threads_list` returns empty array when paused at entry point. The runtime hasn't fully initialized its thread structures yet. Normal behavior.
