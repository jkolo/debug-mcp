# debug-mcp — QA Tool Sweep (Bug Backlog)

**Started**: 2026-06-15
**Branch**: main (post 8e54cc0 / c2eb98a)
**Method**: each tool driven end-to-end through the real MCP server over stdio (JSON-RPC),
freshly built `DebugMcp/bin/Debug/net10.0/DebugMcp.dll`, against the sample debuggees
`tests/DebugTestApp` (long-running loop, rich `this`) and `tests/TestTargetApp` (command-driven
scenarios: collections, nested, recurse, threads, async, exception, object, deep, expressions).
Multiple scenarios per tool: happy path, edge cases, invalid input, error contract.

A "bug" = crash/hang, wrong data, contract inconsistency, misleading error, or behaviour that
would surprise an AI consumer. Severity: **P1** blocks normal use · **P2** wrong/misleading but
workable · **P3** polish/contract nit.

---

## Progress

| Group | Tools | Status | Bugs |
|-------|-------|--------|------|
| Contract/surface | tools/list, resources/list, prompts/list, no-session errors | ✅ | 0 |
| Session | debug_launch, debug_attach, debug_disconnect, debug_pause, debug_continue, debug_step | ✅ | 4 (1×P2, 3×P3) |
| Breakpoints | breakpoint_set, breakpoint_enable, breakpoint_remove, breakpoint_set_exception, tracepoint_set | ✅ | 5 (2×P2, 3×P3) |
| Inspection | variables_get, evaluate, evaluate_safe, object_inspect, members_get, object_summarize, collection_analyze, references_get, stacktrace_get, exception_get_context | ✅ | 3 (1×P1, 1×P2, 1×P3) |
| Memory/modules | memory_read, layout_get, modules_search, types_get | ✅ | 3 (1×P2, 2×P3) |
| Code analysis | code_load, code_get_diagnostics, code_find_usages, code_goto_definition, code_find_assignments | ✅ | 0 |
| Snapshots | snapshot_create, snapshot_diff, snapshot_delete | ✅ | 0 |
| Batch | batch_evaluate | ✅ | 1 (1×P2) |
| Timeline | timeline_query | ✅ | 0 |
| Process I/O | process_read_output, process_write_input | ✅ | 0 |
| ReSharper | resharper_inspect_solution, resharper_inspect_project | ✅ | 0 new (1 fixed: e187f20) |
| Resources | session, breakpoints, threads, modules, snapshots, timeline, source | ✅ | 0 |
| Prompts | diagnose_exception, find_bug_source, inspect_runtime_state, trace_data_flow | ✅ | 0 |

Legend: ⏳ pending · 🔬 testing · ✅ done.

---

## Bugs

> Newest groups appended below as testing proceeds.

<!-- BUG ENTRIES APPENDED HERE -->

### Group: Contract/surface — ✅ done, 0 bugs

**Tested**: `tools/list` (39 tools — all have description, annotations, annotation.title),
`resources/list` (6 static resources) + `resources/templates/list` (`debugger://source/{+file}`),
`prompts/list` (4) + `prompts/get` for all four, no-session error contract for 9 session tools.

**Observations (not bugs)**:
- All session tools return `{success:false, error:{code:"NO_SESSION"}}` when no session is
  active — consistent envelope. ✅
- `debug_disconnect` with no active session returns `{success:true, state, message,
  previousSession}` (idempotent no-op) rather than an error — acceptable.


### Group: Session — ✅ done, 4 bugs

**Tested**: `debug_launch` (invalid path → INVALID_PATH; valid → paused w/ full session fields;
double launch → ALREADY_ATTACHED), `debug_attach` (bad pid → PROCESS_NOT_FOUND; attach to a
separately-launched .NET process → ok, mode=attach), `debug_disconnect` (terminate true/false),
`debug_continue` (ok; while running → NOT_PAUSED), `debug_pause` (ok; idempotent while paused),
`debug_step` (in/over/out at entry **and** at a user breakpoint; invalid mode → INVALID_PARAMETER).

Working correctly: launch/attach/disconnect/continue, error contracts, step *control* (process
does advance and re-pause — confirmed via stacktrace_get).

