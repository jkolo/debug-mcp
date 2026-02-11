# Data Model: State Snapshot & Diff

**Feature**: 027-state-snapshot-diff
**Date**: 2026-02-11

## Entities

### Snapshot

A point-in-time capture of debug state for a single stack frame.

```
Snapshot
├── Id: string              # "snap-{guid}" — unique identifier
├── Label: string           # User-provided or auto-generated ("snapshot-1")
├── CreatedAt: DateTimeOffset  # UTC timestamp of capture
├── ThreadId: int           # Thread where snapshot was taken
├── FrameIndex: int         # Stack frame index (0 = top)
├── FunctionName: string    # Fully qualified method name at capture point
├── Depth: int              # Expansion depth used (0 = top-level only)
└── Variables: List<SnapshotVariable>  # Captured variable values
```

**Identity**: `Id` (snap-{guid})
**Lifecycle**: Created on `snapshot_create`, immutable after creation, deleted on `snapshot_delete` or session disconnect
**Storage**: In-memory `ConcurrentDictionary<string, Snapshot>`

### SnapshotVariable

A single captured variable within a snapshot.

```
SnapshotVariable
├── Name: string            # Variable name (e.g., "retryCount")
├── Path: string            # Dot-separated path (e.g., "order.Customer.Name")
├── Type: string            # CLR type name (e.g., "System.Int32")
├── Value: string           # String representation (same format as variables_get)
├── Scope: VariableScope    # Local, Argument, This, Field, Property, Element
└── Children: List<SnapshotVariable>?  # Expanded children (when depth > 0)
```

**Identity**: Unique within a snapshot by `Path`
**Lifecycle**: Created with parent Snapshot, immutable

### SnapshotDiff

The result of comparing two snapshots.

```
SnapshotDiff
├── SnapshotIdA: string     # First snapshot ID (baseline)
├── SnapshotIdB: string     # Second snapshot ID (comparison)
├── Added: List<DiffEntry>  # Variables in B not in A
├── Removed: List<DiffEntry>  # Variables in A not in B
├── Modified: List<DiffEntry>  # Variables in both with different values
├── ThreadMismatch: bool    # True if snapshots from different threads
└── TimeDelta: TimeSpan     # Time elapsed between snapshots
```

**Identity**: Computed on-demand, not stored
**Lifecycle**: Ephemeral — created per `snapshot_diff` call

### DiffEntry

A single change within a diff.

```
DiffEntry
├── Name: string            # Variable name
├── Path: string            # Full dot-separated path
├── Type: string            # CLR type name
├── OldValue: string?       # Value in snapshot A (null for added)
├── NewValue: string?       # Value in snapshot B (null for removed)
└── ChangeType: DiffChangeType  # Added, Removed, Modified
```

**Identity**: Unique within a SnapshotDiff by `Path`

### DiffChangeType (enum)

```
DiffChangeType
├── Added      # Variable exists in B but not A
├── Removed    # Variable exists in A but not B
└── Modified   # Variable exists in both with different Value
```

## Relationships

```
SnapshotStore (1) ──contains──> (*) Snapshot
Snapshot (1) ──contains──> (*) SnapshotVariable
SnapshotVariable (1) ──contains──> (*) SnapshotVariable  (depth > 0, recursive)
SnapshotDiff (1) ──references──> (2) Snapshot  (by ID)
SnapshotDiff (1) ──contains──> (*) DiffEntry
```

## State Transitions

```
Snapshot lifecycle:
  [not exists] ──snapshot_create──> [stored]
  [stored] ──snapshot_delete(id)──> [not exists]
  [stored] ──snapshot_delete()──> [not exists]     (clear all)
  [stored] ──session disconnect──> [not exists]    (automatic cleanup)
```

## Validation Rules

- `Snapshot.Id` must match pattern `snap-{guid}`
- `Snapshot.Label` must be non-empty (auto-generated if not provided)
- `Snapshot.Depth` must be >= 0
- `Snapshot.ThreadId` must be a valid thread ID at capture time
- `Snapshot.FrameIndex` must be >= 0
- `SnapshotVariable.Path` must be non-empty and unique within a snapshot
- Snapshot count warning at 100 (soft limit, no rejection)
