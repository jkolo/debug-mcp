# Contract: Progress Reporting

**Feature**: 069-mcp-surface-modernization | Covers FR-004, FR-005, and SC-001

---

## Mechanism

A tool declares an `IProgress<ProgressNotificationValue>` parameter. The SDK excludes it from the
tool's `inputSchema`, so it is invisible to callers, and binds an instance automatically.

```csharp
public async Task<ToolResult<InspectionResult>> InspectSolution(
    string solution_path,
    IProgress<ProgressNotificationValue> progress,
    CancellationToken cancellationToken)
```

**Why this shape**: if the client supplied a `progressToken` on the request, reports propagate as
`notifications/progress`. If it did not, the SDK discards them. There is no capability check and
no branching in tool code — the silent degradation FR-005 requires is structural, not
conditional.

Works over stdio: progress notifications are ordinary JSON-RPC notifications on the same
connection.

---

## Wire exchange

**Request carrying a progress token:**

```jsonc
{
  "jsonrpc": "2.0",
  "id": 11,
  "method": "tools/call",
  "params": {
    "name": "resharper_inspect_solution",
    "arguments": { "solution_path": "/src/App.sln" },
    "_meta": { "progressToken": 11 }
  }
}
```

**In-flight notifications:**

```jsonc
{ "jsonrpc": "2.0", "method": "notifications/progress",
  "params": { "progressToken": 11, "progress": 1, "total": 5,
              "message": "acquiring ReSharper engine" } }

{ "jsonrpc": "2.0", "method": "notifications/progress",
  "params": { "progressToken": 11, "progress": 3, "total": 5,
              "message": "building solution" } }
```

**Then the ordinary result**, unchanged in shape.

**Request without a progress token**: identical behaviour to today. No notifications, no error.

---

## Stage inventory

Stage names are user-facing strings and part of this contract.

| Tool | Stages | Countable? |
|---|---|---|
| `resharper_inspect_solution` | acquiring engine → restoring → building solution → inspecting → parsing report | yes, 5 |
| `resharper_inspect_project` | acquiring engine → restoring → building project → inspecting → parsing report | yes, 5 |
| `batch_evaluate` | evaluating expression *n* of *m* | yes, *m* |
| `debug_launch` | starting process → attaching → resolving symbols → ready | yes, 4 |
| `code_load` | locating MSBuild → loading workspace → project *n* of *m* | partly |

Tools outside this list have no distinguishable stages and emit no progress. **Absent progress is
never an error** — that is a spec-level edge case, not a defect.

---

## Timing obligations (SC-001)

| Obligation | Value |
|---|---|
| First stage update | within **5 seconds** of the call |
| Update on stage change | always |
| Maximum silence | **60 seconds** |

The 60-second ceiling has a real consequence: engine acquisition downloads hundreds of megabytes
and the underlying tool is silent throughout. Satisfying the ceiling requires **re-emitting the
current stage as a heartbeat**, optionally with byte counts, rather than only emitting on
transitions. This is a deliberate implementation obligation, called out here so it is not
discovered late.

---

## Cancellation pairing

Progress and cancellation travel together: a client that can see an operation is stuck is the
same client that needs to abandon it.

- `CancellationToken` is threaded to the same depth as `IProgress`.
- Cancellation is honoured **at the earliest point that leaves the debug session consistent**. An
  indivisible ICorDebug step runs to completion first; the debuggee is never left inconsistent to
  honour a cancellation (FR-003).
- After cancelling, the client's next call must succeed within 5 seconds with the session still
  usable (SC-002).

---

## Testability

`IMcpServer.SendNotificationAsync` is an extension method and cannot be mocked with Moq. Unit
tests therefore assert against a **recording `IProgress<T>` double**, following the existing
`IBreakpointNotifier` / `NullBreakpointNotifier` precedent in `tests/DebugMcp.Tests/Support/`.

What a double cannot cover — that a real `progressToken` actually produces
`notifications/progress` on the wire — is covered by the stdio scenario in
[quickstart.md](../quickstart.md).
