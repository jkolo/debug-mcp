# debug-mcp.net Roadmap

## Completed Features

| # | Feature | Version | Description |
|---|---------|---------|-------------|
| 001 | Debug Session | v0.1.0 | Launch, attach, disconnect, state query |
| 002 | Breakpoint Operations | v0.1.0 | Set/remove/enable/list breakpoints, exception breakpoints, wait for hit |
| 003 | Inspection Operations | v0.1.0 | Threads, stack traces, variables, expression evaluation |
| 004 | Memory Operations | v0.1.0 | Raw memory read, object inspect, references, type layout |
| 005 | Module Operations | v0.1.0 | List modules, browse types, get members, search |
| 006 | Debugger Bugfixes | v0.2.0 | Fixed ICorDebug interaction bugs |
| 007 | Debug Launch | v0.2.0 | Launch with env, cwd, args, stopAtEntry |
| 008 | Reqnroll E2E Tests | v0.2.1 | BDD end-to-end tests with Reqnroll/Gherkin |
| 009 | Comprehensive E2E Coverage | v0.2.2 | Extended BDD test scenarios across all tool categories |
| 010 | .NET Tool Packaging | v0.2.3 | Distributed as `dnx -y debug-mcp` |
| 011 | CI/CD Pipeline | v0.2.3 | GitHub Actions build, test, publish workflow |
| 012 | Documentation Improvement | v0.2.3 | Docusaurus website, architecture docs, asciinema demos |
| 013 | Cleanup & Bugfixes | v0.2.3 | Code quality, consistency, edge case fixes |
| 014 | MCP Logging | v0.3.0 | Structured logging with configurable levels |
| 015 | Roslyn Code Analysis | v0.3.0 | Go to definition, find usages, find assignments, diagnostics |
| 016 | Breakpoint Notifications | v0.4.0 | Tracepoints, log message templates, hit count filtering, async push |
| 017 | Process I/O Redirection | v0.4.0 | stdin/stdout/stderr capture and forwarding |
| 018 | Optional Roslyn | v0.5.0 | `--no-roslyn` flag to disable code analysis tools at startup |
| 019 | MCP Resources | v0.6.0 | Session, breakpoints, threads, source file resources |
| 020 | MCP Completions | v0.6.0 | Argument auto-complete for all tools |
| 021 | Symbol Server Integration | v0.7.0 | Automatic PDB download from NuGet/Microsoft symbol servers |
| 022 | Exception Autopsy | v0.8.0 | One-shot `exception_get_context`: exception chain, stack frames, locals, source — replaces 3-4 tool calls |
| 024 | MCP Tool Annotations | v0.9.0 | Tool annotations (readOnlyHint, destructiveHint, idempotentHint), enhanced descriptions for all tools |
| 025 | Cross-Platform Support | v0.10.0 | Windows, macOS, Linux (x64 + ARM64), dynamic DbgShim discovery, CI matrix on 3 OSes |
| 026 | Async Stack Traces | v0.11.0 | Resolve `MoveNext()` to logical names, walk `Task.m_continuationObject` chains, strip state machine variable names |
| 027 | State Snapshot & Diff | v0.11.0 | Capture debug state snapshots and diff two snapshots to track state evolution |
| 028 | Collection & Object Summarizer | v0.12.0 | `collection_analyze` and `object_summarize` tools — single-call collection/object inspection with stats, anomaly detection |
| 029 | Safe Evaluation Mode | TBD | `evaluate_safe` tool with Roslyn AST safety gate — blocks method calls, object construction, assignments before execution; configurable allowlist of known-pure methods; essential guardrail for autonomous agents |
| 030 | MCP Event-Driven Debugger Interface | TBD | Event-driven model replacing polling: `debugger://modules` and `debugger://snapshots` resources, `debugger/sessionStateChanged` notification, enriched `breakpointHit` payload with locals; removed 6 polling tools (35 tools total); fixed fake-async in `process_read_output`/`process_write_input` |
| 031 | Batch Evaluate | v0.17.0 | `batch_evaluate` tool — set transient breakpoints, run, and collect expression values across hits in a single call (36 tools total) |
| 032 | Unified Debugging Timeline | v0.18.0 | `timeline_query` tool + `debugger://timeline` resource — chronological view of breakpoint hits, exceptions, module/thread events, and stdout/stderr (37 tools total) |
| 034 | ReSharper Inspections | TBD | `resharper_inspect_solution` + `resharper_inspect_project` tools — runs JetBrains ReSharper's inspections (hundreds beyond Roslyn) via the auto-installed `jb inspectcode` engine; opt-out via `--no-resharper`, native severities, lazy self-installing version-pinned engine cache (39 tools total) |
| 069 | MCP Surface Modernization | v0.20.0 | Progress reporting for long operations, deferred results via MCP Tasks, every tool migrated to typed structured output with a shared success/error/truncation contract, deterministic (model-free) suspicion ranking on exception context and stack traces, per-call timeouts on every blocking tool (39 tools total, all typed) |

