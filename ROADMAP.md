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
| 024 | MCP Tool Annotations | v0.9.0 | Tool annotations (readOnlyHint, destructiveHint, idempotentHint), enhanced descriptions for all 34 tools |
| 025 | Cross-Platform Support | v0.10.0 | Windows, macOS, Linux (x64 + ARM64), dynamic DbgShim discovery, CI matrix on 3 OSes |
| 026 | Async Stack Traces | v0.11.0 | Resolve `MoveNext()` to logical names, walk `Task.m_continuationObject` chains, strip state machine variable names |

## Proposed Features

### Tier 1 — AI-Native Debugging (highest ROI)

Features that fundamentally change how AI agents interact with the debugger — reducing round-trips, token usage, and enabling autonomous debugging loops.

#### 027 - State Snapshot & Diff
Capture debug state (variables, threads, memory regions) at arbitrary points and diff two snapshots. Tools: `snapshot_create(label)` and `snapshot_diff(id1, id2)` returning structured changes (e.g., "field `_retryCount` changed from 2 to 3"). Enables agents to track state evolution without re-reading entire scope.

#### 028 - Collection & Object Summarizer
Smart inspection for large objects: `collection_analyze(variable)` returns count, min, max, average for primitives; common types, null count, first/last N items for objects. `object_summarize` returns key fields, sizes, interesting flags (nulls, defaults, NaNs, empty strings). Prevents token blowup on large object graphs.

#### 029 - Safe Evaluation Mode
`evaluate_pure` that restricts to member access, arithmetic, comparisons — blocking method calls with side effects. Configurable allowlist of known-pure methods. Essential guardrail for autonomous agents that might otherwise execute destructive code (DB drops, file deletes) inside the debugged process.

#### 030 - Batch Evaluate & Hypothesis Runner
Run N micro-experiments in one call: set breakpoints/tracepoints, run to events, collect variables/stack/memory snapshots, output structured summary. Agents can request 5-20 experiments instead of slow sequential single-stepping. Enables parallel hypothesis testing.

### Tier 2 — Enhanced Debugging Capabilities

Features that significantly expand what agents can diagnose and how efficiently.

#### 031 - Unified Debugging Timeline
`debugger://timeline` resource merging breakpoint hits, exceptions (first-chance/user-unhandled), module loads, thread starts/exits, stdout/stderr events. Each event includes stable IDs and references to thread, frame, module, source location. Enables cross-modality reasoning ("right after stdout line X, exception Y happened").

#### 032 - Correlation IDs
Every tool invocation accepts optional `correlation_id`, echoed in timeline/notifications. Agents can associate outcomes with actions — critical for multi-step plans and parallel agent workflows.

#### 033 - Edit and Continue (Hot Patching)
Modify code while paused and resume with changes applied via `code_apply_patch(file, content)`. Leverages Roslyn EnC capabilities. Closes the autonomous loop: reproduce bug → inspect → write fix → apply → verify — without restarting the process. The "holy grail" of AI-assisted debugging.

#### 034 - Symbol Health Diagnostics
`symbols_status` tool: per-module PDB loaded? Source server available? Portable PDB? Checksum match? Where loaded from (cache, NuGet, Microsoft, local)? Actionable remediation hints. Prevents agents from wasting time when symbols are missing.

#### 035 - Bulk APIs & Pagination
Add `next_cursor` + `total_estimate` to `variables_get`, `members_get`, `references_get`, `types_get`, `modules_list/search`. Prevents token blowup on large result sets and improves latency for "scan then zoom" patterns.

#### 036 - Enriched Debug State
Enrich `debug_state` with: `stop_reason` (breakpoint/exception/step/completed/pause), exception details when relevant, "safe-to-evaluate" hints. Consistent across notifications. Gives agents clear state machine transitions.

#### 037 - Thread Focus Mode
`debug_focus_thread(thread_id)` — all subsequent stepping/inspection commands implicitly target this thread. Reduces parameter passing and agent errors from hallucinating wrong thread IDs.

### Tier 3 — Advanced Analysis

Deep analysis features for complex debugging scenarios.

#### 038 - Heap Snapshot & Diff
Capture heap object snapshots and compare two snapshots to find leaked or growing objects. Enables autonomous memory leak diagnosis: set breakpoint → snapshot → continue → snapshot → diff.

#### 039 - GC Root Retention Paths
`memory_find_retention_paths(object_address)` — answer "why is this object alive?" by returning the chain of GC roots holding it. Agents follow paths better than graph visualizations.

#### 040 - Heap Query Objects
`heap_query_objects(type_name, filter_expression)` — LINQ-style queries over the managed heap. Example: find all `User` objects where `IsActive && LastLogin < threshold`. Find needles in haystacks without iterating memory.

