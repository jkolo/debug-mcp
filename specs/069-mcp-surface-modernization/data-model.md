# Phase 1 Data Model: MCP Surface Modernization

**Feature**: 069-mcp-surface-modernization | **Date**: 2026-08-25

Project conventions apply throughout: **positional records**, immutable updates via `with`,
**`DateTimeOffset` never `DateTime`**, and existing ID prefixes (`bp-`, `tp-`, `ebp-`) unchanged.

---

## 1. Tool result envelope

Replaces the hand-assembled `JsonSerializer.Serialize(new { success = ..., ... })` that every
tool builds today.

**Corrected during US3 implementation, after an empirical pilot (`SnapshotDeleteTool`) against
SDK 2.2.0.** `ToolResult<T>` (below) does **not** become the wire-serialized type any tool
returns. Verified via `[McpServerTool(UseStructuredContent = true)]` + `McpServerTool.Create` /
a real client over `tests/DebugMcp.Tests/Support/InProcessMcpHarness.cs`: the SDK derives both
`outputSchema` and `structuredContent` directly from the tool method's C# return type by
reflection, with **no flattening step** — a method returning `Task<ToolResult<VariablesResult>>`
would publish (and emit) a schema with `data` as a **nested** object, but
[contracts/tool-result-contract.md](./contracts/tool-result-contract.md)'s wire examples are
**flat** (`success` and `variables` as siblings). Reconciling the two would need a custom
`JsonConverter` that flattens `Data`'s properties into the parent object at write time *and*
teaches the SDK's schema generator to do the same at discovery time — verified unnecessary: each
tool instead defines **its own flat record**, combining `Success`, its domain fields, and
`Error` as siblings, and returns that record directly. `ToolResult<T>` remains in
`DebugMcp/Models/Results/ToolResult.cs` — its invariant-checking constructor and its own unit
tests (T004) are still valid and useful as a general-purpose validation aid — but no tool's
method signature uses it as a return type. `ToolError` and `TruncationInfo` **are** reused
directly, unchanged, as the nested `error`/`truncation` wire shapes every flat record embeds.

