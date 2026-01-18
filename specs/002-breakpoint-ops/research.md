# Research: Breakpoint Operations

**Feature**: 002-breakpoint-ops
**Date**: 2026-01-17
**Depends On**: 001-debug-session research

## Executive Summary

This research covers ICorDebug breakpoint APIs for implementing MCP breakpoint tools:
1. Breakpoint creation at IL offsets
2. Source-to-IL mapping via portable PDB
3. Conditional breakpoint evaluation
4. Pending breakpoints for unloaded modules
5. Breakpoint hit event handling

All technical decisions align with the Constitution's Native First principle, building on
the ICorDebug infrastructure established in 001-debug-session.

---

## 1. ICorDebugCode.CreateBreakpoint()

### Decision: Use ICorDebugCode.CreateBreakpoint (not ICorDebugFunction.CreateBreakpoint)

**Rationale**: `ICorDebugCode.CreateBreakpoint()` works even when the debuggee is running,
automatically pausing/resuming as needed. `ICorDebugFunction.CreateBreakpoint()` requires
the debuggee to be stopped.

### API Signature

```cpp
HRESULT CreateBreakpoint(
    [in] ULONG32 offset,                              // IL offset
    [out] ICorDebugFunctionBreakpoint **ppBreakpoint  // Result
);
```

### Usage Flow

```csharp
// 1. Get function from module
var function = module.GetFunctionFromToken(methodToken);

// 2. Get IL code
var code = function.ILCode;

// 3. Create breakpoint at IL offset
var breakpoint = code.CreateBreakpoint(ilOffset);

// 4. Activate
breakpoint.Activate(true);
```

### ICorDebugFunctionBreakpoint Interface

| Method | Purpose |
|--------|---------|
| `Activate(bool)` | Enable/disable breakpoint (false = soft delete) |
| `IsActive()` | Query current activation state |
| `GetFunction()` | Get the ICorDebugFunction |
| `GetOffset()` | Get the IL offset |

---

## 2. Source Location to IL Offset Mapping

### Decision: Use System.Reflection.Metadata for Portable PDB

**Rationale**: Modern .NET uses portable PDB format. System.Reflection.Metadata provides
thread-safe, cross-platform reading without COM interop overhead.

**Alternative considered**: ISymUnmanagedReader (legacy COM interface) - rejected due to
Windows-only heritage and complexity.

### Mapping Flow

```csharp
using System.Reflection.Metadata;

// 1. Load portable PDB
var pdbReader = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
var metadata = pdbReader.GetMetadataReader();

// 2. Get method debug info (from method token)
int methodIndex = methodToken & 0xFFFFFF;
var methodDebugHandle = MetadataTokens.MethodDebugInformationHandle(methodIndex);
var debugInfo = metadata.GetMethodDebugInformation(methodDebugHandle);

// 3. Find sequence point matching source line
foreach (var sp in debugInfo.GetSequencePoints())
{
    if (sp.Document == targetDocument && sp.StartLine == targetLine)
    {
        return sp.Offset;  // IL offset
    }
}
```

### Sequence Point Structure

| Field | Description |
|-------|-------------|
| `Offset` | IL offset in method |
| `StartLine` | Source line start (1-based) |
| `StartColumn` | Source column start (1-based) |
| `EndLine` | Source line end |
| `EndColumn` | Source column end |
| `Document` | Handle to source document |

### Column-Level Breakpoints (Lambda Targeting)

When a line contains multiple statements (e.g., lambdas), use column position to select
the specific sequence point:

```csharp
// Find sequence point matching specific column
SequencePoint? FindSequencePoint(
    MethodDebugInformation debugInfo,
    int targetLine,
    int? targetColumn)
{
    var candidates = debugInfo.GetSequencePoints()
        .Where(sp => sp.StartLine == targetLine && !sp.IsHidden)
        .OrderBy(sp => sp.StartColumn)
        .ToList();

    if (candidates.Count == 0) return null;
    if (targetColumn == null) return candidates.First(); // Use first on line

    // Find sequence point containing the column
    return candidates.FirstOrDefault(sp =>
        sp.StartColumn <= targetColumn && targetColumn <= sp.EndColumn)
        ?? candidates.OrderBy(sp => Math.Abs(sp.StartColumn - targetColumn.Value)).First();
}
```