## Proposed Features

### Tier 1 — AI-Native Debugging (highest ROI)

Features that fundamentally change how AI agents interact with the debugger — reducing round-trips, token usage, and enabling autonomous debugging loops.

#### 031 - Batch Evaluate & Hypothesis Runner
✅ **Shipped in v0.17.0 as `batch_evaluate`** — see Completed Features above. (This entry previously appeared both here and there — reconciled per feature 069's FR-028.)

### Tier 2 — Enhanced Debugging Capabilities

Features that significantly expand what agents can diagnose and how efficiently.

#### 032 - Unified Debugging Timeline
✅ **Shipped in v0.18.0 as `timeline_query` + `debugger://timeline`** — see Completed Features above. (This entry previously appeared both here and there — reconciled per feature 069's FR-028.)

#### 033 - Correlation IDs
Every tool invocation accepts optional `correlation_id`, echoed in timeline/notifications. Agents can associate outcomes with actions — critical for multi-step plans and parallel agent workflows.

#### 070 - Edit and Continue (Hot Patching)
Modify code while paused and resume with changes applied via `code_apply_patch(file, content)`. Leverages Roslyn EnC capabilities. Closes the autonomous loop: reproduce bug → inspect → write fix → apply → verify — without restarting the process. The "holy grail" of AI-assisted debugging. (Renumbered from #034, which collided with the shipped ReSharper Inspections feature — reconciled per feature 069's FR-028.)

#### 035 - Symbol Health Diagnostics
`symbols_status` tool: per-module PDB loaded? Source server available? Portable PDB? Checksum match? Where loaded from (cache, NuGet, Microsoft, local)? Actionable remediation hints. Prevents agents from wasting time when symbols are missing.

#### 036 - Bulk APIs & Pagination
Add `next_cursor` + `total_estimate` to `variables_get`, `members_get`, `references_get`, `types_get`, `modules_list/search`. Prevents token blowup on large result sets and improves latency for "scan then zoom" patterns.

#### 037 - Enriched Debug State
🟡 **Partially absorbed by feature 069** — 069 added deterministic (model-free) suspicion ranking to `exception_get_context` and `stacktrace_get`, in the same spirit of "give agents pre-digested state instead of raw data to sift". Still open: `debug_state` no longer exists as a tool (replaced by the `debugger://session` resource in feature 030) so this proposal's literal `stop_reason`/"safe-to-evaluate" hints on it were not addressed — that remains available scope on the `debugger://session` resource.
Enrich `debug_state` with: `stop_reason` (breakpoint/exception/step/completed/pause), exception details when relevant, "safe-to-evaluate" hints. Consistent across notifications. Gives agents clear state machine transitions.

#### 038 - Thread Focus Mode
`debug_focus_thread(thread_id)` — all subsequent stepping/inspection commands implicitly target this thread. Reduces parameter passing and agent errors from hallucinating wrong thread IDs.

### Tier 3 — Advanced Analysis

Deep analysis features for complex debugging scenarios.

#### 039 - Heap Snapshot & Diff
Capture heap object snapshots and compare two snapshots to find leaked or growing objects. Enables autonomous memory leak diagnosis: set breakpoint → snapshot → continue → snapshot → diff.

#### 040 - GC Root Retention Paths
`memory_find_retention_paths(object_address)` — answer "why is this object alive?" by returning the chain of GC roots holding it. Agents follow paths better than graph visualizations.

#### 041 - Heap Query Objects
`heap_query_objects(type_name, filter_expression)` — LINQ-style queries over the managed heap. Example: find all `User` objects where `IsActive && LastLogin < threshold`. Find needles in haystacks without iterating memory.

#### 042 - Watch Queries (Temporal Predicates)
Declarative, event-driven conditions: "Notify when `Order.Total` becomes negative", "Break when `cacheHits` stops increasing for 5 seconds", "Alert on first `NullReferenceException` on thread X in module Y". Beyond static watchpoints — temporal and filtered.

#### 043 - Causality Capture
"Why did this value change?" — show last N writes to a field/property with stack traces, threads, and timestamps. Approximate via targeted conditional tracepoints on setters/usages, agent-guided narrowing.

#### 044 - Code Decompilation
`code_decompile(type_or_method)` via ICSharpCode.Decompiler — generate C# source from IL. Enables debugging third-party DLLs without source code. Fills the gap when symbol servers don't provide source.

#### 045 - Anomaly Detection
`anomaly_detect` — heuristic scan of runtime state (threads, stacks, variables) for common patterns: null reference candidates, potential deadlocks, memory pressure, thread pool starvation. Returns structured hypotheses with confidence scores. Configurable thresholds to manage false positives.

#### 046 - Thread Management
Freeze/thaw individual threads and set the active thread for inspection. Enables race condition debugging by isolating thread execution.

#### 047 - GC & Runtime Events
Subscribe to runtime events: GC collections, JIT compilations, exceptions thrown, thread pool events. Observe runtime behavior without breakpoints. Useful for performance diagnostics and understanding application health.

#### 048 - Dump File Analysis
Load and analyze `.dmp` crash dump files offline. Post-mortem debugging of production crashes without a live process. Link to common crash pipelines (dotnet-dump, Windows Error Reporting, container core dumps).

### Tier 4 — Integrations & Ecosystem

Expanding reach beyond local debugging.

#### 049 - OpenTelemetry Integration
`telemetry_get_current_activity()` — read current TraceId/SpanId/tags from `System.Diagnostics.Activity`. Correlate debugger state with distributed traces. Enables pivot from "span error in Jaeger" → "attach & break at the code".

#### 050 - CI Debug Mode
Attach to `dotnet test` process on test failure in CI. Auto-capture timeline, snapshots, symbol status. Emit debug artifact bundle as GitHub Actions artifact. Annotate PRs with failure summaries.

#### 051 - Debug Artifact Export
Export shareable bundle: tool calls made, breakpoints/tracepoints, evaluation expressions, captured timeline events, variable snapshots, symbol resolution. Reproducible debugging sessions for team collaboration and agent replay.

#### 052 - VS Code Extension
Extension that manages MCP server connection, provides "Send to agent" workflow, and surfaces debug timeline/resources in the editor. Low-friction adoption path for VS Code users.

#### 053 - Remote Debugging
TCP/network transport for debugging processes on remote machines or containers. Extends the architecture beyond local stdio.

#### 054 - DAP Compatibility Layer
Debug Adapter Protocol adapter exposing debug-mcp capabilities through the standard DAP interface. Enables IDE integration (VS Code, JetBrains) for human+AI hybrid workflows.

#### 055 - Cloud Debugging Integration
Integrate with Azure App Insights / AWS X-Ray for hybrid local/remote sessions. Pull production telemetry into MCP resources for context-aware debugging.

### Tier 5 — Developer Experience & Quality

Improvements to the project itself for maintainability and contributor productivity.

#### 056 - Guardrails Policy
Configurable safety limits: max evaluation time, max object expansion depth, max memory read size, max tool calls per minute, denylist/allowlist for `evaluate` expressions. Prevents agents from "foot-gunning" production-like processes.

#### 057 - Caching Layer
LRU cache for Roslyn compilations per session + file hash. Cross-session TTL cache for symbol server results. `cache_clear` / `cache_stats` tools for troubleshooting.

#### 058 - Internal Metrics
`debugger://metrics` resource exposing tool latency histograms, cache hit ratios, symbol download timings. Optional Prometheus endpoint for operational monitoring.

#### 059 - Mockable Test Harness
Fake ICorDebug runtime layer or recorded session playback for unit tests. Dramatically improves contributor velocity and reduces flaky E2E test dependency.

#### 060 - Debug Scenario Scripts
YAML/JSON scripting format for reproducible debugging scenarios: launch/attach parameters, breakpoints/tracepoints, expected events, assertions. Useful for regression tests and sharing repro steps.

#### 061 - Schema-First Tool Definitions
✅ **Fully absorbed by feature 069** — every tool now returns a typed C# record (`[McpServerTool(UseStructuredContent = true)]`), from which the SDK derives both `outputSchema` and `structuredContent` by reflection. Stable, typed contracts now exist for every one of the 39 tools; a generated TypeScript client remains open scope beyond what 069 covered.

#### 062 - Standardized Response Schema
✅ **Fully absorbed by feature 069** — every tool response now shares one contract: `{success, <data fields>, error?}`, with a shared `ToolError{code, message, details}` shape and a centrally-derived `isError` flag (see `specs/069-mcp-surface-modernization/contracts/tool-result-contract.md`). Collection-returning tools additionally carry a shared `TruncationInfo` when a 256 KB size budget trims a result. Timestamps/staleness flags beyond this were not part of 069's scope.

#### 063 - Code Coverage
Track which lines/branches execute during a debug session. Useful for understanding test coverage or identifying dead code paths during debugging.

#### 064 - Auto-Generate E2E Specs
Generate Reqnroll specifications from MCP tool definitions. Reduce test coverage gaps and keep E2E tests in sync with tool API surface.

### Tier 6 — MCP Protocol Evolution (unlocked by MCP SDK v2)

Capabilities that became available after the MCP SDK 1.3.0 → 2.2.0 upgrade (spec `2026-07-28`). Not implemented yet — proposals only.

#### 065 - Migrate Logging off deprecated MCP Logging capability
MCP Logging (`notifications/message`, `McpServer.LoggingLevel`) is deprecated as of spec `2026-07-28` ([SEP-2577](https://modelcontextprotocol.io/seps/2577-deprecate-roots-sampling-and-logging)) — still functional for ≥12 months, currently tracked as suppressed `MCP9005` in `DebugMcp/DebugMcp.csproj`. `McpLogger` (`DebugMcp/Infrastructure/McpLogger.cs`) already supports a stderr sink via `LoggingOptions.EnableStderr`; this feature is the decision + implementation to make stderr (or an OpenTelemetry exporter) the primary delivery path and drop the protocol notification once the deprecation window closes. See `docs/dependencies.md` for the current state.

#### 066 - Long operations on MCP Tasks
✅ **Fully absorbed by feature 069** — `resharper_inspect_solution`, `resharper_inspect_project`, `batch_evaluate`, `debug_launch` and `code_load` now defer to `ModelContextProtocol.Extensions.Tasks` for a client that opts in (`TaskExecutionPolicy.QualifyingTools`), returning a pollable/cancellable handle instead of blocking the request. Progress reporting for the same five tools' stages was also added (069's User Story 1), ahead of and independent of the Tasks deferral.
`ModelContextProtocol.Extensions.Tasks` (SEP-2663) gives long-running tool calls a way to report progress and let the client poll/cancel instead of blocking on a single request. Direct fit for the worst UX spots in the current tool surface: `resharper_inspect_solution` (first-use engine acquisition is a ~180–650 MB download plus a full solution build, today a single opaque blocking call), `batch_evaluate` (N sequential experiments with no interim feedback), and `debug_launch` when it triggers symbol-server downloads. Would need a new package reference and a design pass on which tools opt in.

#### 067 - Caching hints on immutable results
SEP-2549 caching hints let a tool response tell the client a result is safe to reuse without a new round-trip. Natural candidates: `modules_list`, `types_get`, `members_get`, `layout_get` — all describe a loaded module's static shape, which doesn't change while that module stays loaded. Directly serves the existing Tier 1 goal of reducing agent round-trips and token usage.

#### 068 - Batch breakpoint-state notifications with DeferChangedEvents
The SDK's `DeferChangedEvents()` batches multiple primitive-change notifications (e.g. resource list changes) into one push instead of one per change. Relevant to `debugger://breakpoints`: `batch_evaluate` can set and clear many transient breakpoints in one call today, each mutation firing its own notification — deferring would collapse that into a single update per batch run.
