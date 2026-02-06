# Bugs Found During 022 Exception Autopsy Testing

## BUG-1: `debug_disconnect` hangs when process paused at unhandled exception

**Severity**: High (blocks MCP client)
**Status**: Partially fixed (timeout + force-kill added to tool layer)

**Reproduction**:
1. Launch a .NET app under debugger: `debug_launch(program, stopAtEntry: false)`
2. Trigger an unhandled exception (process pauses with `pauseReason: "exception"`)
3. Call `debug_disconnect(terminateProcess: true)`
4. Tool hangs indefinitely

**Root cause**: `ProcessDebugger.TerminateAsync()` calls `Continue(false)` when paused, then `Terminate()` with 5s timeout + OS kill fallback. However, when paused at an *unhandled* exception, the `Continue(false)` resumes the process which then immediately hits the unhandled exception handler again, creating a loop or state where `Terminate()` never completes. The `_corDebug.Terminate()` cleanup at line 662 may also hang.

**Partial fix applied**: Added 10-second timeout to `DebugDisconnectTool` with `ForceKillProcess()` fallback. The tool now returns `{ success: true, timedOut: true }` instead of hanging forever.

**Full fix needed**: Investigate why `ProcessDebugger.TerminateAsync()` hangs when the process was paused at an unhandled exception. The internal 5s timeout + OS kill should prevent this, but it doesn't work in this scenario.

---

## BUG-2: Exception breakpoints (`breakpoint_set_exception`) don't pause the process

**Severity**: High (exception breakpoints are non-functional)
**Status**: Open

**Reproduction**:
1. Launch TestTargetApp: `debug_launch(..., stopAtEntry: true)`
2. Set exception breakpoint: `breakpoint_set_exception(exception_type: "System.InvalidOperationException")`
3. Continue: `debug_continue()`
4. Send `exception` command to stdin (triggers `throw new InvalidOperationException(...)`)
5. Process does NOT pause — exception is caught by try/catch and process continues

**Root cause**: In `ProcessDebugger.cs` line 2542-2558, the `OnException2` handler only calls `UpdateState(Paused, Exception)` for **unhandled** exceptions (`isUnhandled == true`). For first-chance exceptions, it fires the `ExceptionHit` event and then calls `ShouldAutoContinue()` which always returns true because there's no mechanism for `BreakpointManager.OnExceptionHit()` to signal "don't continue".

The `BreakpointManager.OnExceptionHit()` (line 792) correctly finds matching exception breakpoints and signals the hit to waiters, but it has no way to tell `ProcessDebugger` to NOT call `e.Controller.Continue(false)`. Unlike regular breakpoints (which use `BreakpointHitEventArgs.ShouldContinue`), exception events don't have a similar mechanism.

**Fix needed**: Add a similar `ShouldContinue` flag to `ExceptionHitEventArgs`, and have `ProcessDebugger.OnException2` check it after invoking the event. `BreakpointManager.OnExceptionHit()` should set it to `false` (don't continue) when a matching exception breakpoint exists.

---

## BUG-3: `exception_get_context` returns `Unknown` type and empty message for unhandled exceptions

**Severity**: Medium (autopsy returns partial data)
**Status**: Open

**Reproduction**:
1. Launch app that throws unhandled exception
2. Process pauses with `pauseReason: "exception"`
3. Call `exception_get_context()`
4. Response has `exception.type: "Unknown"` and `exception.message: ""`

**Root cause**: `ExceptionAutopsyService` uses `EvaluateAsync("$exception.GetType().FullName")` to get the exception type. When paused at an unhandled exception, the `$exception` pseudo-variable may not be accessible via `EvaluateAsync` — the evaluation might fail silently (returning `Success: false`).

**Fix needed**: Fall back to the exception type/message from `ExceptionHitEventArgs` (stored in ProcessDebugger or BreakpointManager) when `$exception` evaluation fails. The `ExceptionHitEventArgs` already has `ExceptionType` and `ExceptionMessage` fields that were populated by `GetExceptionInfo(e.Thread)` at the time the exception was thrown.

---

## BUG-4: `breakpoint_list` doesn't show exception breakpoints

**Severity**: Low (confusing UX)
**Status**: Open

**Reproduction**:
1. Set exception breakpoint: `breakpoint_set_exception(exception_type: "System.InvalidOperationException")`
2. Returns success with breakpoint ID
3. Call `breakpoint_list()`
4. Returns empty list (count: 0)

**Root cause**: `breakpoint_list` likely only queries source breakpoints from `BreakpointRegistry`, not exception breakpoints.

**Fix needed**: Include exception breakpoints in `breakpoint_list` response, or add a separate `breakpoint_list_exceptions` tool.