**Requiredness pitfall, also found on the pilot**: a positional record parameter with no default
becomes `required` in the generated schema regardless of its C# nullability annotation (`string?`
without `= null` is still schema-required). Since a failure result omits every domain field, only
`Success` may lack a default — every other property, including `Error`, **MUST** declare
`= null` (or an equivalent default) or the tool's own failure results fail their own schema
(caught by T040). This also corrects this contract's earlier `"required": ["success",
"variables"]` example, which is unachievable once failure results are validated against the same
schema — `contracts/tool-result-contract.md` is corrected accordingly.

### `ToolResult<T>` — validation helper, not a wire type (see correction above)

| Field | Type | Notes |
|---|---|---|
| `Success` | `bool` | **Retained verbatim** — FR-021. Existing consumers read this field today. |
| `Data` | `T?` | The typed success payload. Null when `Success` is false. |
| `Error` | `ToolError?` | Null when `Success` is true. |
| `Warnings` | `IReadOnlyList<string>` | Empty by default. Serves the constitution's "partial success: return data with warnings array". |
| `Truncation` | `TruncationInfo?` | Present only when the result was bounded. Never silent — see edge case in spec. |

**Per-tool flat records** carry the equivalent shape directly: `Success`, the tool's own domain
fields (each defaulted to `null`), `Error`, and — for the tools FR-035 lists — `Truncation`.
`Warnings` is included only on tools that actually use it.

The protocol-level `isError` flag is set from `Success == false` by **one central mechanism**,
not per tool (T053): a `McpRequestFilterBuilderExtensions.AddCallToolFilter` registered once in
`Program.cs`, which inspects the outgoing `CallToolResult.StructuredContent` for a top-level
`success` boolean and sets `IsError` accordingly after the tool method returns. Verified this
composes correctly with MCP Tasks deferral (T031/T032): a deferred call's stored task `Result`
carries the same `isError` the synchronous path would have, because the filter runs as part of
the same call-tool pipeline the Tasks extension wraps.

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

### Accepted wire-shape deviations from FR-021 ("field names and meanings carried over unchanged")

Migrating 39 tools onto one shared `ToolError`/envelope shape surfaced three cases where a
byte-identical wire shape and the shared shape genuinely conflict. Rather than special-case the
shared types per tool, each was resolved once, as policy:

1. **Error fields with no home in `ToolError`.** `evaluate`/`evaluate_safe` legacy errors carried
   `position` and `exception_type` as siblings of `code`/`message`. `ToolError` has no dedicated
   fields for them (adding tool-specific fields to the shared error type would defeat FR-018's
   "one shape for all 39 tools"). Resolution: both now live under `error.details.position` /
   `error.details.exception_type` — same data, nested one level deeper. No information is lost.

2. **Enum representation: bespoke wire records vs. reused domain types.** The SDK's default
   structured-content serializer renders C# enums as strings (`"kind":"Method"`); legacy
   hand-rolled JSON (no enum converter configured) emitted raw integers. Policy: a **bespoke wire
   record** (one written specifically for the tool's result, e.g. `TimelineQueryEventWire`)
   preserves the legacy representation exactly, including raw-int enums where that's what shipped
   before. A tool whose result **reuses a shared domain type** with an enum property
   (`WorkspaceInfo.Type`, `SymbolUsage.Kind`, `SymbolAssignment.Kind`, `DiagnosticInfo.Severity` —
   all in the `code_*` tools, T048) follows the SDK serializer's string rendering instead; adding
   `JsonConverter` attributes to those shared models would also change every MCP *resource* that
   reuses them, which is out of scope here. The delta is accepted per-field, not fixed with a
   converter.

3. **Custom lowercase error codes.** `batch_evaluate`'s five codes (`validation_error`,
   `batch_already_running`, `invalid_json`, `cancelled`, `internal_error`) predate this migration
   and don't follow the `ErrorCodes` set's `UPPER_SNAKE_CASE` convention. Rather than document
   this as a deviation from FR-019 ("codes drawn from `ErrorCodes`"), the five were added to
   `ErrorCodes` verbatim, case preserved — this satisfies FR-019 with zero wire change.

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
| `StatusMessage` | `string?` | **Never populated by this feature.** Confirmed empirically against SDK 2.2.0: a running tool has no way to update its own task's `StatusMessage` (no store method, no ambient task id available to the tool body). Progress still reports via the separate `notifications/progress` channel, unaffected by deferral. |
| `Result` | `ToolResult<T>?` | Populated on `Completed` — including when the underlying tool call threw: the SDK catches the exception itself and completes the task with `isError:true` rather than failing it. Byte-identical to what the blocking path returns (FR-014). |
| `Error` | `ToolError?` | Populated only on `Failed`. In practice this feature's five qualifying tools never produce `Failed`: each already catches its own exceptions into a `{success:false,...}` `Completed` result (see the transition rule below), and nothing in this feature calls `IMcpTaskStore.SetFailedAsync` directly. |

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
- Debuggee termination (or any other failure) while a handle is outstanding drives it to
  `Completed` carrying the tool's own `{success:false,error:{...}}` JSON — **not** `Failed`.
  Corrected from the original design after empirically confirming the SDK never fails a task on
  its own (an uncaught exception becomes `Completed`+`isError:true`), and DebugMcp's qualifying
  tools already catch every failure into that structured JSON before returning. Never left
  hanging either way — see `tasks.md` T030/T038.
- Handles do not survive a process restart. An enquiry naming a handle from a previous process
  returns not-found, never a stale result.

---

## 4. Progress update

Advisory and fire-and-forget. Carries no data the final result does not also carry.

| Field | Type | Notes |
|---|---|---|
| `Stage` | `string` | Human-readable, e.g. `"acquiring engine"`, `"running inspection"`. |
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

**Corrected against the actual implementation** (an earlier draft claimed sub-stages —
`restoring`, `building solution`, `attaching`, `resolving symbols` — that turned out not to be
safely or honestly observable; see the note below the table):

| Tool | Stages | Countable? |
|---|---|---|
| `resharper_inspect_solution` | acquiring engine → running inspection → parsing report | no — engine acquisition and the `jb inspectcode` run are each one opaque child-process call; heartbeats (rule 3) carry liveness during both |
| `resharper_inspect_project` | acquiring engine → running inspection → parsing report | same as above |
| `batch_evaluate` | experiment triggered *n* of *m* | yes — corrected from "evaluating expression n of m": experiments trigger reactively as the debuggee's breakpoints fire (`BatchRunner.RunAsync`'s `allTriggeredCount`), not in an evaluation loop |
| `debug_launch` | starting process → ready | no — heartbeat carries liveness; see the note below |
| `code_load` | loading workspace, project *n* of ? | count only, no total — `MSBuildWorkspace.OpenSolutionAsync`/`OpenProjectAsync` accept a real `IProgress<ProjectLoadProgress>`, each reported project increments `Completed`, but `Total` stays null: a `.csproj`'s transitive project graph isn't known before the load finishes |

**Why `resharper_inspect_*` collapsed from 5 stages to 3**: `restoring`, `building solution` /
`building project`, and `inspecting` all happen *inside* the single `jb inspectcode` child-process
call (`ReSharperCliRunner.RunInspectCodeAsync`). Distinguishing them would require parsing that
process's stdout for phase-marker text whose exact wording was never verified against a live run —
fabricating named sub-stages we cannot actually observe would misrepresent what happened, which is
worse than reporting one honest "running inspection" stage with a heartbeat. `parsing report`
remains a real, separately-observable step (`IInspectionReportParser.Parse`, after the process
exits).

**Why `debug_launch` collapsed from 4 stages to 2**: `attaching` and `resolving symbols` happen
inside `ProcessDebugger.LaunchAsync`, the ICorDebug-callback-driven core the project's own
lock-ordering invariant protects (research.md R7). Instrumenting mid-flight progress there means a
progress call site inside code that runs partly on the ICorDebug callback thread — exactly where
R7 forbids introducing new `await` points around `_lock`/`_stateLock`. The honest, safe stages are
what `DebugSessionManager.LaunchAsync` can observe from outside that boundary — it holds no lock
across its single `await _processDebugger.LaunchAsync(...)` call, so wrapping *that* call is safe:
before and after. A heartbeat during the (usually fast, occasionally symbol-server-bound) wait
satisfies SC-001's 60-second ceiling without touching ICorDebug callback code.

**Why `code_load` dropped `locating MSBuild`**: `MSBuildLocator.RegisterDefaults()` runs once, in
`CodeAnalysisService`'s **static** constructor — triggered by the CLR before the singleton's first
instance is constructed, not inside `LoadAsync`. There is no per-call phase to report; the
`code_load` stage sequence is `loading workspace, project n of m` only.

---

## 5. Ranked suspect

Output of deterministic enrichment. **Strictly additive** — every field present before this
feature remains (FR-025). Enrichment applies to **frame-bearing** results only —
`RankedSuspect.FrameIndex` references a frame already present in the raw result, so a result
with no frames (e.g. `collection_analyze`, `object_summarize`, which summarize a single
object/collection rather than a call stack) has nothing for this model to reference. T069
descoped applying it there for exactly this reason; see tasks.md.

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