#### BUG-001 [P2] `debug_step` returns an unresolved source location
The step succeeds and the debuggee re-pauses at a valid, resolvable source line, but the
location in `debug_step`'s own response is unresolved: `file:"Unknown"`, `line:0`,
`functionName:"0x0600001D"` (a raw metadata token). An on-demand `stacktrace_get` at the same
paused position resolves it correctly — so the data is available; only the step-complete
location resolver is broken.
- **Repro**:
  1. `debug_launch {program:".../DebugTestApp.dll", stopAtEntry:true}`
  2. `breakpoint_set {file:".../tests/DebugTestApp/Program.cs", line:77}`
  3. `debug_continue {}` → wait `debugger/breakpointHit`
  4. `debug_step {mode:"over"}`
- **Expected**: `session.location` = `{file:".../Program.cs", line:78, functionName:"ExecuteWorkItem"}`.
- **Actual**: `session.location` = `{file:"Unknown", line:0, functionName:"0x0600001D"}`.
- **Evidence**: immediately after, `stacktrace_get {max_frames:1}` → top frame
  `function:"Application.ExecuteWorkItem"`, `location.line:77`. So the IP is resolvable.
- **Hypothesis**: the `StepCompleted` location resolution path (ProcessDebugger) differs from
  `GetStackFrames` — it doesn't map the IP/metadata token to the PDB sequence point. Fix by
  reusing the stack-frame location resolution for the step-complete location.

#### BUG-002 [P3] `debug_step` fails with STEP_FAILED at the entry-point pause
Stepping immediately after `debug_launch {stopAtEntry:true}` (i.e. at the entry pause, before
any `debug_continue`) returns `{success:false, error:{code:"STEP_FAILED"}}` for all three modes
(`in`/`over`/`out`). Stepping works once stopped at a normal user breakpoint, so this is
specific to the entry pause.
- **Repro**: `debug_launch {stopAtEntry:true}` → `debug_step {mode:"over"}` → STEP_FAILED.
- **Expected**: either a successful step into user code, or a clear, specific error explaining
  stepping isn't available at the raw entry point.
- **Actual**: generic `STEP_FAILED` with no guidance.

#### BUG-003 [P3] `stacktrace_get` frame `location.function` is a raw metadata token
Each frame has a correct top-level `function` (e.g. `"Application.ExecuteWorkItem"`), but the
nested `location.function` is a raw token (e.g. `"0x0600001D"`) instead of the method name (or
omission). Misleading/duplicated field.
- **Repro**: at any breakpoint, `stacktrace_get {}` → inspect `frames[0].location.function`.
- **Expected**: method name or field omitted.
- **Actual**: `"0x0600001D"`.

#### BUG-004 [P3] `debug_pause` response shape inconsistent with continue/step
`debug_pause` returns `{success, state, threads:[{id}, ...]}` whereas `debug_continue` and
`debug_step` return a `session` object (`processId/state/pauseReason/location/...`). The
`threads` entries carry only `id` (no name/state/location). An AI consumer expecting the same
`session` shape across pause/continue/step gets a different envelope.
- **Repro**: while running, `debug_pause {}` → inspect response.
- **Expected**: a `session` object consistent with `debug_continue`/`debug_step`.
- **Actual**: `{success:true, state:"paused", threads:[{id:1679518}, ...]}`.

### Group: Breakpoints — ✅ done, 5 bugs

**Tested**: `breakpoint_set` (valid→binds+hits, hitCount via resource; duplicate→deduped to 1;
invalid file; out-of-range line; blank line), `breakpoint_enable` (disable→no hit; re-enable→hit),
`breakpoint_remove` (→no hit), `breakpoint_set_exception` (valid→pauses, verified deterministically
3/3; bogus type), `tracepoint_set` (non-blocking: 3 hits while process stays Running ✅).

Working correctly: enable/disable/remove, hit counting, exception breakpoints (pause + reach),
tracepoint *non-blocking* delivery, duplicate dedup (2× same line → 1 breakpoint).

#### BUG-005 [P2] `breakpoint_set` silently "succeeds" on invalid file / out-of-range line
A breakpoint in a non-existent file, or at a line far beyond the file's end, returns
`{success:true, breakpoint:{state:"pending", verified:false, message:"Module not loaded;
breakpoint will bind when module loads"}}`. No validation, and the tool's own
nearest-valid-line capability is never exercised. An AI agent cannot distinguish a typo'd
path/line from a legitimately deferred breakpoint.
- **Repro** (session active, debuggee paused at entry):
  1. `breakpoint_set {file:"/no/such/File.cs", line:10}` → `success:true`, state `pending`.
  2. `breakpoint_set {file:".../tests/DebugTestApp/Program.cs", line:99999}` → `success:true`, state `pending`.
