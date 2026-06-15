# Feature Specification: ReSharper Inspections

**Feature Branch**: `034-resharper-inspect`

**Created**: 2026-06-15

**Status**: Draft

**Input**: User description: "Add ReSharper support to debug-mcp, the same way as Roslyn — opt-out. Research how the ReSharper CLI works and how best to deploy it so it installs itself."

## Overview

debug-mcp already exposes Roslyn-based static analysis (`code_*` tools: load workspace,
find usages, go-to-definition, find assignments, get diagnostics). Roslyn's compiler
diagnostics are valuable but shallow — they catch what the C# compiler catches. JetBrains
ReSharper applies *hundreds* of additional inspections (code smells, redundancies,
potential bugs, style and correctness issues) that the compiler does not.

This feature lets an AI agent run ReSharper's inspection engine over a .NET solution or
project through MCP and receive the findings as structured data. It complements — does not
replace — the existing Roslyn tools. Like the Roslyn integration, it is **on by default and
opt-out**: a single flag turns it off for environments that do not want it.

A core constraint from the user: the ReSharper command-line engine must **install itself**
with zero manual setup. The user installs debug-mcp; the first time they ask for a ReSharper
inspection, the engine is fetched and cached automatically, and every later run reuses it.

## Clarifications

### Session 2026-06-15

- Q: ReSharper inspection (one-time ~180 MB engine download + solution build + analysis)
  far exceeds debug-mcp's usual 30s timeout — what timeout model should be used? → A:
  **Separate budgets** — a distinct, long timeout for the one-time engine acquisition AND a
  separate, generous default timeout for the inspection itself; both overridable.
- Q: ReSharper reports error/warning/suggestion/hint; Roslyn's `code_get_diagnostics`
  reports error/warning/info/hidden — what severity vocabulary should the tool expose? → A:
  **Native ReSharper severities** (error/warning/suggestion/hint) exposed verbatim in
  findings and in the severity-threshold filter; no lossy normalization to Roslyn's scale.
- Q: FR-001 allows "one or more" MCP tools — how many tools should v1 expose? → A:
  **Multiple granular, scope-specific tools** (e.g. a solution-scoped inspection tool and a
  project-scoped inspection tool) rather than one parameterized tool.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run ReSharper inspections on a solution (Priority: P1)

An AI agent investigating code quality (or hunting a subtle bug) asks debug-mcp to run
ReSharper's inspections over the user's solution. The agent gets back a structured list of
findings — each with a stable inspection id, severity, human-readable message, file, line,
and category — so it can reason about them, prioritise, and propose fixes.

**Why this priority**: This is the entire point of the feature. Without it there is no value.
It is the MVP: a single tool that produces structured ReSharper findings is independently
useful and shippable on its own.

