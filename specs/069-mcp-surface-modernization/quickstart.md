# Quickstart: Validating MCP Surface Modernization

**Feature**: 069-mcp-surface-modernization

How to prove each slice actually works. Unit tests cover the logic; the scenarios here cover what
unit tests structurally cannot — **wire-level behaviour**, which was the exact gap that let a
version mismatch slip through during the SDK 2.2.0 upgrade.

## Prerequisites

```bash
dotnet build                      # must be 0 errors, 0 warnings
```

## Baseline — the fast checks

```bash
# Unit + contract only. Integration and performance tests are flaky by
# design and are not part of this loop.
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
```

Expected: all green. The contract suite additionally fails the build if any tool lacks an
`outputSchema`, emits a result that violates its own schema, or drifts out of sync with
`website/docs/tools/*.md` (FR-020).

---

## Driving the server over stdio

Every scenario below speaks JSON-RPC to the server's stdin and reads its stdout.

```bash
dotnet run --project DebugMcp --no-build
```

Three things bite anyone writing this harness for the first time:

- **Filter responses by `id`.** The server interleaves `notifications/message` lines between
  request and response. Reading "the next line" gets a log notification, not the answer.
- **Use a real protocol version.** The `initialize` handshake accepts `2024-11-05`,
  `2025-03-26`, `2025-06-18`, `2025-11-25`. It does **not** accept `2026-07-28` — that is the
  specification revision date, not a wire protocol version negotiable via `initialize`, and
  sending it there returns `-32022`.
- **Scenario 2 needs a second, handshake-free session.** The Tasks extension's per-request
  opt-in (`_meta."io.modelcontextprotocol/clientCapabilities".extensions`) is a
  2026-07-28-era (SEP-2575) mechanism: the SDK unconditionally rejects that reserved `_meta`
  key (`-32600`) under every protocol version `initialize` can negotiate, and never backfills
  the per-request client-capabilities check from what was declared at `initialize`. The only
  way to reach it is to skip `initialize` entirely and carry the full `_meta` trio —
  `io.modelcontextprotocol/protocolVersion: "2026-07-28"`, `.../clientInfo`,
  `.../clientCapabilities` — on every request of that session, per SEP-2575's handshake-free
  connection mode. Confirmed by decompiling `ModelContextProtocol.Core`/`.Extensions.Tasks`
  2.2.0 (`McpSessionHandler.ValidateRequiredPerRequestMetadata`,
  `PopulateContextFromMeta`, `HasTaskExtensionOptIn`).

---

## Scenario 1 — Progress and cancellation (Story 1, P1)

**Setup**: a solution that takes long enough to observe. First run also exercises engine
acquisition.

**Steps**

1. Handshake, then call `resharper_inspect_solution` with `_meta.progressToken` set.
2. Collect every `notifications/progress` line until the result arrives.
3. Repeat the call **without** a progress token.
4. Repeat once more with a token, and cancel the request part-way through.

**Expected**

| # | Outcome |
|---|---|
| 1 | First `notifications/progress` within **5 s**; a notification on every stage change; never more than **60 s** of silence; stages ordered as in the [progress contract](./contracts/progress-contract.md) |
| 2 | Identical result, zero notifications, no error — SC-009 |
| 3 | Server stops the work; the next tool call succeeds within **5 s** and the debug session is still usable — SC-002 |

---

## Scenario 2 — Deferred results (Story 2, P2)

**Steps**

1. Call `resharper_inspect_solution` **with** the tasks extension declared in
   `_meta."io.modelcontextprotocol/clientCapabilities".extensions`.
2. Poll `tasks/get` at the returned `pollIntervalMs` until terminal.
3. Call the same tool **without** declaring the extension.
4. Diff the payload from step 2 against the payload from step 3.
5. Start another task and immediately `tasks/cancel` it.
6. Ask `tasks/get` about a fabricated id, and about a real id after its TTL elapsed.
7. Call a **non-qualifying** tool (e.g. `variables_get`) with the extension declared.

**Expected**

| # | Outcome |
|---|---|
| 1 | `resultType: "task"` within **1 s**, carrying `taskId`, `status: "working"`, `ttlMs`, `pollIntervalMs` |
| 2 | `status` reaches `completed`, `result` present |
| 3 | Ordinary blocking result — **never** a handle (FR-008) |
| 4 | **Byte-identical** (FR-014) — the strongest single assertion in this slice |
| 5 | Cancellation acknowledged; task reaches a terminal status |
| 6 | Two **distinguishable** errors: not-found vs expired (FR-012) |
| 7 | Ordinary blocking result — the 34 non-qualifying tools are pinned to `McpTaskExecutionMode.Synchronous` via `TaskExecutionPolicy.GetMode` (FR-013) |

Step 7 is the one most likely to fail: the SDK's own default execution-mode selector treats
every tool as task-capable (`Optional`), so forgetting the pin silently makes every tool
task-eligible. (There is no `TaskSupport` enum in SDK 2.2.0 — an earlier draft of this doc named
one that never shipped; `TaskExecutionPolicy.cs`'s own doc comment explains the actual
mechanism.)

