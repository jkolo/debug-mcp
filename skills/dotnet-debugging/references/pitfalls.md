# Pitfalls and Tips

Common mistakes when using debug-mcp tools and how to avoid them.

## Process must be paused for inspection

**Problem**: Calling `variables_get`, `evaluate`, `stacktrace_get`, or other inspection tools while the process is running returns an error.

**Fix**: Always ensure the process is paused before inspecting. Check with `debug_state` if unsure. Pause with `debug_pause()` or wait for a breakpoint with `breakpoint_wait()`.

## Always wait after continue

**Problem**: Calling `debug_continue()` then immediately calling `variables_get()` — the process is running, not paused.

**Fix**: After `debug_continue()`, call `breakpoint_wait()` to block until the process pauses at a breakpoint. Only then inspect state.

```
debug_continue()
breakpoint_wait(timeout_ms: 30000)   # blocks until paused
variables_get()                       # now safe
```

## Set breakpoints before the code runs

**Problem**: Setting a breakpoint on a line that has already executed — the breakpoint never hits.

**Fix**: Use `stopAtEntry: true` on `debug_launch` to pause before any user code runs, then set breakpoints, then `debug_continue()`.

## Use full paths for source files

**Problem**: `breakpoint_set(file: "Program.cs", ...)` fails because the debugger can't resolve the file.

**Fix**: Use absolute paths or paths relative to the project root that match the PDB debug info. Check `debugger://source/{file}` resource for available source paths.

## Don't fabricate object references

**Problem**: Passing a made-up string to `object_inspect(object_ref: "some-ref")` — these references are opaque handles returned by other tools.

**Fix**: Get object references from `variables_get` or `evaluate` results. They look like `ref-<hex>` and are only valid for the current debug session.

## Timeout too short

**Problem**: `breakpoint_wait(timeout_ms: 5000)` times out because the breakpoint hasn't been hit yet.

**Fix**: Use longer timeouts for breakpoints that might take time to trigger. 30000ms (30 seconds) is a good default. For integration scenarios, use 60000ms or more.

## Conditional breakpoint syntax

**Problem**: Condition expression fails to evaluate.

**Fix**: Conditions are C# expressions evaluated in the debuggee context. They must return a boolean. Common issues:
- String comparisons need escaped quotes: `condition: "name == \"John\""`
- Use `&&` and `||`, not `and` / `or`
- The expression must compile against the types in scope

## Exception breakpoint type names

**Problem**: `breakpoint_set_exception(exception_type: "NullReferenceException")` doesn't work.

**Fix**: Use fully qualified type names: `"System.NullReferenceException"`. The type must be loadable in the debuggee process.

## Frame index out of range

**Problem**: `variables_get(frame_index: 10)` fails because there aren't that many frames.

**Fix**: Call `stacktrace_get()` first to see available frames. Frame 0 is the top of the stack (current method). Index up from there.

## Thread safety with thread_id

**Problem**: Using a stale or wrong thread ID.

**Fix**: Call `threads_list()` to get current thread IDs. Thread IDs can change between pauses if threads are created or destroyed.

## Roslyn tools require code_load first

**Problem**: `code_find_usages(name: "MyMethod")` returns nothing.

**Fix**: Load the workspace first: `code_load(path: "MyApp.sln")`. Roslyn tools work on source code analysis, not runtime state.

## Tracepoint message interpolation

**Problem**: Tracepoint log messages showing literal `{expression}` instead of evaluated values.

**Fix**: Expressions in `{}` must be valid C# expressions accessible in the breakpoint's scope:
- Good: `log_message: "Count: {list.Count}"`
- Bad: `log_message: "Count: {var count = list.Count; count}"` (statements not allowed)

## Detach vs terminate

**Problem**: `debug_disconnect()` kills a production process you attached to.

**Fix**: Use `debug_disconnect(terminateProcess: false)` when attached to a process you don't own. The default (`true`) terminates the debuggee.

## Snapshot IDs

**Problem**: Hardcoding snapshot IDs like `"snap-1"`.

**Fix**: Snapshot IDs are returned by `snapshot_create`. Call `snapshot_list()` to find existing snapshot IDs. The format is `snap-<guid>`.

## Large collection performance

**Problem**: `collection_analyze` on a collection with millions of elements is slow.

**Fix**: Use `preview_count` to limit how many elements are materialized. The tool provides count and statistics without enumerating the full collection, but deep inspection of many elements takes time.

## Expression evaluation side effects

**Problem**: `evaluate(expression: "list.Add(item)")` modifies the debuggee state.

**Important**: Expression evaluation runs real code in the debuggee. Expressions with side effects (Add, Remove, property setters) will modify program state. Use read-only expressions when you only want to inspect:
- Safe: `evaluate(expression: "list.Count")`
- Mutating: `evaluate(expression: "list.Clear()")` — this actually clears the list

## Tips for effective debugging

1. **Start broad, narrow down**: Use `debug_state` and `stacktrace_get` to orient before diving into variables.

2. **Use object_summarize before object_inspect**: `object_summarize` gives a quick overview; `object_inspect` gives deep detail. Start with the summary.

3. **Prefer evaluate for quick checks**: `evaluate(expression: "x > 0")` is faster than `variables_get` + manual comparison.

4. **Use tracepoints for timing-sensitive bugs**: Breakpoints alter timing. Tracepoints observe without pausing.

5. **Snapshot + diff for regression debugging**: Take a snapshot at a known-good state, reproduce the bug, take another snapshot, diff them.

6. **Check stderr for application errors**: `process_read_output(stream: "stderr")` often contains useful error output the application wrote.

7. **Use modules_search to find types**: When you don't know the full namespace, `modules_search(pattern: "*Order*")` finds matching types across all loaded modules.