**Independent Test**: Point the tool at a small sample solution that contains a known
ReSharper-only issue (e.g. a redundant cast or an obviously unused private member that the
C# compiler does not flag). Verify the response is a success envelope containing that
finding with the correct file/line and a ReSharper inspection id, and that the same issue
does *not* appear in `code_get_diagnostics` (proving it adds coverage beyond Roslyn).

**Acceptance Scenarios**:

1. **Given** a valid solution path and the ReSharper engine not yet installed, **When** the
   agent requests an inspection, **Then** the engine is acquired automatically (no manual
   step), the inspection runs, and a success envelope with a list of findings is returned.
2. **Given** the ReSharper engine already cached from a previous run, **When** the agent
   requests an inspection, **Then** no re-download occurs and the inspection starts without
   the acquisition delay.
3. **Given** a solution with at least one ReSharper inspection issue, **When** results are
   returned, **Then** each finding includes a non-empty inspection id, a severity, a
   message, a file path, and a line number.
4. **Given** a solution with zero issues at or above the requested severity, **When** the
   inspection completes, **Then** a success envelope with an empty findings list (not an
   error) is returned.

---

### User Story 2 - Scope and filter the inspection (Priority: P2)

The agent narrows an inspection to keep it fast and focused: limit to one project (or a file
subset), and/or only return findings at or above a chosen severity. The agent can also skip
the pre-analysis build when the solution is already built, to save time.

**Why this priority**: A full-solution inspection can be slow and noisy. Scoping and severity
filtering make the tool practical for iterative use, but the feature is still valuable without
them (Story 1 alone works). Hence P2, not P1.

**Independent Test**: Run the same solution twice — once unfiltered, once with a severity
threshold of "warning and above" and scoped to a single project — and verify the second run
returns a strict subset of findings, all at or above the threshold, all within the named
project.

**Acceptance Scenarios**:

1. **Given** a severity threshold parameter, **When** the inspection runs, **Then** only
   findings at or above that threshold are returned.
2. **Given** a project-scope parameter naming one project in the solution, **When** the
   inspection runs, **Then** only findings within that project are returned.
3. **Given** a "skip build" option and an already-built solution, **When** the inspection
   runs, **Then** the engine does not rebuild and results are still produced.
4. **Given** an invalid scope (project name that does not exist), **When** the inspection
   runs, **Then** a structured error explains the scope was not found.

---

### User Story 3 - Robust behaviour when the engine cannot be acquired or run (Priority: P3)

When the environment cannot acquire or run the ReSharper engine (no network on first use,
no .NET SDK available, the target path is not a valid solution/project, the engine crashes,
or the run exceeds a time budget), the agent receives a clear, actionable structured error
instead of a hang or an opaque failure. The rest of debug-mcp keeps working.

**Why this priority**: Reliability and good failure messages matter, but they are a
hardening layer on top of the happy path. The feature delivers value before every failure
mode is polished, so P3.

**Independent Test**: Simulate each failure mode (point at a non-solution file; make the
acquisition source unreachable; force a timeout with a tiny time budget) and verify each
returns a distinct, descriptive error envelope and that other debug-mcp tools remain
functional in the same session.

**Acceptance Scenarios**:

1. **Given** the engine is not cached and the acquisition source is unreachable, **When** an
   inspection is requested, **Then** a structured error explains acquisition failed and how
   to remedy it (e.g. check connectivity, or disable the feature), without crashing the
   server.
2. **Given** a path that is not a valid solution or project, **When** an inspection is
   requested, **Then** a structured error identifies the invalid input.
3. **Given** an inspection that exceeds its time budget, **When** the budget elapses, **Then**
   the operation is cancelled and a timeout error is returned.
4. **Given** the feature has been disabled via its opt-out flag, **When** the server starts,
   **Then** the ReSharper tool(s) are not advertised at all and the disabled state is logged.

---

### Edge Cases

- **Concurrent first-run installs**: two inspection requests arrive before the engine is
  cached — acquisition must not corrupt the cache or run twice destructively (serialise or
  make idempotent).
- **Partial/corrupted cache**: a previous acquisition was interrupted — the system must
  detect an incomplete engine and re-acquire rather than fail forever.
- **Very large result sets**: a big solution yields thousands of findings — results must be
  bounded (cap with a documented limit and a "truncated" indicator) so the response stays
  usable for an AI consumer.
- **Solution that fails to build**: when the engine builds before analysing and the build
  fails — the error must surface the build failure clearly, distinct from an inspection
  failure.
- **First-run latency**: the first inspection includes a large one-time download plus a
  build — the caller must be informed this is expected (and it must not block server
  startup, only the invoking call).
- **Disk space / permissions**: the cache directory is not writable — return a clear error
  naming the directory.
- **Findings without a file/line** (solution-level issues) — represent them without
  fabricating a location.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose ReSharper code inspection as **multiple granular,
  scope-specific MCP tools** (e.g. one tool that inspects a whole solution and a separate
  tool that inspects a single project), rather than a single parameterized tool. Each tool
  MUST return findings as structured data, each finding carrying at minimum: inspection id,
  severity, message, file path, and line number (location may be absent for non-file-scoped
  findings).
- **FR-002**: The ReSharper integration MUST be enabled by default and disableable via a
  single opt-out flag, mirroring the existing Roslyn integration's behaviour. When disabled,
  the ReSharper tool(s) MUST NOT be advertised and the disabled state MUST be logged at
  startup.
- **FR-003**: The system MUST acquire the ReSharper command-line engine automatically, with
  no manual installation step required from the user.
- **FR-004**: Acquisition MUST happen lazily — triggered by the first actual inspection
  request, never during server startup — because it involves a large download.
- **FR-005**: Once acquired, the engine MUST be persisted to a cache so subsequent
  inspections reuse it without re-downloading. The cache location MUST be overridable.
- **FR-006**: The acquired engine version MUST be pinned to a known, reproducible version
  (not "whatever is latest at the moment"), so behaviour is stable across machines and time.
- **FR-007**: Inspection results MUST be returned in the same success/error envelope
  convention used by the existing `code_*` tools (a success payload, or a structured error
  with code, message, and optional details).
- **FR-008**: Users MUST be able to scope an inspection to a single project (or documented
  subset) within a solution.
- **FR-009**: Users MUST be able to filter findings by a minimum severity threshold,
  expressed in ReSharper's native severity vocabulary (error / warning / suggestion / hint).
  Findings MUST report this same native severity verbatim; the system MUST NOT normalize or
  remap severities to Roslyn's scale.
- **FR-010**: Users MUST be able to control whether the engine builds the target before
  analysis (to skip a redundant build when already built).
- **FR-011**: Every inspection invocation MUST be logged with tool name, sanitised
  parameters, duration, and outcome (per the project's observability principle).
- **FR-012**: Time budgets MUST be modelled as two separate, overridable timeouts: (a) an
  **acquisition timeout** governing the one-time engine download/install, with a default
  generous enough for a large download; and (b) an **inspection timeout** governing a single
  analysis run, with its own generous default. Both operations MUST be cancellable, and
  exceeding either budget MUST return a structured timeout error (distinguishing acquisition
  timeout from inspection timeout) rather than hanging.
- **FR-013**: All acquisition and run failures (unreachable source, missing prerequisite,
  invalid target, build failure, engine crash, timeout, unwritable cache) MUST be reported
  as distinct, actionable structured errors, and MUST NOT crash the server or affect other
  tools.
- **FR-014**: Result sets MUST be bounded by a documented maximum count, with an explicit
  indicator when results were truncated.
- **FR-015**: The feature MUST work cross-platform (Linux, macOS, Windows) consistent with
  the rest of debug-mcp.
- **FR-016**: The ReSharper findings MUST be presented as a complement to, not a replacement
  of, the existing Roslyn `code_*` tools; both remain available simultaneously when enabled.

### Key Entities

- **Inspection Request**: what to analyse and how — target solution/project path, optional
  project scope, optional severity threshold (native ReSharper scale), optional build
  control, an optional inspection timeout, and an optional acquisition timeout for the
  one-time engine install.
- **Inspection Finding**: a single reported issue — inspection id (stable rule identifier),
  severity in ReSharper's native vocabulary (error / warning / suggestion / hint), message,
  category/group, file path (optional), line (optional), and any available position detail;
  plus the originating project where known.
- **Inspection Result**: the outcome of one run — the collection of findings, counts by
  severity, a truncation indicator, the analysed target, and timing/metadata.
- **Engine Cache**: the locally persisted, version-pinned ReSharper command-line engine —
  its on-disk location, installed version, and readiness/validity state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With the feature enabled and the engine already cached, an agent can run an
  inspection on a small solution and receive structured findings in a single tool call,
  with no manual installation having ever been performed by the user.
- **SC-002**: On a sample solution containing a known ReSharper-only issue, the inspection
  surfaces that issue while `code_get_diagnostics` (Roslyn) does not — demonstrating added
  coverage.
- **SC-003**: The first inspection on a clean machine completes the full acquire-then-analyse
  flow end-to-end without user intervention; the second inspection performs no re-download.
- **SC-004**: Disabling the feature via its opt-out flag results in the ReSharper tool(s)
  being absent from the advertised tool list, and every other debug-mcp tool continues to
  function unchanged.
- **SC-005**: Each defined failure mode (unreachable acquisition source, invalid target,
  timeout, disabled feature) returns a distinct, descriptive result and never hangs or
  crashes the server.
- **SC-006**: Severity filtering and project scoping each demonstrably reduce the returned
  findings to the expected subset on the same solution.

## Assumptions

- **Read-only scope for v1**: only code *inspection* (reporting issues) is in scope.
  Automated code *cleanup/reformatting* (which rewrites source files) is explicitly **out of
  scope for v1** to keep the feature non-destructive and lower-risk; it may be a later
  feature.
- **Free engine**: the ReSharper inspection engine used is the freely available
  command-line inspection tool; no paid license key is required to run inspections. (If a
  license is ever required for some inspections, that is out of scope for v1.)
- **.NET SDK present**: the host already has a .NET SDK available (debug-mcp itself is a
  .NET tool), which is the prerequisite for acquiring and running the engine. Absence of the
  SDK is handled as a graceful, actionable error (FR-013), not a supported happy path.
- **Network available on first use**: the one-time engine acquisition requires network
  access. Offline first-use is handled as a graceful error, not a supported happy path.
- **Opt-out parity with Roslyn**: "the same way as Roslyn" means default-on with a single
  disable flag and tool-registration filtering, consistent with the existing pattern.
- **Solution-oriented input**: the primary input is a solution; single-project input is
  supported where the engine allows it. Inspecting a single loose source file outside any
  project is not a goal.
- **Naming follows project conventions**: the multiple granular MCP tool names follow the
  `noun_verb` convention used across debug-mcp (e.g. `resharper_*` tools scoped per target),
  consistent with the constitution's MCP compliance principle. Their C# class names share a
  common prefix so the opt-out registration filter can exclude them as a group.
- **Cache lives alongside other debug-mcp caches**: the engine cache uses the same
  per-user cache root that debug-mcp already uses for downloaded artefacts (e.g. symbols),
  with an overridable location.

## Dependencies

- The JetBrains ReSharper command-line inspection engine, distributed as a downloadable
  .NET tool, acquired at runtime.
- A .NET SDK on the host (used to acquire and run the engine).
- The existing debug-mcp caching/acquisition pattern already used for downloaded artefacts
  (symbol server cache) as the model for the engine cache.
- Network connectivity for the one-time engine acquisition.

## Out of Scope (v1)

- Automated code cleanup / reformatting that modifies source files.
- Bundling the (~large) engine inside the debug-mcp package itself (acquisition is at
  runtime to keep the package small).
- Exposing the full surface of every engine command-line switch; only the documented subset
  (scope, severity, build control, timeout) is supported.
- Custom inspection profiles / `.DotSettings` management as first-class parameters (may be a
  later enhancement).
- Duplicate-code detection and other non-inspection engine tools.