- **Expected**: non-existent file → error or explicit "path not part of any known module";
  out-of-range line in a real, loaded file → `INVALID_LINE` with `nearestValidLine`.
- **Actual**: both `success:true` with a generic pending breakpoint.

#### BUG-006 [P3] "Module not loaded" pending message is inaccurate when the module IS loaded
At the entry pause, setting a breakpoint in the debuggee's own source (`DebugTestApp.dll`,
which is loaded — we are paused in it) yields `state:"pending"`, message "Module not loaded;
breakpoint will bind when module loads". (It does later bind and hit.) The message misleads and
suggests resolution isn't attempted against already-loaded modules at set time.
- **Repro**: `debug_launch {stopAtEntry:true}` → `breakpoint_set {file:"<debuggee Program.cs>", line:81}` → inspect `message`/`state`.

#### BUG-007 [P2] Tracepoint `{expression}` interpolation is broken (only literal text renders)
Every `{expression}` placeholder in a tracepoint log message fails to evaluate, even though the
identical expressions resolve fine through the `evaluate` tool at the same location. Only
literal text passes through.
- **Repro**:
  1. `debug_launch {program:".../DebugTestApp.dll", stopAtEntry:true}`
  2. `tracepoint_set {file:".../Program.cs", line:81, log_message:"r={result} s={status} prio={item.Priority} user={this._currentUser.Name} const=hi", max_notifications:2}`
  3. `debug_continue {}` → read the `debugger/breakpointHit` (tracepoint) notification's `params.logMessage`.
- **Expected**: `"r=300 s=High prio=4 user=John const=hi"` (values interpolated).
- **Actual**: `"r=<error: syntax_error> s=<error: syntax_error> prio=<error: eval_exception> user=<error: eval_exception> const=hi"`.
- **Cross-check**: at the same line, `evaluate {expression:"item.Priority"}` and
  `evaluate {expression:"this._currentUser.Name"}` succeed; `variables_get` lists `result`,
  `status`, `item`. So the values are inspectable — the tracepoint's log-message evaluator runs
  with a wrong/missing frame-thread context. Simple identifiers fail at parse (`syntax_error`),
  member access fails at eval (`eval_exception`).

#### BUG-008 [P3] `breakpoint_set_exception` accepts a non-existent exception type
`breakpoint_set_exception {exception_type:"NotARealType123"}` returns `success:true`. No
validation that the type resolves; the breakpoint can never match.
- **Expected**: validation warning/error, or at least a note that the type was not found.
- **Actual**: `success:true`.

#### BUG-009 [P3] `debugger://breakpoints` resource shape differs from `breakpoint_set`
The resource returns flat entries `{file, line, state:"Pending", type:"Breakpoint", ...}`
(PascalCase state/type, top-level file/line), while `breakpoint_set` returns
`{location:{file,line}, state:"pending", ...}` (nested location, lower-case state). Same domain
object, two shapes — an AI consumer must special-case each. Also `breakpoint_set`'s `duplicate`
flag stays `null` even when the registry dedupes a repeated location.

### Group: Inspection — ✅ done, 3 bugs

**Tested**: `variables_get` (all/locals/arguments scopes — correct), `evaluate` (arithmetic,
literals, locals, args, member chains, method calls, indexers, bad names), `evaluate_safe`
(pure vs method-call gate), `object_inspect` (valid + bad ref → INVALID_REFERENCE),
`members_get` (Person, System.String), `object_summarize`, `collection_analyze` (List + Dict),
`references_get`, `stacktrace_get` (full + paged), `exception_get_context` (Group 3, works 3/3).

Working correctly: `variables_get`, `object_inspect`, `members_get`, `object_summarize`,
`references_get`, `stacktrace_get` (frames resolve), `collection_analyze` core (count/kind/type),
`evaluate_safe` safety gate (method calls → `safe_eval_rejected`), and `evaluate` for dotted
member/property paths rooted at `this` or an argument (`item.Priority`→0,
`this._currentUser.Name`→"John", `this._settings.Count`→3, `this._currentUser.Tags.Count`→2).

