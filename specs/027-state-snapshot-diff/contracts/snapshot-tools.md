# MCP Tool Contracts: State Snapshot & Diff

**Feature**: 027-state-snapshot-diff
**Date**: 2026-02-11

## snapshot_create

**Name**: `snapshot_create`
**Title**: Create State Snapshot
**Description**: Capture the current debug state (variables, arguments, this) as a named snapshot. Must be called while the process is paused.

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `label` | string | No | auto ("snapshot-N") | Human-readable label for the snapshot |
| `thread_id` | integer | No | active thread | Thread to capture variables from |
| `frame_index` | integer | No | 0 | Stack frame index (0 = top of stack) |
| `depth` | integer | No | 0 | Expansion depth for nested objects (0 = top-level only) |

### Response (success)

```json
{
  "success": true,
  "snapshot": {
    "id": "snap-a1b2c3d4-...",
    "label": "before-fix",
    "timestamp": "2026-02-11T14:30:00.000+00:00",
    "threadId": 12345,
    "frameIndex": 0,
    "functionName": "MyApp.OrderService.ProcessOrder",
    "variableCount": 8,
    "depth": 0
  }
}
```

### Response (error — not paused)

```json
{
  "success": false,
  "error": {
    "code": "NOT_PAUSED",
    "message": "Cannot create snapshot while process is running. Pause at a breakpoint first."
  }
}
```

### Response (warning — soft limit)

```json
{
  "success": true,
  "snapshot": { ... },
  "warning": "Snapshot count (100) has reached the soft limit. Consider deleting old snapshots."
}
```

### Annotations

- `readOnlyHint`: false (creates state)
- `destructiveHint`: false
- `idempotentHint`: false (each call creates a new snapshot)

---

## snapshot_diff

**Name**: `snapshot_diff`
**Title**: Compare Two Snapshots
**Description**: Compare two snapshots and return structured differences (added, removed, modified variables with before/after values).

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `snapshot_id_1` | string | Yes | — | First snapshot ID (baseline) |
| `snapshot_id_2` | string | Yes | — | Second snapshot ID (comparison) |

### Response (success — with changes)

```json
{
  "success": true,
  "diff": {
    "snapshotIdA": "snap-aaa...",
    "snapshotIdB": "snap-bbb...",
    "threadMismatch": false,
    "timeDelta": "00:00:05.230",
    "summary": {
      "added": 1,
      "removed": 0,
      "modified": 2,
      "unchanged": 5
    },
    "added": [
      { "name": "result", "path": "result", "type": "System.String", "value": "\"Success\"" }
    ],
    "removed": [],
    "modified": [
      { "name": "retryCount", "path": "retryCount", "type": "System.Int32", "oldValue": "2", "newValue": "3" },
      { "name": "City", "path": "order.Customer.City", "type": "System.String", "oldValue": "\"Warsaw\"", "newValue": "\"Krakow\"" }
    ]
  }
}
```

### Response (success — no changes)

```json
{
  "success": true,
  "diff": {
    "snapshotIdA": "snap-aaa...",
    "snapshotIdB": "snap-bbb...",
    "threadMismatch": false,
    "timeDelta": "00:00:01.100",
    "summary": { "added": 0, "removed": 0, "modified": 0, "unchanged": 8 },
    "added": [],
    "removed": [],
    "modified": []
  }
}
```

### Response (error — invalid snapshot)

```json
{
  "success": false,
  "error": {
    "code": "SNAPSHOT_NOT_FOUND",
    "message": "Snapshot 'snap-invalid' not found. It may have been deleted or the session may have been disconnected."
  }
}
```

### Annotations

- `readOnlyHint`: true (does not modify state)
- `destructiveHint`: false
- `idempotentHint`: true (same inputs, same output)

---

## snapshot_list

**Name**: `snapshot_list`
**Title**: List Snapshots
**Description**: List all snapshots in the current debug session with their metadata.

### Parameters

None.

### Response (success)

```json
{
  "success": true,
  "snapshots": [
    {
      "id": "snap-aaa...",
      "label": "before-fix",
      "timestamp": "2026-02-11T14:30:00.000+00:00",
      "threadId": 12345,
      "functionName": "MyApp.OrderService.ProcessOrder",
      "variableCount": 8
    },
    {
      "id": "snap-bbb...",
      "label": "after-fix",
      "timestamp": "2026-02-11T14:30:05.230+00:00",
      "threadId": 12345,
      "functionName": "MyApp.OrderService.ProcessOrder",
      "variableCount": 9
    }
  ],
  "count": 2
}
```

### Response (empty)

```json
{
  "success": true,
  "snapshots": [],
  "count": 0
}
```

### Annotations

- `readOnlyHint`: true
- `destructiveHint`: false
- `idempotentHint`: true

---

## snapshot_delete

**Name**: `snapshot_delete`
**Title**: Delete Snapshot(s)
**Description**: Delete a specific snapshot by ID, or clear all snapshots if no ID is provided.

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `snapshot_id` | string | No | — | Snapshot ID to delete. If omitted, deletes all snapshots. |

### Response (success — single delete)

```json
{
  "success": true,
  "deleted": "snap-aaa...",
  "remaining": 1
}
```

### Response (success — clear all)

```json
{
  "success": true,
  "deleted": "all",
  "remaining": 0
}
```

### Response (error — not found)

```json
{
  "success": false,
  "error": {
    "code": "SNAPSHOT_NOT_FOUND",
    "message": "Snapshot 'snap-invalid' not found."
  }
}
```

### Annotations

- `readOnlyHint`: false
- `destructiveHint`: true (deletes data)
- `idempotentHint`: false (clear all is idempotent, single delete is not after first call)
