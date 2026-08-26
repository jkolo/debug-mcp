# Implementation Plan: MCP Surface Modernization

**Branch**: `069-mcp-surface-modernization` | **Date**: 2026-08-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/069-mcp-surface-modernization/spec.md`

## Summary

Bring the MCP surface — 39 tools — up to the `2026-07-28` specification and back into line with
the project's own constitution, across five independently shippable slices:

1. **P1** Every tool becomes asynchronous and cancellable; long operations report named stages
   through `IProgress<ProgressNotificationValue>`.
2. **P2** Five qualifying long operations gain a deferred-result path via the MCP Tasks
   extension, returning a handle instead of blocking — only to clients that opt in per request.
3. **P3** All 39 tools return typed results with `UseStructuredContent`, publish an
   `outputSchema`, retain a text block for backward compatibility, and report failure through one
   shared shape plus protocol-level `isError`.
4. **P4** The four analysis tools gain deterministic, reproducible ranking of candidate frames —
   no language model anywhere in the server.
5. **P5** Every blocking tool accepts an optional timeout with a documented default, closing the
   constitution's tool-standards requirement that is unmet today.

Absorbs ROADMAP #061, #062, #066 in full and the enrichment half of #037.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (SDK pinned in `global.json` as `10.0.100` with
`rollForward: latestMinor` — any installed 10.x satisfies it)

**Primary Dependencies**: ModelContextProtocol / .Core 2.2.0 (existing); **new**:
ModelContextProtocol.Extensions.Tasks 2.2.0. ClrDebug 0.4.2, DbgShim 9.0, Roslyn 5.9.0 /
MSBuild 18.9.6 — all unchanged by this feature.

**Storage**: In-memory only. Deferred-result handles live in `InMemoryMcpTaskStore` for the life
of the server process (see [research.md](./research.md) R5). No new persistence.

**Testing**: xUnit + AwesomeAssertions + Moq. Notification-bearing paths are wrapped in
first-party interfaces with recording doubles, following the existing `IBreakpointNotifier` /
`NullBreakpointNotifier` precedent — `IMcpServer.SendNotificationAsync` is an extension method and
cannot be mocked. Wire-level behaviour (task opt-in negotiation, polling) is covered by the
stdio scenarios in [quickstart.md](./quickstart.md), not by unit tests.

**Target Platform**: Windows / macOS / Linux, x64 + arm64. stdio transport only; there is no HTTP
transport and none is added.

**Project Type**: Single project — a .NET tool exposing an MCP server.

**Performance Goals**: A deferred-result handle returns in under 1 second regardless of the
underlying operation's duration. Progress: first stage update within 5 seconds, never more than
60 seconds of silence. Converting tools to asynchronous signatures must not measurably slow the
fast path — these are in-process calls, not I/O.

**Constraints**:
- **Lock-ordering invariant is inviolable.** `_lock` → `_stateLock` is permitted; the reverse is
  forbidden; ICorDebug callbacks never take `_lock`. No `await` may span a region holding either
  lock (research.md R7).
- **Backward compatibility is a hard requirement**, not a goal: SC-009 requires that a client
  supporting none of the new capabilities observes no change at all.
- Build must stay at zero errors and zero warnings.
- Positional records; `DateTimeOffset` never `DateTime`; ID prefixes `bp-` / `tp-` / `ebp-`.

**Scale/Scope**: 39 tools, 7 resources, 4 prompts. Roughly 40 tool files touched by P3; 10 by P1;
5 by P2; 4 by P4; ~27 by P5 (those blocking tools that lack a timeout today). Plus a new corpus of
≥10 deterministic fault fixtures (FR-030).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against `.specify/memory/constitution.md` v1.0.0.

| Principle | Verdict | Notes |
|---|---|---|
| **I. Native First** | **PASS** | No debugging operation changes. ICorDebug remains the sole mechanism; no DAP, no external debugger. The feature touches transport and result shape only. |
| **II. MCP Compliance** | **PASS — actively repairs three violations** | The constitution requires that *"long-running operations MUST support progress reporting or timeout mechanisms"*, that *"Responses MUST be structured JSON suitable for AI consumption"*, and that *"all blocking operations MUST accept optional timeout (default: 30s)"*. Today zero tools report progress, every result is a hand-built string, and roughly 27 of 40 tool files accept no timeout. P1, P3 and P5 close all three. Tool naming (`noun_verb`) is untouched. |
| **III. Test-First (NON-NEGOTIABLE)** | **PASS, with an obligation** | Every slice is testable before implementation. Contract tests are explicitly required by the constitution (*"Contract tests MUST verify MCP tool schemas match documentation"*) and FR-020 implements exactly that. The Red phase must come first for all five slices — see the ordering note below. |
| **IV. Simplicity** | **PASS with one recorded deviation** | See Complexity Tracking. |
| **V. Observability** | **PASS — improves it** | Structured logging unchanged. Every tool invocation still logs name, parameters, duration and outcome. Progress reporting adds in-flight visibility that did not exist. |

### Resolved: the timeout requirement is now in scope

An earlier draft of this plan carried the timeout gap as a knowingly accepted deviation, on the
reasoning that a timeout parameter changes tool *inputs* while the rest of the feature changes
*outputs*. `/speckit-analyze` correctly escalated that to CRITICAL: a constitution MUST cannot be
deferred by a plan, only satisfied, or amended through a separate constitution change.

**Resolution**: timeouts are in scope as **Story 5 (P5)**. The input-versus-output reasoning
survives, but only as the reason Story 5 is a *separate, last slice* rather than being mixed into
the output migration — not as a reason to leave the requirement unmet. Two design consequences
worth stating here, because getting either wrong turns compliance into a regression:

- The 30-second default is **not** applied uniformly. Tools with an existing documented longer
  default keep it (FR-032); imposing 30 seconds on solution inspection would break a tool that
  routinely runs for minutes.
- A timeout is bounded *waiting*, not forced termination. It obeys the same consistency rule as
  cancellation (FR-034 defers to FR-003): an indivisible runtime step completes first.

The Constitution Check above therefore records no carried deviation for Principle II.

### Post-design re-check

Re-evaluated after Phase 1 artifacts were produced: no principle moves. The data model introduces
no new persistence, the contracts introduce no new transport, and the enrichment design introduces
no external dependency. The single Complexity Tracking entry below stands unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/069-mcp-surface-modernization/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output — 9 resolved decisions
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output — validation scenarios
├── contracts/           # Phase 1 output
│   ├── tool-result-contract.md
│   ├── deferred-result-contract.md
│   └── progress-contract.md
├── checklists/
│   └── requirements.md  # Spec quality + planning-readiness audit
└── tasks.md             # Phase 2 output — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
DebugMcp/
├── Program.cs                          # + task store wiring, + tasks capability
├── Tools/                              # all 39 tools: async, CancellationToken,
│   │                                   #   typed results, TaskSupport level
│   └── ...
├── Models/
│   ├── ErrorResponse.cs                # existing ErrorCodes — reused, US5 may add a timeout code
│   ├── Results/                        # NEW: one typed result record per tool
│   └── Inspection/
│       └── RankedSuspect.cs            # NEW: enrichment output
├── Services/
│   ├── ExceptionAutopsyService.cs      # + deterministic ranking
│   ├── Inspection/                     # + ranking heuristics, documented weights
│   ├── Progress/                       # NEW: first-party progress abstraction
│   │                                   #   (mirrors IBreakpointNotifier precedent)
│   ├── Tasks/                          # NEW: TaskExecutionPolicy (the FR-013 qualifying-tool
│   │                                   #   table, consulted by ExecutionModeSelector — there is
│   │                                   #   no per-tool TaskSupport property in SDK 2.2.0, see
│   │                                   #   research.md R1); + task-store decorator, only if
│   │                                   #   InMemoryMcpTaskStore cannot distinguish expired from
│   │                                   #   unknown ids (FR-012)
│   └── Timeouts/                       # NEW: timeout policy + per-tool defaults
└── Infrastructure/

tests/
├── DebugMcp.Tests/
│   ├── Contract/
│   │   └── ToolAnnotationTests.cs      # + schema presence, + doc coverage (FR-020)
│   ├── Unit/
│   └── Support/                        # + recording progress/task doubles
├── DebugMcp.E2E/
└── DebugTestApp/
    └── FaultScenarios/                 # NEW: ≥10 recorded scenarios (FR-030)

docs/
├── dependencies.md                     # + the new Tasks extension package
└── enrichment-heuristics.md            # NEW: ranking rules and their weights (FR-027)

website/docs/tools/*.md                 # kept in sync by the FR-020 coverage check
ROADMAP.md                              # FR-028/FR-029 housekeeping
```