#### BUG-010 [P1] `evaluate` does not implement most of its documented capabilities
The tool description states: *"Supports property access, method calls, LINQ, string
interpolation, arithmetic. Examples: 'myList.Count', 'customer.Name.ToUpper()', 'x + y * 2'."*
In practice `evaluate` is only a **dotted member-access path resolver**; everything else fails.
- **Repro** (paused at `tests/DebugTestApp/Program.cs:81`):
  | expression | result |
  |---|---|
  | `1+1` | `syntax_error` "Unrecognized expression: 1+1" |
  | `2+3*4` | `syntax_error` |
  | `x + y * 2` (doc example) | `syntax_error` |
  | `true` | `syntax_error` |
  | `"abc".Length` | `syntax_error` |
  | `this._currentUser.Name.ToUpper()` (≈doc example `customer.Name.ToUpper()`) | `variable_unavailable` |
  | `this._currentUser.Tags[0]` (indexer) | `variable_unavailable` |
  | `item.Priority+1` | `variable_unavailable` "Cannot access member 'Priority+1' on path 'item'" |
  | `this._currentUser.Tags.Count` (pure path) | ✅ `2` |
- **Expected**: arithmetic/method/indexer/LINQ per the description (or a description that matches
  reality + a clear `NOT_SUPPORTED` error for unsupported forms).
- **Actual**: parser rejects anything that isn't `a.b.c`; the tool's own example expressions fail.
- **Impact**: AI agents follow the description and get failures; conditions/log-messages that
  use operators or calls won't work either (see BUG-007).

#### BUG-011 [P2] `evaluate` / `evaluate_safe` cannot resolve local variables by name
A bare local that `variables_get` lists with a value cannot be evaluated by name, while an
*argument* with the same single-identifier form works.
- **Repro** (paused at Program.cs:81; `variables_get {scope:"locals"}` → `result`, `status`):
  - `evaluate {expression:"result"}` → `syntax_error` "Unrecognized expression: result"
  - `evaluate {expression:"status"}` → `syntax_error`
  - `evaluate {expression:"item"}` (argument) → ✅ `{WorkItem}`
  - `evaluate_safe {expression:"result"}` → `syntax_error`
- **Expected**: locals resolvable by name (they are in scope and readable via `variables_get`).
- **Actual**: only arguments and `this`-rooted paths resolve as expression roots; locals → syntax_error.

#### BUG-012 [P3] `collection_analyze` previews empty + generic element type
`collection_analyze` on `this._currentUser.Tags` (a `List<string>` with 2 items) returns the
right `count:2`, `kind:"List"`, but `firstElements:[]`, `lastElements:[]` (empty despite
`max_preview_items:5`), `elementType:"System.Object"` (should be `System.String`), and
`typeDistribution:null`.
- **Repro**: paused at Program.cs:81 → `collection_analyze {expression:"this._currentUser.Tags"}`.
- **Expected**: `firstElements:["developer","tester"]`, `elementType:"System.String"`.
- **Actual**: empty previews, `elementType:"System.Object"`.

### Group: Memory/modules — ✅ done, 3 bugs

**Tested**: `memory_read` (null 0x0, non-hex, valid address from object_inspect),
`layout_get` (`WorkItem`, `System.Int32`, `Application`, bad type→TYPE_NOT_FOUND),
`modules_search` (types/methods/both; invalid search_type→INVALID_PARAMETER),
`types_get` (`DebugTestApp`→6 types; bad module→MODULE_NOT_FOUND).

Working correctly: `memory_read` for a valid address (16 bytes returned), `layout_get` offsets/
sizes (WorkItem totalSize 48, headerSize 16, per-field offset/size/alignment),
`modules_search` (types+methods, proper INVALID_PARAMETER on bad search_type), `types_get`.

#### BUG-013 [P2] `object_inspect` address has a double `0x` prefix (breaks round-trip to `memory_read`)
`object_inspect`'s `inspection.address` is formatted as `"0x0x7FA8768096C0"` — a literal `0x`
prepended to a value that already starts with `0x`. Feeding it straight into `memory_read`
fails/misreads; the agent must string-fix it first.
- **Repro**: at a breakpoint, `object_inspect {object_ref:"this._currentUser"}` → read
  `inspection.address` → `"0x0x..."`.
- **Expected**: `"0x7FA8768096C0"`.
- **Cross-check**: after stripping to a single `0x`, `memory_read {address:"0x7FA8768096C0", size:16}`
  reads 16 real bytes — so the address is otherwise valid; only the formatting is wrong.