#### 041 - Watch Queries (Temporal Predicates)
Declarative, event-driven conditions: "Notify when `Order.Total` becomes negative", "Break when `cacheHits` stops increasing for 5 seconds", "Alert on first `NullReferenceException` on thread X in module Y". Beyond static watchpoints — temporal and filtered.

#### 042 - Causality Capture
"Why did this value change?" — show last N writes to a field/property with stack traces, threads, and timestamps. Approximate via targeted conditional tracepoints on setters/usages, agent-guided narrowing.

#### 043 - Code Decompilation
`code_decompile(type_or_method)` via ICSharpCode.Decompiler — generate C# source from IL. Enables debugging third-party DLLs without source code. Fills the gap when symbol servers don't provide source.

#### 044 - Anomaly Detection
`anomaly_detect` — heuristic scan of runtime state (threads, stacks, variables) for common patterns: null reference candidates, potential deadlocks, memory pressure, thread pool starvation. Returns structured hypotheses with confidence scores. Configurable thresholds to manage false positives.

#### 045 - Thread Management
Freeze/thaw individual threads and set the active thread for inspection. Enables race condition debugging by isolating thread execution.

#### 046 - GC & Runtime Events
Subscribe to runtime events: GC collections, JIT compilations, exceptions thrown, thread pool events. Observe runtime behavior without breakpoints. Useful for performance diagnostics and understanding application health.

#### 047 - Dump File Analysis
Load and analyze `.dmp` crash dump files offline. Post-mortem debugging of production crashes without a live process. Link to common crash pipelines (dotnet-dump, Windows Error Reporting, container core dumps).

### Tier 4 — Integrations & Ecosystem

Expanding reach beyond local debugging.

#### 048 - OpenTelemetry Integration
`telemetry_get_current_activity()` — read current TraceId/SpanId/tags from `System.Diagnostics.Activity`. Correlate debugger state with distributed traces. Enables pivot from "span error in Jaeger" → "attach & break at the code".

#### 049 - CI Debug Mode
Attach to `dotnet test` process on test failure in CI. Auto-capture timeline, snapshots, symbol status. Emit debug artifact bundle as GitHub Actions artifact. Annotate PRs with failure summaries.

#### 050 - Debug Artifact Export
Export shareable bundle: tool calls made, breakpoints/tracepoints, evaluation expressions, captured timeline events, variable snapshots, symbol resolution. Reproducible debugging sessions for team collaboration and agent replay.

#### 051 - VS Code Extension
Extension that manages MCP server connection, provides "Send to agent" workflow, and surfaces debug timeline/resources in the editor. Low-friction adoption path for VS Code users.

#### 052 - Remote Debugging
TCP/network transport for debugging processes on remote machines or containers. Extends the architecture beyond local stdio.

#### 053 - DAP Compatibility Layer
Debug Adapter Protocol adapter exposing debug-mcp capabilities through the standard DAP interface. Enables IDE integration (VS Code, JetBrains) for human+AI hybrid workflows.

#### 054 - Cloud Debugging Integration
Integrate with Azure App Insights / AWS X-Ray for hybrid local/remote sessions. Pull production telemetry into MCP resources for context-aware debugging.

### Tier 5 — Developer Experience & Quality

Improvements to the project itself for maintainability and contributor productivity.

#### 055 - Guardrails Policy
Configurable safety limits: max evaluation time, max object expansion depth, max memory read size, max tool calls per minute, denylist/allowlist for `evaluate` expressions. Prevents agents from "foot-gunning" production-like processes.

#### 056 - Caching Layer
LRU cache for Roslyn compilations per session + file hash. Cross-session TTL cache for symbol server results. `cache_clear` / `cache_stats` tools for troubleshooting.

#### 057 - Internal Metrics
`debugger://metrics` resource exposing tool latency histograms, cache hit ratios, symbol download timings. Optional Prometheus endpoint for operational monitoring.

#### 058 - Mockable Test Harness
Fake ICorDebug runtime layer or recorded session playback for unit tests. Dramatically improves contributor velocity and reduces flaky E2E test dependency.

#### 059 - Debug Scenario Scripts
YAML/JSON scripting format for reproducible debugging scenarios: launch/attach parameters, breakpoints/tracepoints, expected events, assertions. Useful for regression tests and sharing repro steps.

#### 060 - Schema-First Tool Definitions
Define tools in a schema, generate C# models, JSON schema, and TypeScript client types. Stable, typed contracts for agent framework integration.

#### 061 - Standardized Response Schema
All tool responses include consistent metadata: timestamps, staleness flags (`is_stale`), partial result annotations, error context. Schema versioning via MCP for backwards compatibility.

#### 062 - Code Coverage
Track which lines/branches execute during a debug session. Useful for understanding test coverage or identifying dead code paths during debugging.

#### 063 - Auto-Generate E2E Specs
Generate Reqnroll specifications from MCP tool definitions. Reduce test coverage gaps and keep E2E tests in sync with tool API surface.
