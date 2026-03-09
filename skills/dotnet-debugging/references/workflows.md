# Debugging Workflows

Step-by-step recipes for common debugging scenarios using debug-mcp tools.

## Workflow 1: Launch and hit a breakpoint

The most basic debugging session.

```
1. debug_launch(program: "bin/Debug/net10.0/MyApp.dll")
2. breakpoint_set(file: "Program.cs", line: 25)
3. debug_continue()
4. breakpoint_wait(timeout_ms: 30000)
5. variables_get()
6. debug_disconnect()
```

If using `stopAtEntry: true`, the process pauses before any user code runs — useful for setting breakpoints before the code path executes.

## Workflow 2: Diagnose a NullReferenceException

```
1. debug_launch(program: "bin/Debug/net10.0/MyApp.dll")
2. breakpoint_set_exception(
     exception_type: "System.NullReferenceException",
     break_on_first_chance: true
   )
3. debug_continue()
4. breakpoint_wait(timeout_ms: 60000, include_autopsy: true)
5. exception_get_context(max_frames: 10, max_variables_frames: 5)
6. stacktrace_get()
7. variables_get()
   — identify which variable is null
8. evaluate(expression: "suspectedVar")
   — confirm the null value
```

To also catch derived exception types (e.g., custom exceptions inheriting from NullReferenceException), set `include_subtypes: true`.

## Workflow 3: Debug a specific code path with conditions

When you need to break only under specific conditions:

```
1. debug_launch(program: "bin/Debug/net10.0/MyApp.dll")
2. breakpoint_set(
     file: "OrderService.cs",
     line: 87,
     condition: "order.Total > 1000 && order.Status == \"Pending\""
   )
3. debug_continue()
4. breakpoint_wait(timeout_ms: 60000)
5. variables_get(scope: "locals")
6. object_summarize(objectExpression: "order")
7. collection_analyze(collection_expression: "order.Items")
```

## Workflow 4: Trace execution without stopping

Use tracepoints to observe behavior in timing-sensitive code:

```
1. debug_launch(program: "bin/Debug/net10.0/MyApp.dll")
2. tracepoint_set(
     file: "MessageHandler.cs",
     line: 42,
     log_message: "Processing {message.Id}: type={message.Type}"
   )
3. tracepoint_set(
     file: "MessageHandler.cs",
     line: 88,
     log_message: "Completed {message.Id} in {elapsed}ms"
   )
4. debug_continue()
   — let it run, tracepoints log without pausing
5. process_read_output(stream: "stdout")
   — check application output
```

Tracepoints auto-disable after `max_notifications` (default 100). Use `hit_count_multiple` to sample (e.g., every 10th hit).

## Workflow 5: Compare state before and after an operation

```
1. breakpoint_set(file: "DataProcessor.cs", line: 50)
2. debug_continue()
3. breakpoint_wait()
4. snapshot_create(label: "before-transform", depth: 3)
5. debug_step(mode: "over")
   — step over the operation
6. snapshot_create(label: "after-transform", depth: 3)
7. snapshot_diff(id1: "<snap-1-id>", id2: "<snap-2-id>")
```

The diff shows which variables changed, what their old/new values are, and which were added/removed.

## Workflow 6: Investigate a multi-threaded issue

```
1. debug_launch(program: "bin/Debug/net10.0/MyApp.dll")
2. breakpoint_set(file: "Worker.cs", line: 30)
3. debug_continue()
4. breakpoint_wait()
5. threads_list()
   — identify thread IDs
6. stacktrace_get(thread_id: 5)
   — inspect specific thread's call stack
7. variables_get(thread_id: 5, frame_index: 0)
   — get that thread's local state
8. variables_get(thread_id: 7, frame_index: 0)
   — compare with another thread
```

## Workflow 7: Attach to a running process

```
1. debug_attach(pid: 12345)
2. debug_pause()
   — pause to set breakpoints
3. breakpoint_set(file: "Controller.cs", line: 100)
4. debug_continue()
5. breakpoint_wait()
6. variables_get()
7. debug_disconnect(terminateProcess: false)
   — detach without killing the process
```

Use `terminateProcess: false` when attaching to a server or long-running process you don't want to kill.

## Workflow 8: Navigate source code with Roslyn

```
1. code_load(path: "MyApp.sln")
2. code_find_usages(name: "ProcessOrder")
   — find all call sites
3. code_goto_definition(file: "OrderService.cs", line: 42, column: 15)
   — jump to the definition
4. code_find_assignments(name: "orderTotal")
   — find where orderTotal gets assigned
5. code_get_diagnostics(minSeverity: "Warning")
   — check for compilation issues
```

Roslyn analysis works on source code — it does not require a running debug session.

## Workflow 9: Analyze object graphs and memory

When debugging memory issues or understanding object relationships:

```
1. (at a breakpoint)
2. variables_get()
   — find the object reference
3. object_inspect(object_ref: "ref-abc123", depth: 3)
   — deep inspect the object tree
4. references_get(object_ref: "ref-abc123", direction: "outbound")
   — what does this object reference?
5. references_get(object_ref: "ref-abc123", direction: "inbound")
   — what references this object?
6. layout_get(type_name: "MyApp.Models.Customer")
   — see memory layout with field offsets
```

## Workflow 10: Interactive expression exploration

Use `evaluate` to test hypotheses without modifying code:

```
1. (at a breakpoint)
2. evaluate(expression: "customers.Count")
3. evaluate(expression: "customers.Where(c => c.Orders.Any()).Count()")
4. evaluate(expression: "string.Join(\", \", customers.Select(c => c.Name))")
5. evaluate(expression: "JsonSerializer.Serialize(order, new JsonSerializerOptions { WriteIndented = true })")
```

Expressions run in the debuggee's context with access to all loaded assemblies. Complex LINQ, string formatting, and serialization all work.

## Workflow 11: Step-by-step debugging loop

A systematic approach for stepping through code:

```
1. breakpoint_set(file: "Algorithm.cs", line: 10)
2. debug_continue()
3. breakpoint_wait()
4. debug_step(mode: "in")     — step into a method call
5. variables_get()            — check state
6. debug_step(mode: "over")   — step over (don't enter next call)
7. variables_get()            — check state again
8. debug_step(mode: "out")    — step out of current method
```

Step modes:
- `"in"` — step into the next method call
- `"over"` — execute the current line, don't enter called methods
- `"out"` — run until the current method returns

## Workflow 12: Process I/O interaction

For debugging console applications that read from stdin:

```
1. debug_launch(program: "bin/Debug/net10.0/ConsoleApp.dll")
2. debug_continue()
3. process_write_input(data: "test input\n")
4. process_read_output(stream: "stdout")
5. process_read_output(stream: "stderr")
```

Use `clear: true` on `process_read_output` to avoid reading the same output twice.