#### BUG-014 [P3] `memory_read` reports `success:true` for a failed / null read
`memory_read {address:"0x0"}` returns `{success:true, memory:{actualSize:0, bytes:"",
error:"Partial read: 0 of 16 bytes"}}`. Top-level success is true while the data block carries
an `error` and zero bytes. (Also lenient: a malformed address tends to degrade to 0x0 rather
than erroring.)
- **Repro**: at a breakpoint, `memory_read {address:"0x0", size:16}`.
- **Expected**: `success:false` with `INVALID_ADDRESS`/`MEMORY_READ_FAILED`.
- **Actual**: `success:true` with an embedded `error` string and empty `bytes`.

#### BUG-015 [P3] `layout_get` reports every field `typeName` as "Unknown"
Field offsets/sizes/alignment are correct, but every field's `typeName` is `"Unknown"` — the
field types are never resolved.
- **Repro**: at a breakpoint, `layout_get {type_name:"WorkItem"}` → all four fields
  (`<Id>k__BackingField` Guid, `<Description>` string, `<CreatedAt>` DateTime, `<Priority>` int)
  show `typeName:"Unknown"`.
- **Expected**: resolved CLR type names (`System.Guid`, `System.String`, …).

### Group: Code analysis (Roslyn) — ✅ done, 0 bugs

**Tested** (no debug session; against `tests/ReSharperSampleApp/ReSharperSampleApp.sln`):
`code_get_diagnostics` before load → NO_WORKSPACE ✅; `code_load` → 1 project ✅;
`code_get_diagnostics` → 0 (clean — consistent with the RedundantCast being ReSharper-only);
`code_find_usages` (by FQN → 2 usages; by source location Calculator.cs:10:24 → 2, symbol
`Compute`); `code_goto_definition` (Program.cs call site → Calculator.cs:10:23 ✅);
`code_find_assignments` (by location Calculator.cs:12 → 1 ✅).

**Observations (not bugs)**:
- `code_find_usages {name:...}` requires the **fully-qualified** name
  (`ReSharperSampleApp.Calculator.Compute` works; `Compute` / `Calculator.Compute` →
  SYMBOL_NOT_FOUND). This matches the documented contract ("Fully qualified name"). Location-based
  lookup is the ergonomic path.
- `code_find_assignments {name:"value"}` (a local) → SYMBOL_NOT_FOUND; locals have no FQN, use
  the location form (works). Expected.

### Group: Snapshots — ✅ done, 0 bugs

**Tested** (paused at DebugTestApp Program.cs:81): `snapshot_create` depth 0 and depth 1;
diff across two iterations → `summary {added:0, removed:0, modified:1, unchanged:3}` with
`timeDelta` ✅; `snapshot_diff` invalid id → SNAPSHOT_NOT_FOUND; `debugger://snapshots` resource
count tracks creates/deletes (3→2→0); `snapshot_delete` one and all; `snapshot_create` while
running → NOT_PAUSED. All correct.

### Group: Batch — ✅ done, 1 bug

**Tested**: `batch_evaluate` with a trigger at DebugTestApp Program.cs:81, capture expressions,
`non_blocking` + `blocking` modes, `max_hits`; invalid JSON → `invalid_json`; empty array →
`validation_error`; on a paused-at-entry session.

Working correctly: experiment lifecycle (triggers, `completion_reason:"all_triggered"`,
`triggered` count, per-experiment `status`/`hit_count`/`hits`), input validation.

#### BUG-016 [P2] `batch_evaluate` never captures values — all captures error
The trigger fires and hits are recorded, but every captured expression fails: `values:{}` and
`eval_errors` filled with `timeout`/`eval_exception` — **in both `blocking` and `non_blocking`
modes** — for the very expressions that `evaluate` resolves at the same location. The tool's
core purpose (collecting values across hits) is non-functional.
- **Repro**:
  1. `debug_launch {program:".../DebugTestApp.dll"}` then `debug_continue {}` (running).
  2. `batch_evaluate {experiments:"[{\"trigger\":{\"file\":\".../Program.cs\",\"line\":81},\"mode\":\"blocking\",\"capture\":[\"this._currentUser.Name\",\"item.Priority\",\"this._settings.Count\"],\"max_hits\":1}]"}`
