---
title: Tools Overview
sidebar_position: 0
---

# Tools Overview

debug-mcp exposes 39 tools organized into 12 categories.

| Category | Tools | When to use |
|----------|-------|-------------|
| [Session](/docs/tools/session) | `debug_launch`, `debug_attach`, `debug_disconnect` | Start and end debug sessions |
| [Breakpoints](/docs/tools/breakpoints) | `breakpoint_set`, `breakpoint_remove`, `breakpoint_enable`, `breakpoint_set_exception`, `tracepoint_set`, `exception_get_context` | Control where execution stops |
| [Execution](/docs/tools/execution) | `debug_continue`, `debug_pause`, `debug_step` | Resume, pause, and step through code |
| [Inspection](/docs/tools/inspection) | `stacktrace_get`, `variables_get`, `evaluate`, `evaluate_safe`, `object_inspect`, `object_summarize`, `collection_analyze` | Examine stacks, variables, and expressions |
| [Memory](/docs/tools/memory) | `memory_read`, `layout_get`, `references_get` | Read raw memory, analyze object layout, trace references |
| [Modules](/docs/tools/modules) | `modules_search`, `types_get`, `members_get` | Browse loaded assemblies, types, and members |
| [Code Analysis](/docs/tools/code-analysis) | `code_load`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics`, `code_goto_definition` | Static analysis with Roslyn (no debugger needed) |
| [ReSharper](/docs/tools/resharper) | `resharper_inspect_solution`, `resharper_inspect_project` | Deeper static analysis with JetBrains ReSharper (no debugger needed) |
| [Process I/O](/docs/tools/process-io) | `process_write_input`, `process_read_output` | Send input and read output from the debugged process |
| [Snapshots](/docs/tools/snapshots) | `snapshot_create`, `snapshot_delete`, `snapshot_diff` | Capture and diff point-in-time variable state |
| [Batch Evaluate](/docs/tools/batch-evaluate) | `batch_evaluate` | Run up to 20 capture/condition micro-experiments in one call |
| [Timeline](/docs/tools/timeline) | `timeline_query` | Query a unified, chronological event history |

## No tool call needed — MCP resources

Some information that used to require a polling tool call is now exposed as an MCP resource
instead — read once, or subscribe to be notified on change:

| Resource | Replaces | Description |
|----------|----------|-------------|
| `debugger://session` | `debug_state` | Current session state, pause reason, location |
| `debugger://breakpoints` | `breakpoint_list` | All breakpoints, tracepoints, and exception breakpoints |
| `debugger://threads` | `threads_list` | Managed threads in the debugged process |
| `debugger://modules` | `modules_list` | Loaded modules/assemblies |
| `debugger://snapshots` | *(new)* | Captured state snapshots |
| `debugger://source/{+file}` | *(new)* | Source file content from PDB-referenced paths |
| `debugger://timeline` | *(new)* | The same events `timeline_query` returns |

`breakpoint_wait` has no tool or resource replacement — instead, subscribe to the
`debugger/breakpointHit` MCP notification (see [Breakpoint Notifications](/docs/tools/breakpoints#breakpoint-notifications)), which is pushed the instant a breakpoint or tracepoint fires. The
server additionally pushes a `debugger/sessionStateChanged` notification whenever the session's
state transitions.

## Long operations: progress and deferred results

Five tools — `resharper_inspect_solution`, `resharper_inspect_project`, `batch_evaluate`,
`debug_launch`, `code_load` — can genuinely take a while (a first-run ~180 MB ReSharper engine
download, a full solution build, many experiments in one batch). These report named progress
stages as they run, and a client that declares the MCP Tasks capability gets back a pollable,
cancellable handle instead of blocking the request on the whole operation. A client that does
neither sees no difference from any other tool call — both behaviors are opt-in per request. See
[Architecture: Cross-Cutting Concerns](/docs/architecture#cross-cutting-concerns-mcp-surface-modernization)
for the full design.

## Every response, every error, one shape

All 39 tools share one envelope — `{success, ...fields, error?}` on success or failure — and one
error shape, `{code, message, details?}`. A handful of tools that return unbounded collections
(`variables_get`, `types_get`, `code_get_diagnostics`, and others) can additionally carry a
`truncation` field, `{returned, available, reason}`, when a 256 KB response-size budget trims the
result — never silently.

## Timeouts

Every tool whose work waits on the debuggee, a build, a symbol server, or the ReSharper engine
accepts an optional timeout parameter — see that tool's own **Parameters** table for its exact
name and default (most default to 30 seconds; a few keep their own longer or shorter default that
predates this concern). Exhausting the budget returns a `TIMEOUT` error naming the elapsed time
and leaves the session usable for the next call. Tools that only read already-captured, in-memory
server state (listed under "No tool call needed" above, plus a few others like `snapshot_diff`)
don't accept a timeout — there's nothing external for one to bound.

## Session State Requirements

Tools require different session states to work:

| Requirement | Tools |
|-------------|-------|
| **No session needed** | `debug_launch`, `debug_attach`, `code_load`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics`, `code_goto_definition`, `resharper_inspect_solution`, `resharper_inspect_project`, `snapshot_delete`, `snapshot_diff`, `timeline_query` |
| **Active session** (running or paused) | `debug_disconnect`, `breakpoint_set`, `breakpoint_remove`, `breakpoint_enable`, `breakpoint_set_exception`, `tracepoint_set`, `debug_continue`, `debug_pause`, `modules_search`, `types_get`, `members_get`, `process_write_input`, `process_read_output`, `batch_evaluate` |
| **Paused session** | `debug_step`, `stacktrace_get`, `variables_get`, `evaluate`, `evaluate_safe`, `object_inspect`, `object_summarize`, `collection_analyze`, `memory_read`, `layout_get`, `references_get`, `exception_get_context`, `snapshot_create` |
