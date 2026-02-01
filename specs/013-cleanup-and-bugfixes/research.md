# Research: Cleanup, Bug Fixes, and Remaining Work

## R1: ProcessWaitState.TryReapChild FailFast on Linux

**Decision**: Investigate process lifecycle ordering before considering test isolation.

**Rationale**: The `FailFast` occurs because .NET's `ProcessWaitState` tries to `waitpid()` on a child that ICorDebug already reaped via ptrace. The fix should ensure ICorDebug releases the process before .NET's `System.Diagnostics.Process` attempts cleanup.

**Alternatives considered**:
- Move test to separate assembly — heavy-handed, doesn't fix the underlying issue
- Suppress FailFast via environment variable (`COMPlus_DbgMiniDumpName`) — masks the problem
- Proper cleanup ordering in test Dispose — preferred approach

## R2: PDB Local Variable Name Resolution via System.Reflection.Metadata

**Decision**: Use `System.Reflection.Metadata` (already referenced in project) to read `LocalVariable` table from portable PDB.

**Rationale**: The existing `PdbSymbolReader` uses `System.Reflection.Metadata` for source location lookup but doesn't read local variable names. The API path is: `MetadataReader` → `MethodDebugInformation` → `GetLocalScopes()` → `LocalScope.GetLocalVariables()` → `LocalVariable.Name`.

**Alternatives considered**:
- ICorDebugCode.GetILToNativeMapping + custom PDB parser — unnecessary complexity
- Use Roslyn scripting to resolve names — overkill for slot-to-name mapping
- Use `System.Reflection.Metadata` directly — chosen, minimal new code

## R3: ICorDebugEval FuncEval for Condition Evaluation

**Decision**: Implement a simple expression parser + ICorDebugEval-based evaluator, chained after SimpleConditionEvaluator.

**Rationale**: ICorDebugEval provides `CallFunction()` and `NewString()` which can evaluate property getters and method calls on the debuggee's thread. The expression subset (comparisons with property access) covers the vast majority of real-world conditional breakpoint use cases without needing a full C# expression compiler.

**Alternatives considered**:
- Roslyn scripting engine — too heavy, requires compilation on each hit
- Mono.Cecil expression evaluator — wrong abstraction layer
- Simple regex-based parser + ICorDebugEval — chosen, sufficient for the defined expression subset

## R4: Asciinema Recording Workflow

**Decision**: Manual recording with `asciinema rec --idle-time-limit 2`. Embed via existing `AsciinemaPlayer` React component.

**Rationale**: Recordings are manual by nature (interactive terminal). The `AsciinemaPlayer` component from spec 012 already handles lazy loading, theme support, and poster frames.

**Alternatives considered**:
- Scripted recording with expect/tmux — fragile, hard to maintain
- Video recording — larger files, worse accessibility
- Manual asciinema — chosen, simplest and most maintainable
