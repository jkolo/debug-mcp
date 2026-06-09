# Feature Specification: Batch Evaluate & Hypothesis Runner

**Feature Branch**: `031-batch-evaluate`  
**Created**: 2026-06-09  
**Status**: Draft  

## Overview

AI debugging agents currently diagnose bugs by repeatedly cycling through: set a breakpoint, resume execution, wait for the hit, inspect variables, continue, repeat. Each cycle requires multiple sequential round-trips between the agent and the debugger — and every wait-for-hit blocks the agent until execution reaches that point. For an agent testing five hypotheses, this means five full blocking cycles with no parallelism.

This feature introduces a batch model: the agent defines up to 20 micro-experiments upfront, submits them in one call, and receives a structured summary of all results. The debugger handles the sequencing internally — the agent no longer orchestrates individual hit-inspect-continue loops.

---

## Clarifications

### Session 2026-06-09

- Q: Dwa eksperymenty na tej samej lokalizacji — niezależne wyniki czy scalane? → A: Każdy eksperyment dostaje niezależny wynik; debugger rejestruje jeden fizyczny breakpoint na daną lokalizację i dispatchuje do wszystkich eksperymentów pasujących warunkiem — każdy z własnym capture list i licznikiem hitów.
- Q: Pre-existing breakpoints podczas batcha — ignorowane/przerywają/dezaktywowane? → A: Pre-existing breakpoints są tymczasowo dezaktywowane na czas trwania batcha i przywracane (z oryginalnym stanem enabled/disabled) po jego zakończeniu lub anulowaniu.
- Q: Tryb ewaluacji wyrażeń w eksperymentach — safe-eval, pełna, czy konfigurowalne? → A: Konfigurowalne per-batch: agent podaje `eval_mode: safe | full` przy submisji; domyślnie `safe`.
- Q: Systemowy cap na łączny rozmiar odpowiedzi batcha — miękki/twardy/brak? → A: Miękki cap na łączną liczbę hitów w batchu (domyślnie 500); po przekroczeniu batch kończy się z powodem "hit_limit_reached" i zwraca wszystkie zebrane dane.
- Q: Cleanup breakpointów eksperymentów po zakończeniu batcha — automatyczny/ręczny/konfigurowalny? → A: Automatyczny cleanup — wszystkie breakpointy zarejestrowane przez batch są usuwane po jego zakończeniu lub anulowaniu; pre-existing breakpointy są przywracane.

---

## User Scenarios & Testing

### User Story 1 — Test Multiple Hypotheses Without Sequential Blocking (Priority: P1)

An AI agent suspects a bug might be caused by one of several conditions: an unexpected null value, a counter that goes negative, or an object that is re-used after being closed. Currently, the agent must test each hypothesis one at a time — set a breakpoint, resume, wait, inspect, continue, repeat. If the first hypothesis is wrong, the agent discards everything and starts over.

In the new model, the agent submits all three hypotheses as a single batch: three trigger locations or conditions with the variables to capture at each. The debugger executes the program, collects data at each trigger point as it is reached, and returns a structured summary of all results. The agent reads the summary and immediately knows which hypothesis is correct without a second round-trip.

**Why this priority**: This is the core value proposition of the feature — eliminating sequential round-trips that make hypothesis testing expensive for token-budget-constrained agents.

**Independent Test**: Submit a batch of 3 experiments targeting 3 different source locations. Run a test program that passes through all three. Verify that the returned summary contains captured variable data for all three locations — without any intermediate tool calls between submission and result retrieval.

**Acceptance Scenarios**:

1. **Given** an agent submits a batch of N experiments (1 ≤ N ≤ 20), **When** the debugged program runs, **Then** the agent receives a single structured response containing the outcome of every experiment, with no intermediate calls required.

2. **Given** an experiment specifies a source location and a list of variable names to capture, **When** execution reaches that location, **Then** the result includes the captured variable values, the thread ID, the stack depth, and the timestamp of the hit.

3. **Given** a batch contains experiments whose triggers are never reached during the run, **When** the program exits or the batch times out, **Then** the summary marks those experiments as "not triggered" and still returns all data collected for experiments that did fire.

4. **Given** an experiment specifies a condition expression in addition to a location, **When** execution reaches the location but the condition evaluates to false, **Then** no data is collected for that hit and execution continues; the experiment remains active until the condition is true or the batch completes.

5. **Given** the agent submits a batch, **When** the run completes (program exits, batch times out, or all experiments triggered), **Then** the summary includes a top-level count of: total experiments, triggered, not triggered, and errors.

---

### User Story 2 — Observe Multiple Points Non-Blocking (Priority: P2)

