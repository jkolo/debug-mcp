---
title: Tools Overview
sidebar_position: 0
---

# Tools Overview

debug-mcp exposes 34 tools organized into 8 categories.

| Category | Tools | When to use |
|----------|-------|-------------|
| [Session](/docs/tools/session) | `debug_launch`, `debug_attach`, `debug_disconnect`, `debug_state` | Start and end debug sessions |
| [Breakpoints](/docs/tools/breakpoints) | `breakpoint_set`, `breakpoint_remove`, `breakpoint_list`, `breakpoint_enable`, `breakpoint_set_exception`, `tracepoint_set`, `breakpoint_wait`, `exception_get_context` | Control where execution stops |
| [Execution](/docs/tools/execution) | `debug_continue`, `debug_pause`, `debug_step` | Resume, pause, and step through code |
| [Inspection](/docs/tools/inspection) | `threads_list`, `stacktrace_get`, `variables_get`, `evaluate`, `object_inspect` | Examine threads, stacks, variables, and expressions |
| [Memory](/docs/tools/memory) | `memory_read`, `layout_get`, `references_get` | Read raw memory, analyze object layout, trace references |
| [Modules](/docs/tools/modules) | `modules_list`, `modules_search`, `types_get`, `members_get` | Browse loaded assemblies, types, and members |
| [Code Analysis](/docs/tools/code-analysis) | `code_load`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics`, `code_goto_definition` | Static analysis with Roslyn (no debugger needed) |
| [Process I/O](/docs/tools/process-io) | `process_write_input`, `process_read_output` | Send input and read output from the debugged process |

## Session State Requirements

Tools require different session states to work:

| Requirement | Tools |
|-------------|-------|
| **No session needed** | `debug_launch`, `debug_attach`, `debug_state`, `code_load`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics`, `code_goto_definition` |
| **Active session** (running or paused) | `debug_disconnect`, `breakpoint_set`, `breakpoint_remove`, `breakpoint_list`, `breakpoint_enable`, `breakpoint_set_exception`, `tracepoint_set`, `breakpoint_wait`, `debug_continue`, `debug_pause`, `modules_list`, `modules_search`, `types_get`, `members_get`, `process_write_input`, `process_read_output` |
| **Paused session** | `debug_step`, `threads_list`, `stacktrace_get`, `variables_get`, `evaluate`, `object_inspect`, `memory_read`, `layout_get`, `references_get`, `exception_get_context` |