When invalid column specified, return available sequence points so client can choose:

```json
{
  "success": false,
  "error": {
    "code": "INVALID_COLUMN",
    "message": "No sequence point at column 25",
    "availableSequencePoints": [
      { "startColumn": 5, "endColumn": 20, "description": "Method call" },
      { "startColumn": 30, "endColumn": 45, "description": "Lambda body" }
    ]
  }
}
```

### Invalid Line Handling

When a requested line doesn't map to executable code:
1. Search for nearest valid line within same method
2. If found, use that offset and report the adjusted line
3. If no valid line in method, return error with suggestion

---

## 3. Conditional Breakpoint Evaluation

### Decision: Support Simple Local Evaluation with ICorDebugEval Fallback

**Rationale**: Simple expressions (e.g., `i > 5`) can be evaluated locally for performance.
Complex expressions requiring debuggee state use ICorDebugEval.

### Two-Tier Strategy

**Tier 1: Local Evaluation** (Fast path)
- Parse condition expression
- If condition only involves literals and simple comparisons
- Evaluate without touching debuggee
- Example: `hitCount > 10`, `true`, `false`

**Tier 2: ICorDebugEval** (Full evaluation)
- Create evaluator on hit thread: `thread.CreateEval(out eval)`
- Call condition getter function
- Wait for `EvalComplete` callback
- Read result value

### ICorDebugEval Requirements

| Requirement | Reason |
|-------------|--------|
| Thread at GC safe point | CLR can perform garbage collection |
| Thread at FuncEval safe point | CLR can hijack thread for evaluation |
| Not in native code | CLR cannot control native execution |
| Not holding locks | May deadlock if evaluated code needs same lock |

### Error Handling

```csharp
// On condition syntax error
return new SetBreakpointResult
{
    Success = false,
    Error = new ErrorResponse
    {
        Code = "INVALID_CONDITION",
        Message = "Condition expression has syntax error at position 5"
    }
};

// On evaluation failure (undefined variable)
// Don't crash debuggee - report error, continue execution
breakpoint.ConditionError = "Variable 'x' not found in scope";
// Resume debuggee
process.Continue(false);
```

---

## 4. Pending Breakpoints

### Decision: Implement Pending Breakpoint Registry

**Rationale**: Breakpoints may be set before the target module loads (common in startup
debugging scenarios). Store pending breakpoints and bind on `LoadModule` event.

### Breakpoint States

| State | Description | Action |
|-------|-------------|--------|
| **Pending** | Source location specified, module not loaded | Wait for LoadModule |
| **Bound** | IL offset resolved, native break opcode injected | Active and monitored |
| **Unbound** | Module unloaded | Revert to pending |
| **Disabled** | Explicitly deactivated | `Activate(false)` called |

### Registry Structure

```csharp
class PendingBreakpoint
{
    string Id;                          // Unique identifier
    string SourceFile;                  // Target source file
    int Line;                           // Target line number
    string? Condition;                  // Optional condition
    bool Enabled;                       // User-controlled enable state
    BreakpointState State;              // Pending/Bound/Disabled
    ICorDebugFunctionBreakpoint? Bound; // Null if pending
}
```

### LoadModule Handler

```csharp
void OnModuleLoad(ICorDebugModule module)
{
    foreach (var pending in _pendingBreakpoints.Where(p => p.State == Pending))
    {
        if (ModuleContainsSource(module, pending.SourceFile))
        {
            try
            {
                var ilOffset = ResolveSourceToIL(module, pending.SourceFile, pending.Line);
                var code = GetCodeForOffset(module, ilOffset);
                var bp = code.CreateBreakpoint(ilOffset);
                bp.Activate(pending.Enabled);

                pending.Bound = bp;
                pending.State = BreakpointState.Bound;

                NotifyBreakpointVerified(pending.Id);
            }
            catch (SymbolException)
            {
                // Keep as pending, maybe loaded later
            }
        }
    }
}
```

