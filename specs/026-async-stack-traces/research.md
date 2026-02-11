# Research: Async Stack Traces

## R1: Async State Machine Frame Detection Strategy

**Decision**: Detect via type name pattern `^<(.+?)>d__\d+$` when method name is `MoveNext`

**Rationale**: The C# compiler generates state machine types with the pattern `<OriginalMethodName>d__N` (angle brackets, `d`, double underscore, sequence number). This pattern has been stable since C# 5.0 (2012) and is relied upon by Visual Studio, Rider, and dotnet-dump. The type name is already available in `GetMethodName` (ProcessDebugger.cs:1825-1835) via `GetTypeDefProps`. No additional ICorDebug API calls needed.

**Alternatives considered**:
- `AsyncStateMachineAttribute` lookup on the original method — rejected: requires a reverse mapping from the state machine type back to the original method token, which is the problem we're solving. The attribute is on the *original* method, not on `MoveNext`.
- `IAsyncStateMachine` interface check via `GetInterfaceImplProps` — rejected: more complex than pattern matching, and the type name already gives us the original method name via capture group 1.

## R2: Continuation Chain Walking via Task.m_continuationObject

**Decision**: Walk `Task.m_continuationObject` using existing `TryGetFieldValue` infrastructure

**Rationale**: `TryGetFieldValue` (ProcessDebugger.cs:3805-3898) already handles reference dereferencing, boxing, type hierarchy traversal, and cross-module base types. `Task.m_continuationObject` is an `internal volatile object?` field on `System.Threading.Tasks.Task`. Its value can be:
- `null` — no continuation registered
- `Action` delegate — simple continuation (read `_target` for state machine reference)
- `ContinuationTaskFromTask` or similar — continuation task (read `m_action._target`)
- `List<object>` — multiple continuations (iterate, find the state machine one)
- `ITaskCompletionAction` — completion callback

For async/await chains, the compiler generates continuations where the delegate's `_target` is the `MoveNextRunner` or the state machine itself. Reading `_target` gives us the state machine instance, from which we can read `<>1__state` and the `<>t__builder.m_task` to continue walking.

**Depth limit**: 50 frames maximum to prevent infinite loops in pathological cases.

**Alternatives considered**:
- Expression evaluation (`EvaluateAsync`) to walk chains — rejected: requires process to execute code (risky during debugging), much slower (ICorDebugEval round-trips), and may have side effects.
- Raw memory reading via `memory_read` tool — rejected: requires knowledge of internal layout offsets which vary by runtime version; fragile.

## R3: PDB StateMachineHoistedLocalScopes Support

**Decision**: Use `MetadataReader.GetCustomDebugInformation()` to read `StateMachineHoistedLocalScopes`

**Rationale**: Portable PDBs for Debug builds contain custom debug info blobs for each method. The `StateMachineHoistedLocalScopes` kind (GUID `6DA9A61E-F8C7-4874-BE62-68BC5630DF71`) maps compiler-generated state machine field slots to the original local variable scopes. PdbSymbolReader.cs already uses `System.Reflection.Metadata` (`MetadataReader`) for local variable resolution. The API `reader.GetCustomDebugInformation(methodDefHandle)` returns all custom debug info entries, which can be filtered by kind GUID.

The blob structure is a sequence of `(StartOffset: uint32, Length: uint32)` pairs defining the IL ranges where each hoisted local is in scope.

**Alternatives considered**:
- Heuristic name stripping (`<result>5__2` → `result`) — accepted as **fallback** when PDB info unavailable (Release builds), but not primary strategy since it loses scope information.
- ISymUnmanagedReader — rejected: not available on all platforms (Windows-only COM API), and PdbSymbolReader already uses the portable/cross-platform approach.

## R4: ValueTask Continuation Chain Feasibility

**Decision**: Best-effort via `_obj` field extraction; mark IValueTaskSource chains as unresolvable

**Rationale**: `ValueTask<T>` is a struct with two relevant fields: `_obj` (object — either `Task<T>` or `IValueTaskSource<T>`) and `_result` (T — for synchronously completed results). When the compiler uses `AsyncValueTaskMethodBuilder<T>`, it creates a `Task<T>` internally in most async paths. Reading `_obj` and checking if it's a `Task` allows standard chain walking. For `IValueTaskSource` implementations (e.g., `ManualResetValueTaskSourceCore`), the continuation mechanism is implementation-specific and not walkable via a generic approach.

**Alternatives considered**:
- Full `IValueTaskSource` continuation support — rejected for P1: too many implementation variants (`PoolingAsyncValueTaskMethodBuilder`, `Channel<T>`, custom sources), each with different internal field layouts. Can be added incrementally if demand exists.

## R5: Existing Infrastructure Assessment

**Decision**: Reuse existing ProcessDebugger field inspection; create one new service class

**Verification**: Key existing methods confirmed as reusable:

| Method | Location | Purpose for this feature |
|--------|----------|--------------------------|
| `TryGetFieldValue` | ProcessDebugger.cs:3805-3898 | Read Task fields, state machine fields |
| `GetMethodName` | ProcessDebugger.cs:1825-1835 | Already resolves type + method name (extend for MoveNext detection) |
| `GetThisReference` | ProcessDebugger.cs:1936-1964 | Get state machine `this` in MoveNext frame |
| `CreateStackFrame` | ProcessDebugger.cs:1743-1818 | Entry point for frame creation (extend with async detection) |
| `InspectObjectValueCore` | ProcessDebugger.cs:4315-4361 | Field enumeration pattern (reuse for continuation objects) |
| `GetLocalVariableNamesAsync` | PdbSymbolReader.cs:384-432 | Local name resolution (extend for state machine locals) |

The existing backing field pattern (`<PropertyName>k__BackingField` handling in ProcessDebugger.cs:3925-3932) confirms the codebase already deals with compiler-generated angle-bracket names.

**Risk**: `TryGetFieldValue` is a private method on `ProcessDebugger`. The new `AsyncStackTraceService` needs access. Options: (a) make it internal, (b) pass as delegate, (c) extract to shared helper. Option (a) is simplest — already have `InternalsVisibleTo` for tests.
