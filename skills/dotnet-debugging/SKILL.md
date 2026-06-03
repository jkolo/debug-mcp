---
name: dotnet-debugging
description: >
  Debug .NET applications using debug-mcp MCP tools — launch processes,
  set breakpoints, step through code, inspect variables, evaluate expressions,
  and analyze exceptions. Use this skill when debugging .NET/C# applications,
  investigating runtime behavior, diagnosing exceptions, inspecting object state,
  or performing any interactive debugging of a .NET process.
compatibility: Requires debug-mcp MCP server connected via stdio. .NET runtime must be installed.
metadata:
  author: debug-mcp
  version: "1.0"
---

# .NET Debugging with debug-mcp

This skill provides guidance for using the debug-mcp MCP server to interactively debug .NET applications. The server exposes 40 tools and 4 resources over the Model Context Protocol.

## When to use this skill

Use this skill when:
- Launching a .NET application under a debugger
- Setting breakpoints (line, conditional, exception) or tracepoints
- Stepping through code (step in, step over, step out)
- Inspecting variables, objects, collections, or memory
- Evaluating C# expressions in the debuggee context
- Diagnosing exceptions and analyzing stack traces
- Comparing program state across snapshots
- Navigating source code via Roslyn (go-to-definition, find usages)

## Quick start: Debug a .NET app

```
1. debug_launch(program: "bin/Debug/net10.0/MyApp.dll", stopAtEntry: true)
2. breakpoint_set(file: "Program.cs", line: 42)
3. debug_continue()
4. breakpoint_wait(timeout_ms: 30000)
5. variables_get()
6. evaluate(expression: "myList.Count")
7. debug_step(mode: "over")
8. debug_disconnect()
```

## Tool categories

### Session control
| Tool | Purpose |
|-|-|
| `debug_launch` | Launch .NET executable under debugger |
| `debug_attach` | Attach to running process by PID |
| `debug_continue` | Resume from pause |
| `debug_pause` | Pause running process |
| `debug_disconnect` | End debug session |
| `debug_state` | Get current session state |
| `debug_step` | Step in/over/out |

### Breakpoints and tracepoints
| Tool | Purpose |
|-|-|
| `breakpoint_set` | Set line breakpoint (optional condition) |
| `breakpoint_set_exception` | Break on exception type |
| `breakpoint_list` | List all breakpoints |
| `breakpoint_enable` | Enable/disable breakpoint |
| `breakpoint_remove` | Remove breakpoint |
| `breakpoint_wait` | Wait for breakpoint hit |
| `tracepoint_set` | Non-blocking observation point |

### Inspection
| Tool | Purpose |
|-|-|
| `stacktrace_get` | Get call stack |
| `threads_list` | List managed threads |
| `variables_get` | Get variables in scope |
| `evaluate` | Evaluate C# expression |
| `object_inspect` | Deep inspect heap object |
| `object_summarize` | Summarize object fields |
| `collection_analyze` | Analyze collection elements |
| `members_get` | Get type members |
| `exception_get_context` | Full exception chain analysis |

### Memory and layout
| Tool | Purpose |
|-|-|
| `memory_read` | Read raw memory bytes |
| `layout_get` | Type memory layout with field offsets |
| `references_get` | Object reference graph |

### Modules and types
| Tool | Purpose |
|-|-|
| `modules_list` | List loaded assemblies |
| `modules_search` | Search types/methods by pattern |
| `types_get` | Get types by namespace |

### Snapshots
| Tool | Purpose |
|-|-|
| `snapshot_create` | Capture debug state |
| `snapshot_list` | List snapshots |
| `snapshot_delete` | Delete snapshots |
| `snapshot_diff` | Compare two snapshots |

### Code analysis (Roslyn)
| Tool | Purpose |
|-|-|
| `code_load` | Load solution/project workspace |
| `code_goto_definition` | Navigate to symbol definition |
| `code_find_usages` | Find all symbol references |
| `code_find_assignments` | Find all assignments to variable |
| `code_get_diagnostics` | Get compilation errors/warnings |

### Process I/O
| Tool | Purpose |
|-|-|
| `process_read_output` | Read stdout/stderr |
| `process_write_input` | Write to stdin |

## Resources

Four read-only resources provide real-time session state:

| URI | Content |
|-|-|
| `debugger://session` | Current session state, process info, location |
| `debugger://breakpoints` | All active breakpoints and tracepoints |
| `debugger://threads` | Managed thread listing |
| `debugger://source/{file}` | Source code from PDB-referenced files |

