# Research: Collection & Object Summarizer

**Feature**: 028-collection-object-summarizer
**Date**: 2026-02-11

## R1: How to Access Collection Elements via ICorDebug

### Decision
Hybrid strategy: direct array access for `CorDebugArrayValue`, internal field access for `List<T>`, ICorDebugEval function calls for other collection types.

### Rationale
- **Arrays**: `CorDebugArrayValue.GetElementAtPosition(i)` provides O(1) direct access without resuming the debuggee. Already used in `ObjectInspectTool` for array inspection.
- **List\<T\>**: Internal `_items` field is a `T[]` array — read it via `GetFieldValue("_items")`, then use `GetElementAtPosition`. The `_size` field gives the actual count (vs `_items.Length` which is capacity). These field names have been stable since .NET Framework 2.0.
- **Dictionary\<K,V\>**: Internal structure uses `_entries` array of `Entry` structs with `key`/`value` fields. We can read `_count` and iterate `_entries[0..count-1]`, skipping entries where `hashCode < 0` (deleted). However, this is more fragile — fallback to `get_Item` eval if field layout differs.
- **HashSet\<T\>, Queue\<T\>, Stack\<T\>**: Each has internal arrays (`_slots`/`_array`). Use eval-based access for simplicity — these are less commonly huge.

### Alternatives Considered
1. **Call `.ToArray()` via eval**: Materializes the entire collection into a new array, which we can then inspect. Pros: works for any `IEnumerable<T>`. Cons: allocates a full copy in the debuggee's heap; for 100K elements this is significant.
2. **Call `GetEnumerator()` + `MoveNext()` + `Current` in a loop via eval**: Follows the standard iteration pattern. Cons: requires N×3 eval calls — far too slow.
3. **Use `ICorDebugEval2.NewParameterizedArray` to create a temp array and copy**: Over-engineered for this use case.

## R2: How to Detect Collection Types

### Decision
Type name prefix matching against a static dictionary of known BCL collection types, with fallback to `Count` property existence check.

### Rationale
- Known types: `System.Collections.Generic.List`1`, `Dictionary`2`, `HashSet`1`, etc. have stable fully-qualified names. A prefix match on the type name from `GetTypeName()` is fast (no eval needed).
- For custom collections: check if `FindPropertyGetter(value, "Count")` returns non-null. If it does, and the type also has `get_Item`, treat as an indexed collection. If only `Count` exists, treat as a counted-but-not-indexed collection (e.g., `HashSet<T>`).
- We classify into `CollectionKind` enum: `Array`, `List`, `Dictionary`, `Set`, `Queue`, `Stack`, `Other`.

### Alternatives Considered
1. **Check implemented interfaces via metadata**: `metaImport.EnumInterfaceImpls()` can find `ICollection<T>`, `IEnumerable<T>`. Pros: accurate. Cons: requires walking the type hierarchy (interface impls may be on base types), more complex code.
2. **Only use eval-based detection**: Call `.Count` and catch failure. Cons: slow and noisy (eval exceptions logged).

## R3: Numeric Statistics Computation

### Decision
Iterate elements, extract primitive values via `CorDebugGenericValue`, compute min/max/sum in a single pass. For sampled collections, report that statistics are approximate.

### Rationale
- `CorDebugGenericValue` exposes the raw bytes of primitive types. We can read `int`, `long`, `float`, `double`, `decimal` directly without eval.
- Single-pass: track `min`, `max`, `sum`, `count` as we enumerate.
- For arrays of primitives, this is very fast (direct memory access).
- For `List<T>` of primitives, read the backing `_items` array and iterate.

### Alternatives Considered
1. **Eval-based LINQ**: Call `.Min()`, `.Max()`, `.Average()` via eval. Cons: 3 eval calls, each iterates the full collection in the debuggee. Slower and allocates LINQ iterators.

## R4: Object Summarizer "Interesting" Value Detection

### Decision
Static heuristic rules applied during field enumeration:

| Value Pattern | Flag | Rationale |
|---------------|------|-----------|
| Null reference | Listed in `nullFields` | Missing data |
| `""` (empty string) | `empty_string` | Usually a bug — uninitialized or missing input |
| `NaN` | `nan` | Arithmetic error |
| `Infinity` / `-Infinity` | `infinity` | Division by zero |
| `0001-01-01T00:00:00` (default DateTime/DateTimeOffset) | `default_datetime` | Uninitialized .NET datetime |
| `Guid.Empty` | `default_guid` | Uninitialized identifier |

Intentionally NOT flagged: `0` for int/long (too common), `false` for bool (normal default), `null` for `Nullable<T>` (already in null list).

### Rationale
The goal is to surface likely bugs without overwhelming with noise. The heuristics target values that are almost always unintentional in production code.

## R5: Response Size / Token Budget

### Decision
Default `maxPreviewItems = 5`. Element previews use `FormatValue()` output (already truncated at 100 chars for strings). Collection summary adds minimal overhead (~50 tokens for metadata). Total for a 1,000-element collection: ~100 tokens (metadata + 10 preview values + stats).

### Rationale
- SC-004 requires <500 tokens for 1,000 elements.
- With 5 first + 5 last preview elements at ~10 tokens each = 100 tokens.
- Statistics (min/max/avg) = ~15 tokens.
- Metadata (count, type, null count) = ~20 tokens.
- Total: ~135 tokens — well within budget.

## R6: Concurrency and Lock Safety

### Decision
Both new services access the debugger through `IDebugSessionManager`, which internally acquires `_lock` on `ProcessDebugger`. No new locks needed. `CallFunctionAsync` is already thread-safe and awaitable.

### Rationale
The lock ordering invariant (`_lock` → `_stateLock`) is maintained because new code only enters through the existing `_lock` acquisition in `ProcessDebugger` methods. No callbacks are involved — these are user-initiated reads.

### Risk
`CallFunctionAsync` resumes the debuggee briefly to execute the getter. If the getter throws or deadlocks, the eval times out (configurable). This is the same risk as `evaluate` tool — acceptable and already mitigated by timeout.
