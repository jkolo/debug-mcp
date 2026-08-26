# Feature Specification: MCP Surface Modernization

**Feature Branch**: `069-mcp-surface-modernization`

**Created**: 2026-08-25

**Status**: Draft

**Input**: User description: "Review the tools, resources and prompts exposed by debug-mcp and consider refactoring toward a more modern approach with asynchrony and agents that get model access and return already-processed results."

## Overview

debug-mcp exposes 39 tools, 7 resources and 4 prompts. The surface works, but it was built
incrementally across 34 features and four areas have drifted behind both the MCP
specification and the project's own constitution:

1. **Long operations are black boxes.** `resharper_inspect_solution` on first use downloads a
   180–650 MB engine and then builds the solution. The client sends one call and receives
   nothing — no progress, no way to cancel — until it either returns or the client times out.
   The constitution (Principle II) already requires that *"long-running operations MUST support
   progress reporting or timeout mechanisms"*. Today no tool reports progress at all.

2. **Results are untyped text.** Every tool hand-assembles a JSON string and returns it as a
   text block. Clients cannot know a result's shape before calling, cannot validate what came
   back, and must parse a string that the server promises — but does not guarantee — is JSON.
   The constitution requires *"structured JSON suitable for AI consumption"*; the wire format
   currently delivers a string that happens to contain JSON.

3. **Results are raw, not prioritized.** `exception_get_context` returns every frame with every
   local. The consuming agent burns context window re-deriving which frame matters — work the
   server can do deterministically, because it is the side holding the debuggee.

4. **Blocking operations cannot be bounded.** The constitution's tool standards require that
   *"all blocking operations MUST accept optional timeout (default: 30s)"*. Roughly a third of the
   tools do; the rest offer no way to bound a call at all. An agent that issues a call has no
   recourse but to wait or abandon the connection.