## Core concepts

### Session lifecycle

A debug session follows this flow:

1. **Launch or attach** — `debug_launch` starts a process; `debug_attach` connects to a running one
2. **Set breakpoints** — before or after launching
3. **Continue/step** — `debug_continue` resumes; `debug_step` advances one step
4. **Wait for pause** — `breakpoint_wait` blocks until a breakpoint hits
5. **Inspect state** — use `variables_get`, `evaluate`, `stacktrace_get`, etc.
6. **Repeat steps 3-5** as needed
7. **Disconnect** — `debug_disconnect` ends the session

### Process must be paused for inspection

Most inspection tools require the process to be paused (at a breakpoint, after stepping, or after `debug_pause`). If you try to inspect while running, the tools will return errors.

**Always check `debug_state` if unsure whether the process is paused.**

### Breakpoint workflow

```
breakpoint_set(file, line)    → returns breakpoint ID
debug_continue()              → process runs
breakpoint_wait(timeout_ms)   → blocks until hit
variables_get()               → inspect at breakpoint
```

For conditional breakpoints, add a condition expression:
```
breakpoint_set(file: "Program.cs", line: 42, condition: "x > 100")
```

### Exception breakpoints

Break when a specific exception type is thrown:
```
breakpoint_set_exception(
  exception_type: "System.NullReferenceException",
  break_on_first_chance: true
)
```

When paused at an exception, use `exception_get_context` for full chain analysis including inner exceptions, stack frames, and variable state.

### Tracepoints vs breakpoints

Tracepoints log without pausing execution — use them to observe behavior without disrupting timing:
```
tracepoint_set(
  file: "Handler.cs",
  line: 15,
  log_message: "Request: {request.Method} {request.Path}"
)
```

### Expression evaluation

`evaluate` runs C# expressions in the debuggee context:
```
evaluate(expression: "users.Where(u => u.IsActive).Count()")
evaluate(expression: "DateTime.Now.ToString(\"yyyy-MM-dd\")")
```

The expression runs in the current frame's scope. Use `thread_id` and `frame_index` to target a specific stack frame.

### Snapshots for state comparison

Capture state at different points and diff them:
```
snapshot_create(label: "before-change")
debug_continue()
breakpoint_wait()
snapshot_create(label: "after-change")
snapshot_diff(id1: "snap-1", id2: "snap-2")
```

## Common patterns

### Investigate a NullReferenceException

1. `breakpoint_set_exception(exception_type: "System.NullReferenceException", break_on_first_chance: true)`
2. `debug_continue()`
3. `breakpoint_wait(timeout_ms: 60000)`
4. `exception_get_context(max_frames: 10, max_variables_frames: 3)`
5. Inspect the null variable with `variables_get` or `evaluate`

### Watch a variable change over iterations

1. Set a conditional breakpoint: `breakpoint_set(file, line, condition: "i % 10 == 0")`
2. Use snapshots to capture state at each hit
3. Compare with `snapshot_diff`

### Find where a method is called from

1. `code_load(path: "MyApp.sln")`
2. `code_find_usages(name: "ProcessOrder")`
3. Set breakpoints at call sites
4. Inspect call stack with `stacktrace_get`

### Analyze a collection at a breakpoint

1. `collection_analyze(collection_expression: "orders")` — get count, statistics, element preview
2. `evaluate(expression: "orders.Where(o => o.Total > 1000).ToList()")` — filter in-place
3. `object_inspect(object_ref: "ref-123", depth: 2)` — deep inspect specific elements

## Important notes

- **Timeout parameters**: Most tools accept `timeout_ms`. Default is usually sufficient, but increase for slow operations (large collections, complex evaluations).
- **Thread safety**: When debugging multi-threaded apps, specify `thread_id` to target a specific thread. Use `threads_list` to enumerate threads.
- **Frame index**: Stack frames are 0-indexed from the top. Frame 0 is the current method. Use `stacktrace_get` to see available frames before targeting a specific one.
- **Object references**: `object_inspect` and `references_get` use opaque reference strings returned by other tools. Don't fabricate these.

## Detailed reference

For complete tool parameters, return types, and advanced usage:

- [Tool reference](references/tools.md) — all 40 tools with full parameter details
- [Debugging workflows](references/workflows.md) — step-by-step recipes for common scenarios
- [Pitfalls and tips](references/pitfalls.md) — common mistakes and how to avoid them
