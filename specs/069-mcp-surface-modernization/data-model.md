# Phase 1 Data Model: MCP Surface Modernization

**Feature**: 069-mcp-surface-modernization | **Date**: 2026-08-25

Project conventions apply throughout: **positional records**, immutable updates via `with`,
**`DateTimeOffset` never `DateTime`**, and existing ID prefixes (`bp-`, `tp-`, `ebp-`) unchanged.

---

## 1. Tool result envelope

Replaces the hand-assembled `JsonSerializer.Serialize(new { success = ..., ... })` that every
tool builds today.

### `ToolResult<T>`

| Field | Type | Notes |
|---|---|---|
| `Success` | `bool` | **Retained verbatim** — FR-021. Existing consumers read this field today. |
| `Data` | `T?` | The typed success payload. Null when `Success` is false. |
| `Error` | `ToolError?` | Null when `Success` is true. |
| `Warnings` | `IReadOnlyList<string>` | Empty by default. Serves the constitution's "partial success: return data with warnings array". |
| `Truncation` | `TruncationInfo?` | Present only when the result was bounded. Never silent — see edge case in spec. |

The protocol-level `isError` flag is set from `Success == false`; it is **not** a field on the
record. Both signals are emitted: `isError` for clients that read the protocol, `Success` for
clients that read the payload.

### `ToolError`

| Field | Type | Notes |
|---|---|---|
| `Code` | `string` | **MUST** be one of the constants in the existing `ErrorCodes` set (`DebugMcp/Models/ErrorResponse.cs`). No tool invents a code — FR-019. |
| `Message` | `string` | Human-readable and actionable. Fed back to the model for self-correction. |
| `Details` | `object?` | Optional structured context — the offending parameter, its value, valid range. |

### `TruncationInfo`

| Field | Type | Notes |
|---|---|---|
| `Returned` | `int` | Items actually present. |
| `Available` | `int?` | Total known to exist; null when the total is not cheaply knowable. |
| `Reason` | `string` | Why bounding occurred, e.g. a size cap. |

**Validation rules**
- `Success == true` ⟹ `Error` is null and `Data` is non-null.
- `Success == false` ⟹ `Error` is non-null and `Data` is null.
- `Error.Code` ∈ `ErrorCodes`. Enforced by a contract test, not by convention.

---

## 2. Per-tool payload records

One record per tool, in `DebugMcp/Models/Results/`. Each becomes the tool's `outputSchema`.

Field names and value semantics are carried over **unchanged** from the JSON each tool emits
today (FR-021) — this is a change of representation, not of content. Example:

```
VariablesResult(
    IReadOnlyList<VariableInfo> Variables,
    int? ThreadId,
    int FrameIndex)

VariableInfo(
    string Name,
    string Type,
    string Value,
    string Scope,          // "local" | "argument" | "this"
    bool HasChildren)
```

The `has_children` / `HasChildren` style difference is a serialization concern; the wire names
that exist today are preserved through the serializer's naming policy, not by renaming fields.

---

## 3. Deferred-result handle

Owned by `InMemoryMcpTaskStore`; the shape below is what the client observes.

| Field | Type | Notes |
|---|---|---|
| `TaskId` | `string` | Unique, opaque. Clients must not parse it. |
| `Status` | `TaskStatus` | See lifecycle below. |
| `CreatedAt` | `DateTimeOffset` | |
| `TtlMs` | `int` | Expiry window. After it elapses, enquiry returns an expiry error, distinguishable from not-found (FR-012). |
| `PollIntervalMs` | `int` | Server's suggestion to the client. |
| `StatusMessage` | `string?` | Current stage name; mirrors the latest progress update. |
| `Result` | `ToolResult<T>?` | Populated only on `Completed`. Byte-identical to what the blocking path returns (FR-014). |
| `Error` | `ToolError?` | Populated only on `Failed`. |

### Lifecycle

```
                 ┌──────────────► Completed  (terminal)
                 │
    Working ─────┼──────────────► Failed     (terminal)
       │         │
       │         └──────────────► Cancelled  (terminal)
       │
       └──► InputRequired ──► Working
              (not produced by this feature — see research.md R9)
```

**Transition rules**
- `Working` is the only non-terminal state this feature produces.
- Terminal states never change once reached.
- Cancellation is **cooperative**: `tasks/cancel` is acknowledged immediately, but the task may
  still reach `Completed` or `Failed` if the work could not be interrupted safely (FR-003).
- Debuggee termination while a handle is outstanding drives it to `Failed` carrying the reason —
  never to `Completed`, never left hanging.
- Handles do not survive a process restart. An enquiry naming a handle from a previous process
  returns not-found, never a stale result.