The original framing of this work proposed *"agents that get model access"*. That framing was
investigated and rejected on evidence: the MCP mechanism for a server to reach a model —
**Sampling** — was deprecated by [SEP-2577](https://modelcontextprotocol.io/seps/2577-deprecate-roots-sampling-and-logging)
(status: Final), part of the same `2026-07-28` specification revision this project adopted with
MCP SDK 2.2.0. The SEP cites low client adoption, implementation complexity, and names Sampling
*"the most security-sensitive of the three… attack surface for prompt injection and data
exfiltration"*. Building a new capability on a deprecated, security-flagged mechanism was
rejected.

The intent behind that framing is preserved without the model: the server pre-processes results
**deterministically**. Ranking, correlating and trimming are computed from data the server
already has — debuggee state, PDB symbols, the Roslyn workspace. This keeps every result
reproducible and unit-testable, requires no API key, and adds no per-call cost or latency.

This feature therefore has five slices, in priority order: make long operations observable and
cancellable; let them return a handle instead of blocking; give every tool a machine-readable
result contract; pre-rank the diagnostic tools' output; and give every blocking operation a
timeout.

It absorbs four existing ROADMAP proposals: **#061** (Schema-First Tool Definitions),
**#062** (Standardized Response Schema), **#066** (Long operations on MCP Tasks), and the
enrichment half of **#037** (Enriched Debug State).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Long operations become observable and interruptible (Priority: P1)

An agent asks debug-mcp to inspect a solution with ReSharper. Today it sends the call and waits
in silence, with no way to tell a slow operation from a hung one, and no way to abandon it. In
this story the agent sees named stages as they complete — engine download, solution build,
inspection, parsing — and can withdraw the request at any point, after which the server stops
doing the work rather than finishing it for nobody.

**Why this priority**: This is the most acute of the constitution violations this feature repairs.
It is also the prerequisite for Story 2 — an operation cannot
be handed off as a background task until it can first report on itself and be stopped. It
requires no change to any result shape, so it ships without touching client compatibility.

**Independent Test**: Invoke a long-running tool with progress requested, and assert that
distinct, ordered stage updates arrive before the final result. Separately, withdraw an
in-flight request and assert the server abandons the work rather than completing it.

**Acceptance Scenarios**:

1. **Given** a client that asked for progress on a solution inspection, **When** the operation
   passes through engine acquisition, build and inspection, **Then** the client receives an
   ordered stage update for each, each carrying a human-readable stage name, before the result.
2. **Given** a client that did not ask for progress, **When** the same operation runs, **Then**
   it completes exactly as it does today, with no error and no behavioural change.
3. **Given** an inspection in progress, **When** the client withdraws the request, **Then** the
   server stops the work and releases its resources instead of running to completion.
4. **Given** any tool that reads debuggee state, **When** the client withdraws the request
   mid-call, **Then** the debug session remains usable for subsequent calls.

---

### User Story 2 - Long operations return a handle instead of blocking (Priority: P2)

An agent starts a solution inspection and immediately gets back a handle rather than a stalled
call. It can ask about that handle whenever it likes, do other work in between, and collect the
result when the work finishes. Clients that do not understand handles keep getting today's
blocking behaviour, unchanged.

**Why this priority**: It removes the last structural reason a long operation can fail — the
client-side call timeout — and frees the agent to interleave other work. It ranks below Story 1
because progress alone already converts the worst symptom (indistinguishable from a hang) into a
visible one, and because this story's value depends on client support that not every client has.

**Independent Test**: With a client that declares support, invoke a qualifying tool and assert a
handle comes back within a fraction of the operation's real duration; poll it to completion and
assert the final payload is byte-identical to what the blocking path returns. With a client that
does not declare support, assert the blocking path is used.

**Acceptance Scenarios**:

1. **Given** a client that declared support for deferred results, **When** it invokes a
   qualifying long operation, **Then** it receives a handle in under a second, carrying an
   identifier, a status, an expiry and a suggested polling interval.
2. **Given** a client that did **not** declare support, **When** it invokes the same tool,
   **Then** it receives the ordinary blocking result and never a handle.
3. **Given** an outstanding handle, **When** the client asks about it, **Then** it gets the
   current status; once the work is finished the same enquiry returns the identical payload the
   blocking path would have produced.
4. **Given** an outstanding handle, **When** the client cancels it, **Then** the server
   acknowledges and stops the work, and the handle reaches a terminal status.
5. **Given** a handle whose work has failed, **When** the client asks about it, **Then** it
   receives the same structured error the blocking path would have returned.
6. **Given** a handle that has outlived its stated expiry, **When** the client asks about it,
   **Then** it receives a clear expiry error rather than a stale or empty result.

---

### User Story 3 - Every tool publishes a checkable result contract (Priority: P3)

An agent inspecting the tool catalogue can see, before ever calling a tool, exactly what shape
that tool's result takes. When the result arrives it is structured data, not a string that must
be parsed and hoped over. Errors follow one shape across all 39 tools rather than 39 near-misses.

**Why this priority**: It is the largest slice by file count and the one visible to every client,
so it ships after the async work has settled. Its value is real but cumulative — it reduces
agent guesswork and parse failures across every interaction rather than fixing one acute failure.

**Independent Test**: Fetch the tool catalogue and assert every tool declares a result schema.
Invoke each tool and assert the returned structured payload validates against its declared
schema, and that failures across all tools share one error shape.

**Acceptance Scenarios**:

1. **Given** a client listing available tools, **When** it reads the catalogue, **Then** every
   one of the 39 tools declares a result schema describing its success payload.
2. **Given** any tool invocation that succeeds, **When** the result arrives, **Then** it carries
   structured data validating against that tool's declared schema.
3. **Given** any tool invocation that fails, **When** the result arrives, **Then** it carries a
   code, a human-readable message and optional details, in one shape shared by all tools.
4. **Given** a client that cannot read structured results, **When** it invokes any tool, **Then**
   it still receives a readable text rendering of the same result and continues to work.
5. **Given** the user-facing tool documentation, **When** it is compared against the served tool
   list, **Then** every documented tool exists and every existing tool is documented — matched by
   name — and any divergence fails the build.

---

### User Story 4 - Diagnostic results arrive pre-ranked (Priority: P4)

An agent diagnosing an exception receives, alongside the raw frames, the server's ranked
assessment of which frames are worth looking at and why — each with the concrete evidence that
earned it the rank. The agent spends its context window on the diagnosis rather than on sifting.

**Why this priority**: It delivers the clearest token savings but depends on Story 3's result
contracts to express the new fields cleanly, and it is the slice most likely to need tuning
against real cases. It is additive — nothing existing is removed — so it is the safest slice to
ship last.

**Independent Test**: Replay a recorded faulting scenario and assert the frame a human would
identify appears at the top of the ranking, with its supporting evidence attached. Assert the
same input always yields the same ranking.

**Acceptance Scenarios**:

1. **Given** an exception with a null-valued local at the fault site, **When** the agent requests
   exception context, **Then** the response ranks candidate frames and the frame introducing the
   null ranks first, with the offending variable named as evidence.
2. **Given** any ranked response, **When** the same scenario is replayed, **Then** the ranking
   and its scores are identical — no run-to-run variation.
3. **Given** a ranked response, **When** the agent ignores the ranking, **Then** all the raw data
   available today is still present and unchanged.
4. **Given** a scenario with no symbols loaded, **When** ranking cannot be computed, **Then** the
   response says so explicitly and still returns the raw data, rather than omitting the field
   silently or failing.

---

### User Story 5 - Every blocking operation can be bounded by a timeout (Priority: P5)

An agent issuing any call that waits — on the debuggee, on a build, on a symbol server — can say
in advance how long it is willing to wait. When that budget is exhausted the call returns a clear
timeout error and the debug session is still usable, instead of the agent having to choose between
waiting indefinitely and abandoning the connection.

**Why this priority**: This closes a constitution requirement that is violated today, and it was
originally excluded from this feature on the grounds that it changes tool *inputs* while the rest
of the feature changes *outputs*. That reasoning still holds — which is why it ships last, as its
own slice, rather than being mixed into the output migration. It ranks below the other four
because cancellation (Story 1) already gives clients a way to bound a call, so this slice improves
ergonomics and compliance rather than removing a failure that has no workaround.

**Independent Test**: Invoke a blocking tool with a deliberately short timeout against work known
to exceed it, and assert a timeout error comes back within the budget and that the next call
succeeds. Invoke the same tool with no timeout supplied and assert the documented default applies.

**Acceptance Scenarios**:

1. **Given** any blocking tool, **When** the agent inspects its parameters, **Then** an optional
   timeout parameter is present and its default is documented.
2. **Given** a blocking tool invoked with a timeout shorter than the work requires, **When** the
   budget is exhausted, **Then** the call returns a timeout error naming the elapsed budget, and
   the debug session remains usable for the next call.
3. **Given** a blocking tool invoked with no timeout, **When** it runs, **Then** the documented
   default for that tool applies — 30 seconds for ordinary tools, and the tool's own longer
   documented default for the long-running ones.
4. **Given** an agent that never supplies a timeout, **When** it uses any tool as it does today,
   **Then** nothing it relies on changes.

---

### Edge Cases

- **Client declares deferred-result support mid-session but not on a given call.** Support is
  declared per request, not once per session. The server MUST decide per call, on that call's
  declaration alone, and MUST NOT return a handle to a call that did not declare support.
- **Debuggee terminates while a deferred operation is outstanding.** The operation MUST reach a
  terminal failed status carrying the reason, not hang or report success.
- **Client disconnects while work is in flight.** The server MUST stop the abandoned work rather
  than continue burning CPU for a client that is gone.
- **Withdrawal arrives mid-way through a runtime call that cannot be interrupted.** The server
  MUST NOT abandon the runtime mid-operation; it completes the indivisible step, then stops.
  Debuggee state must never be left inconsistent to honour a cancellation.
- **Two long operations requested concurrently.** Each MUST get its own independent handle and
  progress stream, with no cross-talk between them.
- **Result exceeds the size budget.** The tool MUST return a bounded result with an explicit
  truncation marker stating what was omitted, never a silently trimmed one. The budget is a stated
  number, not a judgement call — see FR-035.
- **Timeout expires while an indivisible runtime step is in flight.** A timeout is bounded-wait,
  not forced termination. It obeys the same rule as cancellation: the indivisible step completes,
  then the call returns a timeout error. Debuggee state is never left inconsistent to honour a
  deadline.
- **Timeout and deferred results together.** A tool invoked through the deferred path applies its
  timeout to the underlying work, not to the handle. Exhausting it drives the handle to `failed`
  with a timeout error — distinct from the handle's own expiry, which concerns the handle's
  lifetime rather than the work's.
- **Ranking heuristics disagree with reality.** Ranking is advisory. The raw data it was derived
  from MUST always remain present so a consumer can reach a different conclusion.
- **Progress requested on an operation with no meaningful stages.** The tool completes without
  emitting progress; absent progress MUST NOT be treated as an error.
- **Server restarts while handles are outstanding.** Handles do not survive a restart. Enquiries
  about a handle from a previous process MUST return a clear not-found error, never a stale
  result.

## Requirements *(mandatory)*

### Functional Requirements

#### Asynchrony and cancellation (Story 1)

- **FR-001**: Every tool MUST expose an asynchronous invocation path; no tool may block the
  request-handling thread for the duration of its work.
- **FR-002**: Every tool MUST accept a cancellation signal from the client and MUST stop work at
  the earliest point at which stopping leaves the debug session consistent.
- **FR-003**: Cancellation MUST NOT leave the debuggee, the debug session, or any server-held
  state inconsistent. Where an operation cannot be safely interrupted, it MUST run that step to
  completion before honouring the cancellation.
- **FR-004**: Tools whose work has distinguishable stages MUST report progress as those stages
  begin, each update carrying a human-readable stage name and, where a total is knowable, a
  completed-of-total count.
- **FR-005**: Progress reporting MUST degrade silently: a client that does not request progress
  MUST observe behaviour identical to today's.
- **FR-006**: The existing lock-ordering invariant between user-facing operations and runtime
  callback state MUST be preserved. No change made for asynchrony may introduce interleaving
  that is not possible today.

#### Deferred results (Story 2)

- **FR-007**: The server MUST advertise support for deferred results in its capabilities.
- **FR-008**: The server MUST return a deferred-result handle **only** to a request that
  declared support for them. Absent that declaration, the server MUST return the ordinary
  blocking result.
- **FR-009**: A handle MUST carry a unique identifier, an initial status, an expiry, and a
  suggested polling interval, and MUST be returned before the underlying work completes.
- **FR-010**: The server MUST let a client enquire about a handle and receive the current
  status; on success the terminal response MUST carry the identical payload the blocking path
  would have produced, and on failure the identical structured error.
- **FR-011**: The server MUST accept cancellation of an outstanding handle, acknowledge it, and
  drive the handle to a terminal status.
- **FR-012**: Enquiry about an unknown or expired identifier MUST return a distinguishable error
  that states which of the two occurred.
- **FR-013**: Only operations that can exceed **five seconds** in normal use qualify for deferred
  results. The qualifying set is: ReSharper solution inspection, ReSharper project inspection,
  batch evaluation, process launch when symbol acquisition is involved, and Roslyn workspace
  load. Every other tool MUST return its result directly.
- **FR-014**: Each qualifying tool MUST behave identically through the blocking and deferred
  paths. The same inputs MUST produce the same payload regardless of which path was taken.

#### Result contracts (Story 3)

- **FR-015**: All 39 tools MUST return structured, typed results rather than hand-assembled
  strings.
- **FR-016**: Every tool MUST publish, in the tool catalogue, a schema describing its success
  result.
- **FR-017**: Every tool result MUST also carry a readable text rendering, so that clients unable
  to consume structured results continue to work unchanged.
- **FR-018**: All 39 tools MUST report failure in one shared shape carrying a code, a
  human-readable message, and optional structured details.
- **FR-019**: Error codes MUST be drawn from a single documented set; a tool MUST NOT invent a
  code outside it.
- **FR-020**: An automated check MUST fail the build when any of the following diverge: (a) a
  tool exists but publishes no result schema, (b) a tool's actual result does not validate
  against the schema it publishes, or (c) a tool exists but is absent from the user-facing tool
  documentation, or the documentation names a tool that no longer exists. The check verifies
  documentation coverage by tool name only; it does not attempt to derive result shapes from
  prose.
- **FR-021**: Existing field names and value semantics MUST be preserved through the migration.
  A consumer reading a field today MUST find the same field, with the same meaning, afterwards.
- **FR-035**: A single serialized-result size budget MUST be defined, defaulting to **256 KB** and
  overridable by configuration. A tool whose result would exceed it MUST bound the result and
  attach a truncation marker naming what was omitted and why. The tools subject to bounding are
  those that return unbounded collections: `variables_get`, `types_get`, `members_get`,
  `references_get`, `stacktrace_get`, `timeline_query`, `modules_search`, `object_inspect`,
  `collection_analyze`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics`,
  `resharper_inspect_solution` and `resharper_inspect_project`. Every other tool returns a
  naturally bounded result and MUST NOT truncate.

#### Deterministic enrichment (Story 4)

- **FR-022**: The server MUST NOT call any language model, and MUST NOT require or accept a model
  provider credential. All enrichment MUST be computed from data the server already holds.
- **FR-023**: Enrichment MUST be deterministic: identical debuggee state MUST yield an identical
  ranking, including identical scores.
- **FR-024**: Exception context MUST include a ranked list of candidate frames, each carrying a
  score and the concrete evidence supporting it — named variables and their values, and source
  locations.
- **FR-025**: Enrichment MUST be strictly additive. Every field available before this feature
  MUST remain present and unchanged.
- **FR-026**: When enrichment cannot be computed — missing symbols, unavailable state — the
  response MUST say so explicitly and still carry the raw data.
- **FR-027**: Ranking heuristics and their weights MUST be documented and individually testable
  against recorded scenarios.
- **FR-030**: A corpus of at least **10 recorded faulting scenarios** MUST be produced as part of
  this feature, each carrying the debuggee state at the fault and the frame a human identifies as
  the fault site. No such corpus exists today, and SC-006, SC-007 and SC-008 cannot be evaluated
  without it. The corpus MUST cover at least: a null dereference, an exception thrown inside a
  nested call chain, an exception crossing an async boundary, an aggregate/inner exception, and a
  scenario with symbols deliberately unavailable.

#### Per-call timeouts (Story 5)

- **FR-031**: Every tool that performs a blocking operation MUST accept an **optional** timeout
  parameter. A blocking operation is one that waits on something outside the server's own memory:
  the debuggee, a build, a symbol server, or the ReSharper engine. Tools that only read in-memory
  server state are not blocking operations and MUST NOT gain the parameter.
- **FR-032**: The default when no timeout is supplied MUST be **30 seconds**, except for tools
  that already document a longer default, which keep theirs. Applying 30 seconds to the
  long-running tools would break them — solution inspection routinely exceeds it — so their
  existing documented defaults stand and are stated in their descriptions.
- **FR-033**: Exhausting the budget MUST return a distinct, documented timeout error code naming
  the elapsed budget, and MUST leave the debug session usable for the next call.
- **FR-034**: A timeout MUST obey the same consistency rule as cancellation (FR-003): where an
  operation cannot be safely interrupted, the indivisible step completes before the timeout is
  honoured. A timeout bounds waiting; it never forces the runtime into an inconsistent state.

#### Housekeeping

- **FR-028**: `ROADMAP.md` MUST be corrected as part of this feature: the duplicate `Tier 4`
  heading resolved, the `034` number collision between the shipped ReSharper feature and the
  proposed Edit-and-Continue feature resolved, and entries `031` and `032` — currently listed
  simultaneously as completed and as proposed — reconciled.
- **FR-029**: Proposals **#061**, **#062** and **#066** MUST be marked as fully absorbed by this
  feature rather than left as open proposals, and **#037** MUST be marked as partially absorbed
  — its enrichment half is delivered here, its remaining scope stays open.

### Key Entities

- **Deferred-result handle**: A handle for work that outlives its request. Carries an
  identifier, a lifecycle status (working, awaiting input, completed, failed, cancelled), an
  expiry, a suggested polling interval, and — once terminal — either the final payload or the
  structured error. Scoped to the server process; does not survive a restart.
- **Progress update**: An ordered, in-flight report against one operation. Carries a stage name
  and, where knowable, a completed-of-total count. Advisory: carries no data the final result
  does not also carry.
- **Tool result contract**: The published description of one tool's success payload, plus the
  shared failure shape. Lives in the tool catalogue and is enforced against the tool's real
  output by an automated check.
- **Ranked suspect**: One candidate frame in a diagnostic result, carrying a frame reference, a
  deterministic score, and the evidence that produced it — named variables with values, and
  source locations. Advisory; never replaces the raw data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An agent running a first-time solution inspection sees the first stage update
  within 5 seconds, an update on every subsequent stage change, and never more than 60 seconds
  of silence — instead of today's complete silence for the whole run.
- **SC-002**: An agent can abandon any in-flight operation and issue its next successful call
  within 5 seconds, with the debug session still usable.
- **SC-003**: For each of the five qualifying operations, an opted-in client obtains the complete
  result without any single request/response exchange staying open longer than the handle's
  suggested polling interval — verified by driving all five through the deferred path and
  recording the longest exchange. This removes the structural cause of client-timeout failures
  rather than merely observing their absence.
- **SC-004**: 100% of tools publish a result schema, and 100% of their results validate against
  the schema they publish.
- **SC-005**: 100% of tool failures across all 39 tools carry a code drawn from the single
  documented set.
- **SC-006**: Diagnosing a recorded faulting scenario consumes at least 50% fewer tokens than
  today, because the agent reads the ranked assessment instead of every frame's every local.
- **SC-007**: In a suite of at least 10 recorded faulting scenarios, the frame a human identifies
  as the fault site ranks first in at least 8.
- **SC-008**: Replaying any recorded scenario 10 times yields byte-identical enrichment output
  every time.
- **SC-009**: A client that supports none of the new capabilities observes behaviour identical to
  today's across the full tool surface — no errors, no changed field names, no changed meanings.
- **SC-010**: The build continues to complete with zero errors and zero warnings.
- **SC-011**: 100% of blocking tools accept an optional timeout and document its default, closing
  the constitution's tool-standards requirement that is unmet today.
- **SC-012**: A blocking tool given a timeout shorter than its work returns within that budget
  plus the duration of one indivisible runtime step, and the next call succeeds.
- **SC-013**: The server ships with no dependency on any language-model provider and no
  configuration path that accepts a model credential — verified automatically, not by inspection.

## Assumptions

- **Deferred-result handles live only in memory, for the life of the server process.** The
  transport is stdio with one client per process, so there is no reconnect-and-resume semantics
  to serve; and every debug-session-bound result references runtime state that dies with the
  process anyway. Persisting handles across restarts would promise a resumption the architecture
  cannot deliver. Clients enquiring about a handle from a previous process receive a not-found
  error.
- **Deferred results are opt-in per request and therefore safe to add.** The specification
  forbids returning a handle to a client that did not declare support, so clients that never
  declare it are unaffected by this feature existing.
- **Enrichment is scoped to the four tools that already perform analysis** — exception context,
  object summarization, collection analysis and stack traces. Extending it to the remaining tools
  is deliberately out of scope until these four prove their heuristics.
- **The five-second threshold in FR-013 is a design rule, not a runtime measurement.** Tools are
  assigned to the qualifying set by their known worst case, not by timing each call.
- **Existing tool names, parameter names and parameter semantics are unchanged.** This feature
  changes how results are shaped and delivered, never how tools are addressed or invoked.
- **The existing four prompts and seven resources are unchanged.** Resources already return
  structured payloads and are not part of the result-contract migration.
- **Pagination remains out of scope.** Several tools return unbounded collections, but bulk APIs
  and pagination are tracked separately as ROADMAP #036; this feature bounds results through
  explicit truncation markers only.
- **Model-backed enrichment is rejected, not deferred.** The MCP mechanism for it is deprecated
  and security-flagged; direct provider integration was weighed and declined on cost,
  non-determinism, credential handling and testability. Reopening it requires a new decision, not
  a follow-up task.
- **Per-call timeouts are in scope, isolated as Story 5.** The constitution's tool standards
  require that *"all blocking operations MUST accept optional timeout (default: 30s)"*, and the
  gap predates this feature. It was initially excluded on the grounds that it changes tool
  *inputs* while the rest of the feature changes *outputs*; that reasoning survives as the reason
  Story 5 is a **separate, last slice** rather than being folded into the output migration. It
  does not survive as a reason to leave a constitution requirement unmet, so the slice is in.
- **The 30-second default is not applied uniformly.** Applying it to the long-running tools would
  break them outright — solution inspection routinely runs for minutes. Tools with an existing
  documented longer default keep it (FR-032). A constitution requirement is satisfied by every
  blocking tool *having* a documented timeout, not by every tool having the same one.
- **The user-facing tool documentation is prose, not a machine-readable catalogue.** It is
  organised thematically rather than one entry per tool, which is why FR-020 checks documentation
  coverage by tool name only. Making the documentation itself generated from the published
  schemas is a reasonable future step but is not required here.
