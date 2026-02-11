# Quickstart: Async Stack Trace Verification

## Prerequisites

- .NET 10 SDK installed
- Git checkout on `026-async-stack-traces` branch

## Step 1: Build

```bash
dotnet build
```

Expected: 0 errors, 0 warnings.

## Step 2: Run unit + contract tests

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
```

Expected: All tests pass, including new `AsyncStackTraceServiceTests` and `AsyncStackTraceContractTests`.

## Step 3: Verify async frame detection (manual)

1. Start debug-mcp with a test app that has async methods
2. Set a breakpoint inside an async method
3. When paused, call `stacktrace_get`
4. Verify:
   - Top frame shows original method name (not `<MethodName>d__N.MoveNext()`)
   - Frame includes `frame_kind: "async"`
   - Calling async frames show `is_awaiting: true`

## Step 4: Verify continuation chain

1. Create a test app with: `Main` → `MethodA` → `MethodB` (all async)
2. Set breakpoint in `MethodB`
3. Call `stacktrace_get`
4. Verify logical stack shows all three methods in order, even though `Main` and `MethodA` are not on the physical thread stack

## Step 5: Verify include_raw parameter

```
stacktrace_get(include_raw: true)
```

Expected: Response includes both `frames` (logical) and `raw_frames` (physical, including MoveNext and thread pool internals).

## Step 6: Verify backward compatibility

1. Call `stacktrace_get` without new parameters
2. Verify response still contains: `success`, `thread_id`, `total_frames`, `frames[]`
3. Verify each frame still has: `index`, `function`, `module`, `is_external`
4. New fields (`frame_kind`, `is_awaiting`, `logical_function`) are present but do not break existing consumers

## Step 7: Verify state machine variable names (US3)

1. Pause inside async method with local variables
2. Call `variables_get` on that frame
3. Verify variables show original source names (e.g., `result` not `<result>5__2`)
