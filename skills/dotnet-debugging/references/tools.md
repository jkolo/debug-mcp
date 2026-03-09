# Tool Reference

Complete parameter reference for all 40 debug-mcp tools. Parameters marked with `*` are required.

## Session control

### debug_launch

Launch a .NET executable under debugger control.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `program`* | string | — | Path to .NET executable (.dll or .exe) |
| `args` | string[] | null | Command-line arguments |
| `cwd` | string | null | Working directory |
| `env` | string | null | Environment variables (KEY=VALUE, newline-separated) |
| `stopAtEntry` | bool | false | Pause at entry point before any user code runs |
| `timeout` | int | 30000 | Launch timeout in milliseconds |

### debug_attach

Attach to a running .NET process.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `pid`* | int | — | Process ID to attach to |
| `timeout` | int | 30000 | Attach timeout in milliseconds |

### debug_continue

Resume execution from a paused state.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `timeout` | int | 5000 | Continue timeout in milliseconds |

### debug_pause

Pause a running process. No parameters.

### debug_disconnect

Disconnect the debug session.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `terminateProcess` | bool | true | Whether to kill the debuggee process |

### debug_state

Get current session state (process info, pause reason, source location). No parameters.

### debug_step

Step through code.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `mode`* | string | — | `"in"`, `"over"`, or `"out"` |
| `timeout` | int | 10000 | Step timeout in milliseconds |

## Breakpoints and tracepoints

### breakpoint_set

Set a line breakpoint.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `file`* | string | — | Source file path |
| `line`* | int | — | Line number |
| `column` | int | null | Column number (optional precision) |
| `condition` | string | null | C# condition expression (break only when true) |

Returns: breakpoint ID (e.g., `bp-<guid>`)

### breakpoint_set_exception

Set an exception breakpoint.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `exception_type`* | string | — | Full exception type name (e.g., `System.NullReferenceException`) |
| `break_on_first_chance` | bool | true | Break when exception is first thrown |
| `break_on_second_chance` | bool | false | Break on unhandled exception |
| `include_subtypes` | bool | true | Also break on derived exception types |

Returns: exception breakpoint ID (e.g., `ebp-<guid>`)

### breakpoint_list

List all active breakpoints, tracepoints, and exception breakpoints. No parameters.

### breakpoint_enable

Enable or disable a breakpoint.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `id`* | string | — | Breakpoint ID |
| `enabled`* | bool | — | Enable (true) or disable (false) |

### breakpoint_remove

Remove a breakpoint by ID.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `id`* | string | — | Breakpoint ID to remove |

### breakpoint_wait

Wait for a breakpoint to be hit.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `timeout_ms` | int | 30000 | Maximum wait time |
| `breakpoint_id` | string | null | Wait for a specific breakpoint (null = any) |
| `include_autopsy` | bool | false | Include exception autopsy if paused at exception |

### tracepoint_set

Set a non-blocking observation point that logs without pausing.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `file`* | string | — | Source file path |
| `line`* | int | — | Line number |
| `column` | int | null | Column number |
| `log_message` | string | null | Message template with `{expression}` interpolation |
| `hit_count_multiple` | int | 1 | Only notify every N hits |
| `max_notifications` | int | 100 | Maximum notifications before auto-disable |

Returns: tracepoint ID (e.g., `tp-<guid>`)

## Inspection

### stacktrace_get

Get the call stack for a thread.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `thread_id` | int | null | Thread ID (null = current/active thread) |
| `start_frame` | int | 0 | Start from this frame index |
| `max_frames` | int | 20 | Maximum frames to return |
| `include_raw` | bool | false | Include raw ICorDebug frame data |

### threads_list

List all managed threads. No parameters.

### variables_get

Get variables in scope at a stack frame.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `thread_id` | int | null | Thread ID (null = current) |
| `frame_index` | int | 0 | Stack frame index (0 = top) |
| `scope` | string | "all" | `"locals"`, `"arguments"`, `"this"`, or `"all"` |
| `expand` | string | null | Dot-delimited path to expand (e.g., `"request.Headers"`) |

### evaluate

Evaluate a C# expression in the debuggee context.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `expression`* | string | — | C# expression to evaluate |
| `thread_id` | int | null | Thread ID |
| `frame_index` | int | 0 | Stack frame index |
| `timeout_ms` | int | 5000 | Evaluation timeout |

### object_inspect

Inspect a heap object by reference.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `object_ref`* | string | — | Object reference string from prior inspection |
| `depth` | int | 1 | Inspection depth (nested objects) |
| `thread_id` | int | null | Thread ID |
| `frame_index` | int | 0 | Stack frame index |

### object_summarize

Summarize an object's fields, categorizing them as valued, null, default, or anomalous.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `objectExpression`* | string | — | Variable name or expression resolving to an object |
| `max_collection_preview` | int | 3 | Max collection elements to preview |
| `thread_id` | int | null | Thread ID |
| `frame_index` | int | 0 | Stack frame index |
| `timeout_ms` | int | 5000 | Timeout |

### collection_analyze

Analyze a collection with count, element preview, and statistics.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `collection_expression`* | string | — | Variable name or expression resolving to a collection |
| `preview_count` | int | 5 | Number of elements to preview |
| `thread_id` | int | null | Thread ID |
| `frame_index` | int | 0 | Stack frame index |
| `timeout_ms` | int | 5000 | Timeout |

### members_get