An agent wants to trace the value of a variable across five different functions in a hot path — not to pause execution, but to collect observations as the program runs at full speed. Currently the agent would need five tracepoints and then parse the notification stream to correlate them.

In the new model, the agent defines five observation points in a batch, each marked as non-blocking (collect and continue). The program runs to completion; the agent receives a summary with the collected observations from each point in order. No notifications to parse, no correlation work.

**Why this priority**: Non-blocking observation is a natural fit for the batch model and removes the need to subscribe to and parse a notification stream for simple multi-point tracing.

**Independent Test**: Submit a batch of 5 non-blocking observation experiments. Run a program that passes through all 5 points. Verify that the summary contains timestamped observations from each point in execution order, and that the program ran to completion without pausing.

**Acceptance Scenarios**:

1. **Given** an experiment is marked as non-blocking, **When** execution reaches the trigger location, **Then** the data is collected and execution continues immediately — the program is not paused.

2. **Given** a non-blocking experiment trigger fires multiple times (e.g., inside a loop), **When** the batch completes, **Then** the result for that experiment includes all hits with their values and timestamps, up to a configurable maximum per-experiment hit count.

3. **Given** a batch contains a mix of blocking and non-blocking experiments, **When** a blocking experiment fires, **Then** execution pauses at that point while the other experiments' results up to that moment are preserved; on resume, the remaining experiments continue as configured.

---

### User Story 3 — Partial Results on Timeout or Early Exit (Priority: P3)

An agent submits a batch of 10 experiments targeting different code paths. After 30 seconds, only 6 have triggered — the other 4 are in code paths that require a specific input not encountered during the run. The agent wants whatever data was collected, not a timeout error.

In the new model, the batch has a configurable timeout. When it expires, the batch returns immediately with all data collected so far, clearly marking which experiments triggered and which did not. The agent can inspect partial results and decide whether to run another targeted batch.

**Why this priority**: Graceful partial return is essential for real-world use where not every code path fires in every run. Silent loss of collected data would make the feature unreliable.

**Independent Test**: Submit a batch where some experiments target unreachable code. Set a short timeout. Verify that the response arrives at timeout with partial results — triggered experiments include their data, untriggered experiments are marked "not triggered", and no data is lost.

**Acceptance Scenarios**:

1. **Given** a batch has a configured timeout, **When** the timeout expires before all experiments trigger, **Then** the batch returns immediately with all data collected so far and marks untriggered experiments explicitly.

2. **Given** the debugged process exits before all experiments trigger, **Then** the batch returns with partial results and a "process exited" completion reason.

3. **Given** the agent cancels the batch mid-run, **Then** all data collected up to that point is returned in the summary.

---

### Edge Cases

- When two experiments target the exact same source location: the debugger registers one physical breakpoint for that location and dispatches the hit to all experiments whose condition (if any) evaluates to true. Each matching experiment records an independent hit with its own capture list and hit counter.
- What happens when variable evaluation at a trigger location times out or throws? The experiment result should include partial data (location, timestamp) rather than be omitted entirely.
- What happens when a blocking experiment fires inside a loop — does it pause on every iteration or only the first hit up to the per-experiment maximum?
- What happens when the batch exceeds the maximum experiment count (> 20)?
- What happens when an experiment's trigger location cannot be resolved (invalid source file or line number)?
- What happens when a second batch is submitted while the first is still running?

---

## Requirements

### Functional Requirements

**Batch Submission**

- **FR-001**: An agent MUST be able to submit a batch of 1 to 20 experiments in a single tool call.
- **FR-002**: Each experiment MUST specify at minimum: a trigger (source location by file and line, or an exception type) and whether execution should pause at the trigger (blocking) or continue (non-blocking).
- **FR-003**: Each experiment MAY additionally specify: a list of variable names or expressions to evaluate at the trigger, a condition expression that must be true for the trigger to fire, and a maximum number of hits to collect before the experiment is considered complete.
- **FR-003a**: The batch submission MUST accept an `eval_mode` parameter (`safe` or `full`) that applies to all expression evaluations within the batch. The default is `safe`. In `safe` mode, expressions are validated through the Roslyn AST safety gate (same rules as `evaluate_safe`) before execution. In `full` mode, arbitrary expressions are permitted; the agent assumes responsibility for any side effects.
- **FR-004**: The batch MUST accept a global timeout (in seconds) after which it returns with whatever data has been collected so far.
- **FR-004a**: The batch MUST enforce a soft cap on the total number of hits collected across all experiments (default: 500). When the cap is reached, the batch MUST end immediately with completion reason `hit_limit_reached` and return all data collected up to that point. The cap value MUST be configurable per-batch submission.
- **FR-005**: Submitting a batch while a debugger session is not active MUST return an error immediately.
- **FR-006**: Submitting a batch with more than 20 experiments MUST return a validation error without starting execution.
- **FR-007**: Submitting a second batch while the first is still running MUST return an error.
- **FR-016**: When multiple experiments share the same source location, the debugger MUST register one physical breakpoint for that location and dispatch each hit to all experiments whose condition evaluates to true. Each matching experiment accumulates hits independently.
- **FR-017**: When a batch starts, all breakpoints and tracepoints registered outside the batch (pre-existing) MUST be temporarily disabled. On batch completion or cancellation, all pre-existing breakpoints and tracepoints MUST be restored to their original enabled/disabled state, and all breakpoints registered by the batch MUST be automatically removed.