---

## Scenario 3 — Result contracts (Story 3, P3)

**Steps**

1. `tools/list`; assert all 39 entries carry an `outputSchema`.
2. Call each tool; validate `structuredContent` against that tool's schema.
3. Confirm `content[0]` is a text block carrying the same data serialized.
4. Trigger a failure (e.g. `variables_get` with `frame_index: -1`).
5. Read only `content[0].text` and parse it as JSON, ignoring `structuredContent` entirely — the
   way every client behaves today.

**Expected**

| # | Outcome |
|---|---|
| 1 | 39 of 39 — FR-016, SC-004 |
| 2 | 100% conform. The spec makes this mandatory when an `outputSchema` is published |
| 3 | Present — FR-017, and the mechanism by which SC-009 holds |
| 4 | `isError: true`, plus `success: false` with a `code` from the documented `ErrorCodes` set — FR-018, FR-019, SC-005 |
| 5 | Works unchanged, same field names, same meanings — FR-021, SC-009 |

Step 5 is the backward-compatibility proof. If it needs any client change, the slice has failed
regardless of what the other steps show.

---

## Scenario 4 — Deterministic enrichment (Story 4, P4)

**Setup**: the fault-scenario corpus required by FR-030 — at least 10 **deterministic fixture
programs** in `tests/DebugTestApp/FaultScenarios/`, each paired with the frame a human identifies
as the fault site. Coverage must include a null dereference, a fault in a nested call chain, a
fault across an async boundary, an aggregate/inner exception, and one with symbols deliberately
unavailable.

There is no record-and-replay in debug-mcp. "Running a scenario" means launching that fixture
under the debugger again — not restoring a recording. Consequently, comparisons are made on the
**normalized enrichment output** (`FrameIndex`, `Score`, `Heuristic`, `Weight`, `Evidence`,
ordering); addresses, thread IDs, PIDs and durations vary between runs by nature and are excluded.
See [data-model.md](./data-model.md#what-determinism-means-here-precisely).

**Steps**

1. Run each fixture to its fault; call `exception_get_context`.
2. Compare the top-ranked frame against the corpus's recorded human answer.
3. Run one fixture **10 times**; compare the normalized enrichment output across all 10.
4. Measure tokens for a full diagnosis, before vs after.
5. Run the no-symbols fixture.

**Expected**

| # | Outcome |
|---|---|
| 2 | Fault site ranks first in **at least 8 of 10** — SC-007 |
| 3 | Normalized enrichment output **identical** all 10 times — SC-008, FR-023 |
| 4 | At least **50%** fewer tokens — SC-006 |
| 5 | Explicit `RankingUnavailable` with a reason, raw data still present, call still succeeds — FR-026 |

If step 3 fails, suspect the fixture before the heuristics: an unseeded random source,
wall-clock branching or racing threads inside the fixture makes it unfit for the corpus.

Every raw field available before this feature must still be present in all five steps (FR-025).

---

## Scenario 5 — Per-call timeouts (Story 5, P5)

**Steps**

1. `tools/list`; for every tool classified as blocking, assert an optional timeout parameter is
   present and its default documented. Assert non-blocking tools do **not** carry one.
2. Invoke a blocking tool with a timeout deliberately shorter than the work requires.
3. Invoke the same tool with no timeout supplied.
4. Invoke `resharper_inspect_solution` with no timeout supplied.
5. Trigger a timeout during an indivisible runtime step.

**Expected**

| # | Outcome |
|---|---|
| 1 | Every blocking tool has one, no non-blocking tool does — FR-031, SC-011 |
| 2 | Timeout error naming the elapsed budget, returned within the budget plus one indivisible step; the next call succeeds — FR-033, SC-012 |
| 3 | The 30-second default applies — FR-032 |
| 4 | Its own **longer** documented default applies, **not** 30 seconds. A 30-second default here would break a tool that routinely runs for minutes — FR-032 |
| 5 | The step completes, then the timeout error returns. Debuggee state consistent, session usable — FR-034 |

Step 4 is the one that distinguishes real compliance from compliance-by-regression.

---

## Recording SC-003

While running Scenario 2, record the **longest single request/response exchange** for each of the
five qualifying operations. SC-003 is satisfied when no exchange exceeds the handle's suggested
polling interval — that is what demonstrates the structural cause of client-timeout failures has
been removed, rather than merely observing that none happened to occur.

---

## Final gate

```bash
dotnet build                    # 0 errors, 0 warnings — SC-010
dotnet build -c Release
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
```

Plus the housekeeping in FR-028/FR-029: `ROADMAP.md` has one `Tier 4` heading, no duplicate `034`,
`031`/`032` no longer listed as both done and proposed, and `#061`/`#062`/`#066` marked absorbed
with `#037` marked partially absorbed.