**Structure Decision**: Single project, existing layout preserved. New directories, each justified
by a distinct requirement: `Models/Results/` for FR-015 typed results, `Services/Progress/` for
FR-004's testable abstraction, `Services/Timeouts/` for FR-031's policy, `Services/Tasks/` for
FR-013's qualifying-tool policy (unconditional — the SDK has no per-tool task-support setting to
substitute for it; see research.md R1) plus an FR-012 store decorator only if needed, and
`tests/DebugTestApp/FaultScenarios/` for FR-030's corpus. No new project, no new assembly.

### Slice ordering

The slices are independently shippable but not independently orderable. P1 precedes P2 because an
operation must be able to report on itself and be stopped before it can be handed off as a task.
P3 precedes P4 because enrichment adds fields, and adding them to typed records is cleaner than
adding them to hand-built strings and migrating twice. P5 comes last because it is the only slice
that touches tool *inputs*; keeping it separate is what stops the input and output migrations from
landing in the same review.

P1 → P2 → P3 → P4 → P5 is therefore the intended order, and each boundary is a viable stopping
point. P5 has one soft dependency: its timeout errors use the shared error shape from P3, so
shipping P5 before P3 would mean writing that error twice.

## Complexity Tracking

> Filled because Constitution Check records one deviation from Principle IV.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| **A fourth level of indirection on the five qualifying tools' call path** (tool → task wrapper → task store → existing service → ICorDebug), against Principle IV's *"Maximum 3 levels of indirection for any operation path"* | FR-013 and Story 2 require a long operation to return a handle before its work completes. That inherently interposes a layer between the request and the work: something must own the in-flight operation once the response has already been sent. | *Keeping the blocking path only* forfeits Story 2 entirely and leaves the client-timeout failure unaddressed. *Hand-rolling a job-handle convention on ordinary tool calls* (the spec's "Stateful Tools" pattern) removes no indirection — the layer still exists — while additionally forfeiting `tasks/cancel` and forcing clients to learn a bespoke convention instead of a standard one. The extra level is confined to five tools; the other 34 are pinned to `TaskSupport = Forbidden` and keep today's depth. |