**Execution**

- **FR-008**: The debugger MUST execute the program from its current position (already running or paused) and collect data as experiments fire, without requiring the agent to call resume or continue between individual hits.
- **FR-009**: Non-blocking experiments MUST collect data and allow execution to continue without pausing the debugged program.
- **FR-010**: Blocking experiments MUST pause execution after data is collected and MUST resume automatically before proceeding to the next event, unless the batch explicitly configured the session to remain paused after the final blocking hit.
- **FR-011**: An experiment that fires in a loop MUST collect data up to its configured per-experiment hit maximum, then deactivate (execution continues without further collection at that location for that experiment).

**Results**

- **FR-012**: The batch result MUST include a top-level summary: completion reason (all triggered / timeout / process exited / cancelled), total experiments, triggered count, not-triggered count, error count.
- **FR-013**: Each experiment result MUST include: experiment index, trigger status (triggered / not triggered / error), hit count, and for each hit: timestamp, thread ID, source location, and any collected variable values.
- **FR-014**: If variable evaluation fails for a hit, the result MUST include the successfully evaluated values and mark the failed evaluations individually — the hit MUST NOT be omitted from the result.
- **FR-015**: The agent MUST be able to cancel a running batch and receive all data collected up to the cancellation point.

### Key Entities

- **Experiment**: A single observation unit within a batch — trigger definition, variable capture list, optional condition, blocking/non-blocking mode, and per-experiment hit limit.
- **Batch**: A collection of experiments submitted together, with a global timeout, a configurable total-hit cap (default 500), an `eval_mode` (`safe` or `full`), and an optional "remain paused after last blocking hit" flag.
- **Experiment Hit**: A single firing of an experiment's trigger — captures timestamp, source location, thread ID, and evaluated variable values.
- **Experiment Result**: All hits recorded for one experiment during the batch run, plus the final status (triggered / not triggered / error).
- **Batch Summary**: The top-level result returned to the agent — completion reason, aggregated counts, and the ordered list of experiment results.

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: An agent can submit 5 experiments targeting distinct source locations in one call and receive all results without any intermediate tool calls between submission and the final summary.
- **SC-002**: The number of tool calls required to test N independent hypotheses is reduced from N × (set + resume + wait + inspect) to 2 (submit batch + read summary), regardless of N up to 20.
- **SC-003**: Batch results contain sufficient data for an agent to form a diagnostic conclusion without follow-up inspection calls in at least 80% of typical single-frame debugging scenarios involving primitive and simple reference types.
- **SC-004**: When a batch ends early (timeout, process exit, hit cap reached, or cancellation), 100% of data collected before the termination point is present in the returned summary — no triggered experiment data is silently discarded.
- **SC-005**: A batch of 20 non-blocking observation experiments on a program that executes all trigger points returns a complete summary within 2 seconds of the program completing, excluding program execution time.

---

## Assumptions

- The existing breakpoint and tracepoint infrastructure (BreakpointManager, BreakpointNotifier, conditional evaluation) is the foundation for experiment triggering — this feature composes on top of it rather than replacing it.
- Variable evaluation during a batch hit uses the same evaluation budget and safety constraints as direct `evaluate` calls; the 100 ms per-notification evaluation budget from feature 030 applies per hit.
- A batch occupies the active debug session exclusively while running — a second batch cannot be submitted until the first completes or is cancelled.
- The maximum of 20 experiments per batch is a reasonable bound for agent use cases; it can be revisited if agents demonstrate need for larger batches.
- The default per-experiment hit maximum (when not specified by the agent) is 1 — collect the first hit and deactivate.

---

## Out of Scope

- Parallel execution of multiple program instances to run experiments simultaneously — all experiments run against the same sequential execution.
- Persistent experiment definitions across debug sessions — a batch is a one-shot submission, not a saved configuration.
- Replay of a recorded execution to fire all experiments against the same run — experiments fire against live execution only.
- Cross-process batching (submitting experiments to multiple attached processes in one call).
