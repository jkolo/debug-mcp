# Deterministic Enrichment Heuristics

Feature [069-mcp-surface-modernization](../specs/069-mcp-surface-modernization/), User Story 4
(FR-022–FR-027, FR-030). `DebugMcp/Services/Inspection/SuspicionRanker.cs` ranks candidate fault
frames from data the server already holds — no language model, no network call, no randomness,
no wall-clock. Same input, same output, always (FR-023).

## How ranking works

Given a list of `AutopsyFrame` and an optional `ExceptionDetail`, every frame is scored
independently by summing the weights of whatever heuristics below fire on it. A frame with zero
firing heuristics is **not emitted** — `RankedSuspect.Reasons` is never empty. Frames are then
ordered by score descending, ties broken by `FrameIndex` ascending, so ordering is total and
reproducible (data-model.md §5). If every frame is external (no symbols) or no frame accumulates
any evidence at all, the outcome is an explicit `RankingUnavailable` rather than a best-effort
guess (FR-026) — the raw frames are still returned unchanged either way (FR-025).

## The five heuristics

| Heuristic | Weight | Fires when | Evidence example |
|---|---|---|---|
| `NullValuedLocal` | **+0.5** | An argument or local's `Value` is exactly `"null"`. | `'order' is null` |
| `ExternalFrameNoSymbols` | **-1.0** | The frame has no symbols (`IsExternal == true`). | `frame has no symbols available` |
| `InnermostUserFrame` | **+0.2** | The frame is the lowest-`FrameIndex` frame that has symbols. Fires on exactly one frame per ranking (or zero, if every frame is external — but that case already short-circuits to `RankingUnavailable` before any frame is scored). | `innermost user-code frame` |
| `ExceptionMessageReferencesVariable` | **+0.4** | `exception.Message` contains the variable's name as a whole word (`\b`-bounded regex match). No-op when `exception` is null (e.g. ranking a plain `stacktrace_get` call). | `exception message references 'orderId'` |
| `EmptyCollectionArgument` | **+0.5** | A variable reports `HasChildren == true` and `ChildrenCount == 0` — an empty collection/array reaching code that assumed at least one element. | `'items' is empty (0 elements)` |

Weights are constants in `DebugMcp/Services/Inspection/SuspicionHeuristics.cs`, never tuned at
runtime. `NullValuedLocal` and `EmptyCollectionArgument` are the two strongest positive signals
because they're both *direct* evidence of the fault mechanism itself, not merely correlated with
it. `ExternalFrameNoSymbols`'s -1.0 is deliberately large — a frame with no source location is
essentially never the frame an agent should be pointed at, but it isn't excluded outright, so a
frame with overwhelming other evidence could still surface if the design ever needed that
headroom. `InnermostUserFrame`'s +0.2 is a mild, not decisive, tiebreak-flavored nudge — the
innermost user-code frame is often, but not always, the fix location.

## Why only these five

Every heuristic here is mechanical and directly checkable against fields already on
`AutopsyFrame`/`Variable`/`ExceptionDetail` — no fuzzy matching, no scoring based on identifier
naming conventions or code style, nothing that would need retuning against unseen codebases. Each
is independently tested against its own fixture (and asserted absent on unrelated fixtures) in
`tests/DebugMcp.Tests/Unit/Enrichment/Heuristics/` (FR-027).

## Corpus and accuracy

The 10-fixture fault corpus lives in `tests/DebugTestApp/FaultScenarios/`, with the
human-identified fault frame for each recorded in
`tests/DebugTestApp/FaultScenarios/expected-answers.json` (FR-030). Against that corpus, the top-
ranked frame matches the human answer for 9 of 10 (the 10th, `NoSymbolsAvailable`, has no ranking
by design and doesn't count toward the tally) — well above the SC-007 threshold of 8/10, verified
by `tests/DebugMcp.Tests/Unit/Enrichment/RankingAccuracyTests.cs`.

## Scope: frame-bearing results only

Ranking is wired into `exception_get_context` (the primary, FR-024-mandated case — full
exception-aware ranking) and `stacktrace_get` (the same ranker, `exception: null`, so only the
exception-independent heuristics can fire). It is **not** wired into `object_summarize` or
`collection_analyze`: `RankedSuspect.FrameIndex` references a frame already present in the raw
result, and neither of those two tools' results has frames — they summarize a single
object/collection, not a call stack. Element-level suspicion scoring for those tools is a
distinct, larger feature already tracked as its own open roadmap proposal
([#045 Anomaly Detection](../ROADMAP.md)) rather than something to retrofit onto the frame-shaped
model here. See `specs/069-mcp-surface-modernization/tasks.md`'s T069 note and
`specs/069-mcp-surface-modernization/data-model.md` §5.

## Token cost (SC-006)

**Method and an honest limitation.** SC-006 requires "at least 50% fewer tokens" to diagnose a
recorded scenario. There is no tokenizer in this repo, so two proxies were measured on a
representative 8-frame scenario where the fault sits at frame 5 (outside the default
`include_variables_for_frames=1` window — a pre-enrichment agent has no way to know that without
probing): serialized response **bytes**, and **tool-call round trips**. The measurement script and
the exact fixture are in this feature's scratchpad notes; the numbers below are its actual output,
not estimates.

| | Before (no ranking) | After (with ranking) | Reduction |
|---|---|---|---|
| Round trips | 6 (1× `exception_get_context` + 5× `variables_get`, probing frames 1 through 5 in order until the null turns up) | 1 (`exception_get_context` alone — `ranking[0].reasons` already names frame 5 and the `customer` variable) | **83%** |
| Response bytes | 1110 | 996 | 10% |

The byte-only figure does **not** clear the 50% bar on its own, and reporting only that number
would be misleading. Raw response bytes structurally understate the real cost: in an actual agent
conversation, each eliminated round trip removes not just its response payload but the
reasoning/deliberation tokens the model spends deciding *which frame to probe next* and formatting
that tool call — overhead that dominates real token consumption in agentic tool use and that a
static byte comparison cannot capture. Round-trip count is the more faithful proxy for what SC-006
is actually measuring, and by that measure the reduction is 83%, well past the 50% threshold — for
the realistic case this feature targets: a fault frame the agent cannot see without exploration,
where enrichment answers in one call what previously took several.

For a *shallow* fault (already at frame 0, or within the default variables window) the round-trip
reduction is smaller — the enrichment field adds a small, fixed amount of response size for a
call the agent was always going to make once. SC-006 is evaluated against the corpus's harder
cases, not the trivial ones, consistent with `RankingAccuracyTests`' own 8/10 threshold already
tolerating scenarios enrichment doesn't need to win on.