- **Expected**: `hits[0].values = {"this._currentUser.Name":"John","item.Priority":4,"this._settings.Count":3}`.
- **Actual**: `hits[0].values = {}`, `eval_errors = {"this._currentUser.Name":"timeout","item.Priority":"eval_exception","this._settings.Count":"eval_exception"}`.
- **Cross-check**: at the same line, `evaluate {expression:"this._currentUser.Name"}`→"John",
  `evaluate {expression:"item.Priority"}`→4. So the values are inspectable; the batch capture
  evaluator runs with a wrong/missing frame-thread context or an over-tight timeout.
- **Related**: same failure family as BUG-007 (tracepoint `{expression}` interpolation). Likely a
  shared root cause in the non-`evaluate`-tool evaluation path.

**Observation (P3)**: `batch_evaluate` on a session still paused at the entry point returns
`completion_reason:"timeout"`, `triggered:0` (it does not resume the process itself). Calling it
right after `debug_launch` (without `debug_continue`) silently yields zero hits — an explicit
"process not running" hint, or auto-resume, would help.

### Group: Timeline — ✅ done, 0 bugs

**Tested**: `timeline_query` after real activity (launch + breakpoint hit) → 13 events with
correct `eventType` distribution (session_started, module_loaded×9, thread_started,
stdout_written, breakpoint_hit); `eventTypes` JSON filter (`["breakpoint_hit"]`); bogus type →
empty; `maxEvents`; `fromEventId`. All correct.

**Observation (not a bug)**: on a session paused at the entry point with no execution yet,
`timeline_query` returns 0 events (nothing has happened) — expected.

### Group: Process I/O — ✅ done, 0 bugs

**Tested** (TestTargetApp command loop): `process_read_output` (`both`/`stdout`/`stderr`,
`clear:true`), `process_write_input` (commands `method`→`METHOD_RESULT:Hello, World!`,
unknown→`UNKNOWN_COMMAND:boguscmd`, `exit` with `close_after:true`→EOF). stdout/stderr capture,
clear semantics, and stdin delivery all correct.

**Observation (not a bug)**: after the debuggee exits via `exit`+`close_after`, `debugger://session`
no longer reports a `state` (session ended) — terminal handling is benign.

### Group: Resources — ✅ done, 0 bugs

**Tested** (paused at a breakpoint): read all six static resources (`session`, `breakpoints`,
`threads`, `modules`, `snapshots`, `timeline`) — all return well-formed payloads (threads now
populated, post c2eb98a fix); `debugger://source/<path>` returns file content for an
allowed/PDB-referenced source (2317 chars); `debugger://source//etc/passwd` is **blocked** (error)
— path access is correctly restricted to known source paths.

### Group: Prompts — ✅ done, 0 bugs

**Tested**: `prompts/get` for all four prompts (with and without optional args) → substantive
messages (1.0–1.3 KB) that reference concrete tools; required-arg enforcement
(`find_bug_source` with no args → error); unknown prompt → "Unknown prompt: '...'".

**Observation (P3, minor)**: a missing required prompt argument yields a generic "An error
occurred." rather than naming the missing argument.

### Group: ReSharper — ✅ done, 0 new bugs (1 already fixed)

Covered extensively in the preceding session: `resharper_inspect_solution` /
`resharper_inspect_project` happy paths, severity filtering, project scoping, `noBuild`, opt-out
(`--no-resharper` → tools absent), and error contract (`INVALID_PATH`, `PROJECT_NOT_FOUND`),
verified end-to-end against `tests/ReSharperSampleApp`. One bug found and **already fixed**:
finding `severity` serialized PascalCase vs lower-case `summary` keys → commit `e187f20`
(lower-case converter). No further issues.

---

## Summary

**16 bugs** across 39 tools / 7 resources / 4 prompts (+1 already fixed during the sweep).

| Severity | Count | IDs |
|----------|-------|-----|
| P1 | 1 | BUG-010 |
| P2 | 6 | BUG-001, 005, 007, 011, 013, 016 |
| P3 | 9 | BUG-002, 003, 004, 006, 008, 009, 012, 014, 015 |

Clean groups (no bugs): Contract/surface, Code analysis, Snapshots, Timeline, Process I/O,
Resources, Prompts. The debugger control plane (launch/attach/continue/pause/breakpoints/
snapshots/timeline/resources/code-analysis) is solid; the issues cluster in **expression
evaluation** and **location/type resolution + response-shape polish**.

### Likely shared root causes (fix these first → many bugs collapse)

