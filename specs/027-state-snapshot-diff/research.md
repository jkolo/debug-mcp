# Research: State Snapshot & Diff

**Feature**: 027-state-snapshot-diff
**Date**: 2026-02-11

## R1: Variable Capture Mechanism

**Decision**: Reuse existing `ProcessDebugger.GetVariables()` via `IDebugSessionManager`

**Rationale**: The existing variable retrieval path already handles all complexity — ICorDebug frame enumeration, PDB name resolution, async state machine variable stripping, value formatting. Snapshots should capture the same string representations agents see from `variables_get`.

**Alternatives considered**:
- Direct ICorDebug access from SnapshotService — rejected (duplicates complex frame/variable logic, violates Simplicity principle)
- Raw memory capture — rejected (spec explicitly says "string representations", and raw memory is a separate feature 038)

**Implementation**: Call `IDebugSessionManager.GetVariables(threadId, frameIndex, scope: "all")` which returns `List<Variable>`. Map each `Variable` to `SnapshotVariable` record.

## R2: Nested Object Expansion (Depth > 0)

**Decision**: Iterative expansion using existing expand mechanism

**Rationale**: The existing `variables_get` tool supports an `expand` parameter that expands a variable by path. For depth > 0, after capturing top-level variables, iterate those with `HasChildren = true` and expand them, recursively up to the specified depth.

**Alternatives considered**:
- Single ICorDebug call with depth — rejected (no such API; expansion must be done variable-by-variable)
- Parallel expansion — rejected (ICorDebug is single-threaded; calls must be sequential while paused)

**Implementation**:
1. Capture top-level variables (depth 0)
2. For each variable with `HasChildren && depth > 0`: call expand to get children
3. For each child with `HasChildren && currentDepth < maxDepth`: recurse
4. Flatten to dot-separated paths: `order.Customer.Name`

## R3: Diff Algorithm

**Decision**: Dictionary-keyed path comparison with O(n) complexity

**Rationale**: Snapshots contain string key-value pairs. Simple set operations (intersection, difference) give us added/removed/modified in a single pass. No need for tree-based diffing since variables are already flattened to paths.

**Alternatives considered**:
- Tree-based structural diff — rejected (overcomplicated for string key-value data)
- Line-by-line text diff — rejected (loses structured semantics)

**Implementation**:
```
dictA = {path: variable for variable in snapshotA.Variables}
dictB = {path: variable for variable in snapshotB.Variables}

added = keys in B not in A
removed = keys in A not in B
modified = keys in both where A[key].Value != B[key].Value
```

## R4: Storage Pattern

**Decision**: Follow BreakpointRegistry pattern — `ConcurrentDictionary<string, Snapshot>`

**Rationale**: Proven pattern in the codebase. Thread-safe without explicit locking. Simple CRUD operations. Session-scoped with event-driven cleanup.

**Alternatives considered**:
- Regular Dictionary + lock — rejected (ConcurrentDictionary is simpler for this use case)
- Separate SnapshotManager orchestrator — rejected (snapshots don't need event-driven orchestration like breakpoints; SnapshotService handles both capture logic and cleanup)

## R5: Session Cleanup

**Decision**: SnapshotService subscribes to `IProcessDebugger.StateChanged`, clears store on Disconnected

**Rationale**: Matches BreakpointManager cleanup pattern. Fire-and-forget to avoid blocking ICorDebug callback thread.

**Alternatives considered**:
- McpResourceNotifier cleanup — rejected (snapshots are not an MCP resource; cleanup belongs in SnapshotService)
- DebugSessionManager cleanup — rejected (adding snapshot awareness to session manager violates separation of concerns)

## R6: Soft Limit (100 snapshots)

**Decision**: Log warning when count reaches 100, do not reject

**Rationale**: Spec says "soft limit with a warning". Agents are expected to manage cleanup. Hard limits could break autonomous debugging loops that rely on creating many snapshots.

**Implementation**: In `SnapshotStore.Add()`, check count after adding. If >= 100, log warning via ILogger. Include warning in tool response.