---

## 4. Progress update

Advisory and fire-and-forget. Carries no data the final result does not also carry.

| Field | Type | Notes |
|---|---|---|
| `Stage` | `string` | Human-readable, e.g. `"acquiring ReSharper engine"`, `"building solution"`. |
| `Completed` | `int?` | Null when the operation has no countable unit of work. |
| `Total` | `int?` | Null when the total is not knowable in advance — common for downloads of unknown size. |

**Rules**
- Emitted only when the client supplied a progress token; otherwise discarded by the SDK with no
  branching in tool code (FR-005).
- Stage sequence is ordered. Three emission patterns, and only these three:
  1. **Named stages** — emitted once each, on transition, in a fixed order. Never repeat.
  2. **Counted stages** — an *n* of *m* stage emits on every increment of *n*. The stage name
     repeats by design; `Completed` advances each time.
  3. **Heartbeats** — a long silent stage re-emits its *current* stage unchanged, to satisfy
     SC-001's 60-second ceiling. `Completed` does not advance.
- Absence of progress is never an error (spec edge case).

### Stage inventory for the qualifying tools

| Tool | Stages |
|---|---|
| `resharper_inspect_solution` | acquiring engine → restoring → building solution → inspecting → parsing report |
| `resharper_inspect_project` | acquiring engine → restoring → building project → inspecting → parsing report |
| `batch_evaluate` | *n* of *m* expressions evaluated (countable) |
| `debug_launch` | starting process → attaching → resolving symbols → ready |
| `code_load` | locating MSBuild → loading workspace → *n* of *m* projects loaded (countable) |

---

## 5. Ranked suspect

Output of deterministic enrichment. **Strictly additive** — every field present before this
feature remains (FR-025).

| Field | Type | Notes |
|---|---|---|
| `FrameIndex` | `int` | References a frame already present in the raw result. |
| `Score` | `double` | Deterministic. Same state ⟹ same score, bit for bit (FR-023, SC-008). |
| `Reasons` | `IReadOnlyList<SuspicionReason>` | Non-empty. A rank with no evidence is not emitted. |

### `SuspicionReason`

| Field | Type | Notes |
|---|---|---|
| `Heuristic` | `string` | Names the rule that fired. Each rule is documented and independently testable (FR-027). |
| `Weight` | `double` | This rule's contribution to `Score`. Weights are documented constants, not tuned at runtime. |
| `Evidence` | `string` | Concrete and checkable, e.g. `"local 'order' is null"`. |
| `Location` | `SourceLocation?` | File and line where the evidence sits, when symbols allow. |

### Availability

When ranking cannot be computed — no symbols, state unavailable — the enrichment field is
present and explicitly says so (FR-026). It is never silently omitted, and its absence never
fails the call:

```
RankingUnavailable(string Reason)   // e.g. "no PDB loaded for MyApp.dll"
```

**Determinism rules**
- No wall-clock time, no random source, no hash-order iteration may influence `Score` or ordering.
- Ties break on `FrameIndex` ascending, so ordering is total and reproducible.

### What determinism means here, precisely

debug-mcp has **no record-and-replay**. A live ICorDebug session cannot be re-run bit-for-bit —
addresses, thread IDs, PIDs and timings differ on every run. Determinism is therefore scoped
deliberately:

- **The fault-scenario corpus (FR-030) is a set of deterministic fixture programs**, living in
  `tests/DebugTestApp/FaultScenarios/`, each paired with the frame a human identifies as the
  fault site. "Replaying a scenario" means *running that fixture again under the debugger*, not
  restoring a recording.
- **Determinism is asserted on the normalized enrichment output** — `FrameIndex`, `Score`,
  `Heuristic`, `Weight`, `Evidence`, and ordering. Volatile runtime facts (addresses, thread IDs,
  PIDs, durations) are excluded from the comparison because they are not enrichment output.

This is exactly what FR-023 requires — *identical debuggee state yields an identical ranking* —
and what SC-008 measures, since SC-008 speaks of "enrichment output", not of the whole response.
A fixture whose own logic is non-deterministic (unseeded randomness, wall-clock branching, racing
threads) does not belong in the corpus.

---

## 6. What this model does *not* introduce

- No persistence of any kind. Nothing is written to disk.
- No new identifier scheme beyond `TaskId`; existing `bp-` / `tp-` / `ebp-` prefixes are untouched.
- No changes to tool **inputs** — names, parameters and their semantics are unchanged (FR-021,
  and the timeout deviation recorded in [plan.md](./plan.md)).
- No changes to the 7 resources or 4 prompts.