---

## 5. Breakpoint Hit Event Handling

### ICorDebugManagedCallback.Breakpoint

```cpp
void Breakpoint(
    ICorDebugAppDomain *pAppDomain,
    ICorDebugThread *pThread,
    ICorDebugBreakpoint *pBreakpoint
);
```

### Processing Flow

```csharp
void OnBreakpoint(ICorDebugAppDomain domain, ICorDebugThread thread, ICorDebugBreakpoint bp)
{
    // 1. Find our breakpoint entry
    var entry = _registry.FindByCorDebugBreakpoint(bp);
    if (entry == null)
    {
        // Unknown breakpoint - continue
        _process.Continue(false);
        return;
    }

    // 2. Evaluate condition (if any)
    if (entry.HasCondition)
    {
        bool conditionMet = EvaluateCondition(thread, entry.Condition);
        if (!conditionMet)
        {
            _process.Continue(false);
            return;  // Silent continue
        }
    }

    // 3. Increment hit count
    entry.HitCount++;

    // 4. Collect context
    var frames = GetStackFrames(thread);
    var location = GetSourceLocation(frames[0]);

    // 5. Queue hit event for MCP client
    _hitQueue.Enqueue(new BreakpointHit
    {
        BreakpointId = entry.Id,
        ThreadId = thread.GetID(),
        Location = location,
        HitCount = entry.HitCount
    });

    // 6. Update session state to paused
    _sessionState = SessionState.Paused;

    // IMPORTANT: Do NOT call Continue() - wait for client action
}
```

### Critical: Continue() Requirement

The debuggee remains suspended until `Continue()` is called. For breakpoint_wait:
- Wait tool returns when hit is queued
- Client decides next action (inspect variables, step, continue)
- Client must eventually continue or disconnect

---

## 6. Exception Breakpoints

### Decision: Use ICorDebugManagedCallback2.Exception for Exception Breakpoints

**Rationale**: ICorDebugManagedCallback2.Exception provides first-chance and second-chance
exception notifications without requiring explicit breakpoints. Filter by exception type
in the callback handler.

### ICorDebugManagedCallback2.Exception Callback

```cpp
HRESULT Exception(
    ICorDebugAppDomain *pAppDomain,
    ICorDebugThread *pThread,
    ICorDebugFrame *pFrame,
    ULONG32 nOffset,
    CorDebugExceptionCallbackType dwEventType,  // DEBUG_EXCEPTION_FIRST_CHANCE, etc.
    DWORD dwFlags
);
```

### CorDebugExceptionCallbackType Values

| Value | Description |
|-------|-------------|
| `DEBUG_EXCEPTION_FIRST_CHANCE` | Exception thrown, before any catch handlers run |
| `DEBUG_EXCEPTION_USER_FIRST_CHANCE` | First chance in user code |
| `DEBUG_EXCEPTION_CATCH_HANDLER_FOUND` | A catch handler will run |
| `DEBUG_EXCEPTION_UNHANDLED` | No catch handler, process will terminate |

### Exception Breakpoint Registry

```csharp
class ExceptionBreakpoint
{
    string Id;                     // Unique identifier
    string ExceptionTypeName;      // e.g., "System.NullReferenceException"
    bool BreakOnFirstChance;       // Break when thrown
    bool BreakOnSecondChance;      // Break when unhandled
    bool IncludeSubtypes;          // Match derived types
    bool Enabled;
}
```

### Exception Handler Implementation

