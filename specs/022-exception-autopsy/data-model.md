# Data Model: Exception Autopsy

## New Models

### ExceptionAutopsyResult

Top-level bundled response from the autopsy tool.

| Field | Type | Description |
|-------|------|-------------|
| threadId | int | Thread where exception occurred |
| exception | ExceptionDetail | Primary exception details |
| innerExceptions | InnerExceptionEntry[] | Chain of inner exceptions (depth-capped) |
| innerExceptionsTruncated | bool | True if chain was capped before reaching null |
| frames | AutopsyFrame[] | Stack frames from the exception thread |
| totalFrames | int | Total frames available (may exceed returned count) |
| throwingFrameIndex | int | Index of the frame that threw the exception (usually 0) |

### ExceptionDetail

Core exception information from the current exception.

| Field | Type | Description |
|-------|------|-------------|
| type | string | Full exception type name (e.g., "System.NullReferenceException") |
| message | string | Exception message |
| isFirstChance | bool | True if first-chance, false if unhandled |
| stackTraceString | string? | Runtime's StackTrace property value (may be null if unavailable) |

### InnerExceptionEntry

One level of the inner exception chain.

| Field | Type | Description |
|-------|------|-------------|
| type | string | Full type name of the inner exception |
| message | string | Inner exception message |
| depth | int | Nesting depth (1 = first InnerException, 2 = InnerException.InnerException, ...) |

### AutopsyFrame

Stack frame enriched with optional variable data.

| Field | Type | Description |
|-------|------|-------------|
| index | int | Frame index (0 = top of stack) |
| function | string | Function name |
| module | string | Module name |
| isExternal | bool | True if no source/symbols available |
| location | SourceLocation? | File, line, column (null if no symbols) |
| arguments | Variable[]? | Function arguments (null if unavailable) |
| variables | FrameVariables? | Local variables (only for frames within `include_variables_for_frames` depth) |

### FrameVariables

Variables for a single stack frame with per-variable status.

| Field | Type | Description |
|-------|------|-------------|
| locals | Variable[] | Successfully retrieved local variables (one-level expanded) |
| errors | VariableError[]? | Variables that failed to inspect |

### VariableError

Error marker for a variable that could not be inspected.

| Field | Type | Description |
|-------|------|-------------|
| name | string | Variable name |
| error | string | Error message describing why inspection failed |

## Existing Models (Reused)

- **Variable** (`Models/Inspection/Variable.cs`) — Name, Type, Value, Scope, HasChildren, ChildrenCount, Path
- **SourceLocation** (`Models/SourceLocation.cs`) — File, Line, Column, FunctionName, ModuleName
- **ExceptionInfo** (`Models/Breakpoints/ExceptionInfo.cs`) — Type, Message, IsFirstChance, StackTrace
- **BreakpointHit** (`Models/Breakpoints/BreakpointHit.cs`) — includes ExceptionInfo? field

## Relationships

```
ExceptionAutopsyResult
├── ExceptionDetail (1:1)
├── InnerExceptionEntry[] (1:N, max depth capped)
└── AutopsyFrame[] (1:N, max frames capped)
    ├── SourceLocation? (1:0..1)
    ├── Variable[]? arguments (1:0..N)
    └── FrameVariables? (1:0..1)
        ├── Variable[] locals (1:N)
        └── VariableError[]? (1:0..N)
```

## State Transitions

No new state transitions. The autopsy tool is read-only — it inspects existing paused state without modifying it. The debugger must already be in `SessionState.Paused` with `PauseReason.Exception` or `PauseReason.Breakpoint` (with ExceptionInfo present).