Get members (methods, properties, fields) of a type.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `type_name`* | string | — | Fully qualified type name |
| `module_name` | string | null | Module to search in |
| `include_inherited` | bool | false | Include inherited members |
| `member_kinds` | string | null | Filter: `"method"`, `"property"`, `"field"` (comma-separated) |
| `visibility` | string | null | Filter: `"public"`, `"private"`, `"protected"`, `"internal"` |
| `include_static` | bool | true | Include static members |
| `include_instance` | bool | true | Include instance members |

### exception_get_context

Get full exception context including inner exception chain, stack frames, and variable snapshots.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `max_frames` | int | 10 | Max stack frames per exception |
| `max_variables_frames` | int | 3 | Include variables for top N frames |
| `max_inner_depth` | int | 5 | Max inner exception depth |

## Memory and layout

### memory_read

Read raw memory bytes from the debuggee process.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `address`* | string | — | Memory address (hex string, e.g., `"0x7fff1234"`) |
| `size`* | int | — | Number of bytes to read |
| `format` | string | "hex" | Output format: `"hex"`, `"decimal"`, `"ascii"` |

### layout_get

Get memory layout of a type showing field offsets and sizes.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `type_name`* | string | — | Fully qualified type name |
| `include_inherited` | bool | true | Include inherited fields |
| `include_padding` | bool | false | Show padding bytes |
| `thread_id` | int | null | Thread ID |
| `frame_index` | int | 0 | Stack frame index |

### references_get

Analyze object references (outbound or inbound).

| Parameter | Type | Default | Description |
|-|-|-|-|
| `object_ref`* | string | — | Object reference string |
| `direction` | string | "outbound" | `"outbound"` (what this object references) or `"inbound"` (what references this object) |
| `max_results` | int | 20 | Maximum references to return |
| `include_arrays` | bool | true | Include array element references |
| `thread_id` | int | null | Thread ID |
| `frame_index` | int | 0 | Stack frame index |

## Modules and types

### modules_list

List loaded assemblies in the debugged process.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `include_system` | bool | false | Include system/framework modules |
| `name_filter` | string | null | Filter by module name substring |

### modules_search

Search for types or methods across loaded modules.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `pattern`* | string | — | Search pattern (supports wildcards) |
| `search_type` | string | "type" | `"type"` or `"method"` |
| `module_filter` | string | null | Restrict to specific module |
| `case_sensitive` | bool | false | Case-sensitive matching |
| `max_results` | int | 50 | Maximum results |

### types_get

Get types in a module organized by namespace.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `module_name`* | string | — | Module name |
| `namespace_filter` | string | null | Filter by namespace prefix |
| `kind` | string | null | Filter: `"class"`, `"struct"`, `"enum"`, `"interface"` |
| `visibility` | string | null | Filter: `"public"`, `"internal"` |
| `max_results` | int | 100 | Maximum results |
| `continuation_token` | string | null | Token for pagination |

## Snapshots

### snapshot_create

Capture current debug state as a named snapshot.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `label` | string | null | Human-readable label |
| `thread_id` | int | null | Thread ID |
| `frame_index` | int | 0 | Stack frame index |
| `depth` | int | 2 | Variable inspection depth |

### snapshot_list

List all snapshots with metadata. No parameters.

### snapshot_delete

Delete snapshot(s).

| Parameter | Type | Default | Description |
|-|-|-|-|
| `id` | string | null | Snapshot ID (null = delete all) |

### snapshot_diff

Compare two snapshots and return differences.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `id1`* | string | — | First snapshot ID |
| `id2`* | string | — | Second snapshot ID |

## Code analysis (Roslyn)

These tools require a loaded Roslyn workspace. Call `code_load` first. Disabled when server runs with `--no-roslyn`.

### code_load

Load a .NET solution or project into the Roslyn workspace.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `path`* | string | — | Path to `.sln` or `.csproj` file |

### code_goto_definition

Navigate to where a symbol is defined.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `file`* | string | — | Source file containing the symbol |
| `line`* | int | — | Line number |
| `column`* | int | — | Column number |

### code_find_usages

Find all references to a symbol.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `name` | string | null | Symbol name to search for |
| `symbolKind` | string | null | Filter by kind |
| `file` | string | null | Source file (for positional lookup) |
| `line` | int | null | Line number |
| `column` | int | null | Column number |

### code_find_assignments

Find all assignments to a variable or property.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `name` | string | null | Variable/property name |
| `symbolKind` | string | null | Filter by kind |
| `file` | string | null | Source file |
| `line` | int | null | Line number |
| `column` | int | null | Column number |

### code_get_diagnostics

Get compilation diagnostics (errors, warnings).

| Parameter | Type | Default | Description |
|-|-|-|-|
| `projectName` | string | null | Filter to specific project |
| `minSeverity` | string | null | Minimum severity: `"Hidden"`, `"Info"`, `"Warning"`, `"Error"` |
| `maxResults` | int | null | Maximum diagnostics to return |

## Process I/O

### process_read_output

Read stdout or stderr from the debuggee process.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `stream` | string | "stdout" | `"stdout"` or `"stderr"` |
| `clear` | bool | false | Clear the buffer after reading |

### process_write_input

Write data to the debuggee's stdin.

| Parameter | Type | Default | Description |
|-|-|-|-|
| `data`* | string | — | Data to write |
| `close_after` | bool | false | Close stdin after writing |