1. **Non-`evaluate`-tool expression evaluation is broken** — BUG-007 (tracepoint `{expr}`) and
   BUG-016 (batch_evaluate captures) both fail (`timeout`/`eval_exception`/`syntax_error`) for
   expressions that the `evaluate` tool resolves fine. One shared evaluator/frame-context fix
   likely repairs both. **Highest leverage.**
2. **`evaluate` engine is a path-walker, not an expression evaluator** — BUG-010 (no arithmetic/
   methods/indexers/LINQ, contradicting its own docs) + BUG-011 (can't resolve locals). Either
   implement real evaluation or correct the description and add a clear `NOT_SUPPORTED` error.
3. **Step/location resolution** — BUG-001 (`debug_step` location Unknown/0/token) + BUG-003
   (frame `location.function` token). Reuse the working `GetStackFrames` resolver for the
   step-complete and location DTOs.
4. **`success:true` on failure / missing validation** — BUG-005 (bad breakpoint file/line),
   BUG-008 (bogus exception type), BUG-014 (`memory_read` null/failed read). Return proper errors.
5. **Response-shape & data-quality polish** — BUG-004 (debug_pause shape), BUG-009 (breakpoints
   resource vs tool shape), BUG-012 (collection previews/elementType), BUG-013 (address double
   `0x`), BUG-015 (layout field types "Unknown").

### Suggested fix order
BUG-010/007/016 (evaluation) → BUG-001/003 (location) → BUG-005/014/008 (validation) →
BUG-013/012/015 (data quality) → BUG-002/004/006/009 (polish).

---

## Resolution (2026-06-15, branch `035-qa-bugfixes`)

All 16 bugs fixed across three commits. Live-verified via an MCP stdio harness against
`DebugTestApp`; fast suite (Unit|Contract) stays green at 1388 passing.

| Bug | Status | How |
|-----|--------|-----|
| BUG-010 [P1] | ✅ fixed | New Roslyn tree-walking expression evaluator over ICorDebug (`ProcessDebugger.ExpressionEvaluator.cs`): arithmetic/comparison/logical/bitwise/conditional, member access, indexers, method calls, casts, interpolation. Lambdas/LINQ → clear `not_supported`. Verified 30 expressions. |
| BUG-007 [P2] | ✅ fixed | Root cause was BUG-011 (locals → syntax_error) + a too-short eval timeout, not a func-eval impossibility. Tracepoint `{expr}` now renders all placeholders (incl. property getters). Verified live. |
| BUG-011 [P2] | ✅ fixed | `TryGetLocalOrArgument` resolves locals by PDB name. |
| BUG-016 [P2] | ✅ fixed | Blocking batch captures deferred off the ICorDebug callback thread with a longer timeout (the original failures were func-eval timeouts, not a mid-callback impossibility). Verified across 2 hits; non-blocking capture also verified working. |
| BUG-001 [P2] | ✅ fixed | `GetCurrentLocationInfo` resolves file/line via PDB + type-qualified method name. |
| BUG-005 [P2] | ✅ fixed | Out-of-range line in a loaded file → `INVALID_LINE` (+nearestValidLine); unknown path stays pending. |
| BUG-013 [P2] | ✅ fixed | Force `ulong` so `CORDB_ADDRESS` isn't double-`0x`-prefixed. |
| BUG-003 [P3] | ✅ fixed | Frame `location.function` mirrors the resolved method name, never a token. |
| BUG-012 [P3] | ✅ fixed | `collection_analyze` previews populate (working indexers) + elementType inferred. |
| BUG-015 [P3] | ✅ fixed | `layout_get` parses field signature blobs (ECMA-335) for real type names. |
| BUG-014 [P3] | ✅ fixed | `memory_read` returns `MEMORY_READ_FAILED` on a zero-byte read. |
| BUG-004 [P3] | ✅ fixed | `debug_pause` returns a `session` envelope like continue/step. |
| BUG-009 [P3] | ✅ fixed | `debugger://breakpoints` matches `breakpoint_set` shape (nested location, lower-case state/type). |
| BUG-002 [P3] | ✅ fixed | Clear, actionable step error at the entry pause. |
| BUG-006 [P3] | ✅ fixed | Accurate pending message when the source isn't in a loaded module. |
| BUG-008 [P3] | ✅ fixed | `breakpoint_set_exception` returns a `note` that the type was not verified. |

Commits: `8be6263` (batch 1: addresses/locations/layout/shapes), `b10c22e` (BUG-010 evaluator),
`f8de8ae` (batch deferred capture, breakpoint validation, step message).
