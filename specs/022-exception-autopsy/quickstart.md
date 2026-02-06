# Quickstart: Exception Autopsy

## What this feature adds

A new MCP tool `exception_get_context` that bundles all exception diagnosis data into a single call. Also extends `breakpoint_wait` with an optional `include_autopsy` parameter.

## New files to create

```
DebugMcp/
├── Models/
│   └── Inspection/
│       ├── ExceptionAutopsyResult.cs    # Top-level result record
│       ├── ExceptionDetail.cs           # Exception type/message/status
│       ├── InnerExceptionEntry.cs       # Inner exception chain entry
│       ├── AutopsyFrame.cs              # Frame with optional variables
│       ├── FrameVariables.cs            # Variables + errors for a frame
│       └── VariableError.cs             # Error marker for failed variable
├── Services/
│   ├── IExceptionAutopsyService.cs      # Interface
│   └── ExceptionAutopsyService.cs       # Implementation
└── Tools/
    └── ExceptionGetContextTool.cs       # MCP tool wrapper

tests/DebugMcp.Tests/
└── Unit/
    └── Inspection/
        └── ExceptionAutopsyServiceTests.cs  # Unit tests
```

## Files to modify

```
DebugMcp/
├── Tools/
│   └── BreakpointWaitTool.cs            # Add include_autopsy parameter
└── Program.cs                           # Register IExceptionAutopsyService in DI
```

## Implementation order

1. **Models** — Define all record types (no dependencies)
2. **IExceptionAutopsyService** — Interface with `GetExceptionContextAsync` method
3. **ExceptionAutopsyService** — Core logic: check pause reason, get exception info, get stack frames, get variables, walk inner exceptions
4. **ExceptionGetContextTool** — MCP tool wrapper (thin, delegates to service)
5. **DI Registration** — Register service in Program.cs
6. **BreakpointWaitTool extension** — Add `include_autopsy` parameter, call autopsy service when exception hit
7. **Unit tests** — Test service logic with mocked IProcessDebugger and IDebugSessionManager
8. **Contract tests** — Verify tool schema matches contract JSON

## Key integration points

- `IProcessDebugger.CurrentPauseReason` → detect exception state
- `IProcessDebugger.EvaluateAsync("$exception.InnerException...")` → walk inner chain
- `IDebugSessionManager.GetStackFrames()` → get structured frames
- `IDebugSessionManager.GetVariables()` → get locals per frame
- `IProcessDebugger.ActiveThreadId` → get exception thread
