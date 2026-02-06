# Research: Exception Autopsy

## R-001: How to detect if paused at an exception

**Decision**: Check `IProcessDebugger.CurrentPauseReason == PauseReason.Exception` OR check if the most recent `BreakpointHit` (from `BreakpointManager`) has non-null `ExceptionInfo`. Both paths work — use `CurrentPauseReason` for the standalone tool (covers unhandled exceptions that don't go through breakpoint infrastructure) and `BreakpointHit.ExceptionInfo` for the `breakpoint_wait` integration.

**Rationale**: `PauseReason.Exception` is set by `ProcessDebugger.OnException2` for both exception breakpoints and unhandled exceptions (line 2548). This is the single source of truth for "am I paused at an exception?"

**Alternatives considered**: Checking `thread.CurrentException` directly — rejected because it requires ICorDebug interop and the `CurrentPauseReason` already tracks this state cleanly.

## R-002: How to get inner exception chain

**Decision**: Use `IProcessDebugger.EvaluateAsync` to walk `InnerException` chain. Evaluate `$exception.InnerException`, then `$exception.InnerException.InnerException`, etc. Alternatively, evaluate `$exception.InnerException.GetType().FullName` and `$exception.InnerException.Message` for each level. Cap at configurable depth (default 5).

**Rationale**: ICorDebug does not directly expose inner exception chains. The `EvaluateAsync` method is the established way to inspect object properties. The `$exception` pseudo-variable refers to the current exception in the debugger context.

**Alternatives considered**:
- Walking `ICorDebugValue` chain manually via field offset — too fragile, depends on runtime internals
- Single `evaluate("$exception.ToString()")` — side effects, may call custom ToString(), and doesn't give structured output

## R-003: How to get exception stack trace string

**Decision**: Evaluate `$exception.StackTrace` property via `EvaluateAsync`. This returns the runtime's formatted stack trace string. Additionally, use `GetStackFrames()` for structured frame data (file, line, function, module).

**Rationale**: The runtime `StackTrace` property gives the canonical trace. The `GetStackFrames()` from ICorDebug gives richer structured data with source locations. Both are useful — the string for raw context, the structured frames for navigation.

**Alternatives considered**: Only using `GetStackFrames()` — rejected because it may miss frames that the runtime `StackTrace` includes (e.g., rethrown exception retains original trace).

## R-004: Where to place the autopsy service

**Decision**: Create `ExceptionAutopsyService` in `DebugMcp/Services/` implementing `IExceptionAutopsyService`. Inject `IDebugSessionManager` (for stack frames, variables) and `IProcessDebugger` (for pause reason, evaluate). The tool class `ExceptionAutopsyTool` in `DebugMcp/Tools/` delegates to the service.

**Rationale**: Follows existing pattern — tools are thin MCP wrappers, services contain logic. Separating service from tool enables reuse in `BreakpointWaitTool` integration (P3).

**Alternatives considered**: Putting all logic in the tool class — rejected because `BreakpointWaitTool` also needs to call autopsy logic (FR-008).

## R-005: Tool naming convention

**Decision**: Tool name `exception_get_context`. Follows existing `noun_verb` pattern: `exception` (noun) + `get_context` (verb phrase). Aligns with `variables_get`, `stacktrace_get`, `members_get`.

**Rationale**: Constitution mandates `noun_verb` format. "exception" is already an established noun in the tool vocabulary (`breakpoint_set_exception`). "get_context" describes the bundled nature of the response.

**Alternatives considered**:
- `exception_autopsy` — not verb-based, violates naming convention
- `exception_inspect` — conflicts semantically with `object_inspect`
- `debug_exception` — "debug" is reserved for session lifecycle tools

## R-006: Variable expansion depth

**Decision**: Use existing `GetVariables()` with `scope: "all"` for the throwing frame. For one-level expansion, iterate `HasChildren` variables and call `GetVariables()` with `expandPath` for each. This matches existing behavior in `variables_get` tool.

**Rationale**: Clarification session decided on one-level expansion by default. Reusing `GetVariables` ensures consistency with `variables_get` tool output.

**Alternatives considered**: Custom ICorDebug traversal — rejected, duplicates existing logic and increases maintenance burden.
