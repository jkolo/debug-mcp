# Contract: Deferred Results (MCP Tasks)

**Feature**: 069-mcp-surface-modernization | Covers FR-007 … FR-014

Applies to exactly five tools (FR-013). Every other tool is pinned to
`TaskSupport = Forbidden` and always returns its result directly.

| Qualifying tool | Why it qualifies |
|---|---|
| `resharper_inspect_solution` | first use downloads a 180–650 MB engine, then builds the solution |
| `resharper_inspect_project` | same engine acquisition, then a project build |
| `batch_evaluate` | runs an unbounded set of expressions across breakpoint hits |
| `debug_launch` | may acquire symbols from a symbol server — see the note below |
| `code_load` | loads an MSBuild workspace across all projects in a solution |

---

### Note on `debug_launch` — qualification is per tool, not per call

FR-013 qualifies process launch *"when symbol acquisition is involved"*, which reads as a
per-call condition. It cannot be implemented as one: `TaskSupport` is a static per-tool setting,
and whether symbols will be fetched is not known until the launch is already under way.

**Resolution**: `debug_launch` is `TaskSupport = Optional` unconditionally. An opted-in client
therefore receives a handle even for a fast launch with symbols already cached, and pays one
extra `tasks/get` round-trip for it. That is accepted: opting in is the client's choice, the cost
is a single round-trip, and the alternative — dropping `debug_launch` from the set — would leave
the genuinely slow case (cold symbol cache) exposed to exactly the client-timeout failure this
slice exists to remove.

The four other qualifying tools are slow in every invocation, so the question does not arise for
them.

---

## The safety property

The specification forbids returning a task to a client that did not ask for one:

> *"Before returning a `CreateTaskResult`, verify that the client included the extension in its
> per-request capabilities. Never return a task to a client that did not declare support."*

This is what makes the feature non-breaking. Support is declared **per request**, not once per
session, so the decision is made on each call from that call's declaration alone.

---

## Opt-in — client side

```jsonc
{
  "jsonrpc": "2.0",
  "id": 10,
  "method": "tools/call",
  "params": {
    "name": "resharper_inspect_solution",
    "arguments": { "solution_path": "/src/App.sln" },
    "_meta": {
      "io.modelcontextprotocol/clientCapabilities": {
        "extensions": { "io.modelcontextprotocol/tasks": {} }
      }
    }
  }
}
```

## Advertisement — server side

The server declares the extension in its `server/discover` capabilities (FR-007):

```jsonc
"capabilities": {
  "extensions": { "io.modelcontextprotocol/tasks": {} }
}
```

---

## Path A — client opted in

**Handle, returned in under 1 second** (FR-009):

```jsonc
{
  "jsonrpc": "2.0",
  "id": 10,
  "result": {
    "resultType": "task",
    "task": {
      "taskId": "tsk-9f3c1a...",
      "status": "working",
      "createdAt": "2026-08-25T10:14:02.117+00:00",
      "ttlMs": 3600000,
      "pollIntervalMs": 2000
    }
  }
}
```

**Polling:**

```jsonc
// → tasks/get { "taskId": "tsk-9f3c1a..." }
// ← still running
{ "taskId": "tsk-9f3c1a...", "status": "working",
  "statusMessage": "building solution" }

// ← finished
{
  "taskId": "tsk-9f3c1a...",
  "status": "completed",
  "result": {
    "content": [ { "type": "text", "text": "{\"success\":true,...}" } ],
    "structuredContent": { "success": true, "findings": [ /* ... */ ] },
    "isError": false
  }
}
```

The `result` payload is **byte-identical** to what Path B returns for the same inputs (FR-014).
This is directly assertable and should be a test.

**Failure** drives the task to `failed` carrying the same `ToolError` the blocking path would
have produced (FR-010).

**Cancellation** — `tasks/cancel` is acknowledged immediately, but is *cooperative*: the task may
still reach `completed` or `failed` if the underlying work could not be interrupted without
leaving the debug session inconsistent (FR-003, FR-011).

---

## Path B — client did not opt in

Byte-for-byte today's behaviour. The call blocks and returns the ordinary result. No handle, no
error, no warning (FR-008).

---

## Enquiry errors (FR-012)

Unknown and expired are **distinguishable** — the client must be able to tell "I never had this"
from "I waited too long":

| Situation | Response |
|---|---|
| `taskId` never existed, or belongs to a previous server process | not-found error naming the id |
| `taskId` existed but `ttlMs` has elapsed | expiry error, distinct code, stating the TTL |

Never a stale result, never an empty success.

---

## Lifecycle edge cases

| Event | Required outcome |
|---|---|
| Debuggee terminates while a task is outstanding | task → `failed` with the reason; never `completed`, never left `working` |
| Client disconnects mid-flight | server stops the work rather than burning CPU for an absent client |
| Server restarts | handles do not survive; enquiry returns not-found |
| Two long operations at once | independent handles and independent progress streams, no cross-talk |
| `input_required` | not produced by this feature — no operation here needs mid-flight input |

---

## Configuration note for implementation

**Corrected against the installed 2.2.0 assemblies** — the SDK has no per-tool
`Execution.TaskSupport` property; that was a mistaken assumption in an earlier draft of this
contract. Task eligibility is one server-wide delegate:

```csharp
builder.WithTasks(taskStore, opts =>
    opts.ExecutionModeSelector = ctx => taskExecutionPolicy.SelectMode(ctx));
```

`McpTasksOptions.ExecutionModeSelector: Func<RequestContext<CallToolRequestParams>,
McpTaskExecutionMode>` is evaluated per request; `McpTaskExecutionMode` is `Synchronous` |
`Optional` | `Required`. The SDK's documented default *"treats every tool as task-capable"* — so
leaving the selector unset would silently make all 39 tools task-eligible, contradicting FR-013,
exactly as the old wording warned, just via a different mechanism: **not** an implicit
per-tool default that individual tool files must override, but a **must-supply-explicitly**
selector at registration. `Services/Tasks/TaskExecutionPolicy.cs` holds the classification (the
five qualifying tool names → `Optional`, everything else → `Synchronous`) as one table, consulted
by the selector — there is no `TaskSupport` value to pin on the 34 non-qualifying tool files
because no such per-tool setting exists. This is still the single most likely way to get this
slice wrong; the risk moved from "a missed file" to "a missing or mis-scoped selector."