```csharp
void OnException(
    ICorDebugAppDomain domain,
    ICorDebugThread thread,
    ICorDebugFrame frame,
    uint offset,
    CorDebugExceptionCallbackType eventType,
    uint flags)
{
    // 1. Get exception object from thread
    var exceptionValue = thread.GetCurrentException();
    var exceptionType = GetTypeName(exceptionValue);

    // 2. Check against registered exception breakpoints
    foreach (var eb in _exceptionBreakpoints.Where(e => e.Enabled))
    {
        bool typeMatches = eb.IncludeSubtypes
            ? IsAssignableTo(exceptionType, eb.ExceptionTypeName)
            : exceptionType == eb.ExceptionTypeName;

        if (!typeMatches) continue;

        bool shouldBreak =
            (eventType == DEBUG_EXCEPTION_FIRST_CHANCE && eb.BreakOnFirstChance) ||
            (eventType == DEBUG_EXCEPTION_UNHANDLED && eb.BreakOnSecondChance);

        if (shouldBreak)
        {
            // Queue exception hit event
            _hitQueue.Enqueue(new BreakpointHit
            {
                BreakpointId = eb.Id,
                ThreadId = thread.GetID(),
                ExceptionInfo = new ExceptionInfo
                {
                    Type = exceptionType,
                    Message = GetExceptionMessage(exceptionValue),
                    IsFirstChance = eventType == DEBUG_EXCEPTION_FIRST_CHANCE
                }
            });

            _sessionState = SessionState.Paused;
            return; // Do NOT call Continue()
        }
    }

    // No matching exception breakpoint - continue
    _process.Continue(false);
}
```

### Exception Type Resolution

For pending exception breakpoints (type not yet loaded):
- Store type name as string
- Validate type exists when assembly loads
- If type never found, mark breakpoint as unverified

---

## 7. Duplicate Breakpoint Handling

### Decision: Return Existing Breakpoint ID for Same Location

**Rationale**: Setting the same breakpoint twice should be idempotent per MCP Compliance
principle. Return the existing breakpoint rather than creating a duplicate.

### Implementation

```csharp
BreakpointSetResult SetBreakpoint(string file, int line, string? condition)
{
    // Check for existing
    var existing = _registry.FindByLocation(file, line);
    if (existing != null)
    {
        // Update condition if different
        if (condition != existing.Condition)
        {
            existing.Condition = condition;
        }
        return new BreakpointSetResult
        {
            Id = existing.Id,
            Verified = existing.State == Bound,
            Message = "Breakpoint already exists at this location"
        };
    }

    // Create new...
}
```

---

## 8. Dependencies

| Package | Purpose | Version |
|---------|---------|---------|
| `ClrDebug` | ICorDebug wrappers | Latest |
| `System.Reflection.Metadata` | Portable PDB reading | 10.0+ (in-box) |
| `ModelContextProtocol` | MCP server SDK | 0.6.0+ |

---

## 9. Error Codes

| Code | Description |
|------|-------------|
| `NO_SESSION` | No active debug session |
| `INVALID_FILE` | Source file not found in any loaded module |
| `INVALID_LINE` | Line number does not contain executable code |
| `INVALID_CONDITION` | Condition expression has syntax error |
| `BREAKPOINT_NOT_FOUND` | Breakpoint ID does not exist |
| `EVAL_FAILED` | Condition evaluation failed at runtime |
| `MODULE_NOT_LOADED` | Breakpoint pending (module not yet loaded) |

---

## Sources

- [ICorDebugCode.CreateBreakpoint - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/debugging/icordebugcode-interface1)
- [ICorDebugManagedCallback::Breakpoint - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/unmanaged-api/debugging/icordebug/icordebugmanagedcallback-breakpoint-method)
- [System.Reflection.Metadata SequencePoint - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.sequencepoint)
- [Portable PDB Specification - GitHub](https://github.com/dotnet/core/blob/main/Documentation/diagnostics/portable_pdb.md)
- [How do Managed Breakpoints work? - Microsoft Blog](https://learn.microsoft.com/en-us/archive/blogs/jmstall/how-do-managed-breakpoints-work)
- [Writing a .NET Debugger (part 4) - Breakpoints](https://lowleveldesign.wordpress.com/2010/12/01/writing-a-net-debugger-part-4-breakpoints/)
- [ClrDebug - GitHub](https://github.com/lordmilko/ClrDebug)
