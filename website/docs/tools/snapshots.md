---
title: Snapshots
sidebar_position: 9
---

# Snapshots

Snapshot tools capture, compare, and delete in-memory point-in-time captures of a paused
session's variables — a lightweight alternative to re-running the same inspection calls to see
what changed between two points in execution.

## When to Use

Use snapshot tools when you need to compare state across two points in a debugging session —
before and after a suspected mutation, or across two hits of the same breakpoint. Snapshots live
only in memory for the lifetime of the debug session; they are not persisted to disk.

**Typical flow:** `snapshot_create` (at point A) → *(continue execution)* → `snapshot_create` (at point B) → `snapshot_diff` (A, B) → `snapshot_delete` (cleanup)

## Tools

### snapshot_create

Capture the current debug state (variables, arguments, `this`) as a named snapshot.

**Requires:** Paused session

**When to use:** You want to preserve the current variable state so you can compare it against
a later point in execution — for example, before continuing past a suspected mutation.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `label` | string | No | Human-readable label for the snapshot (auto-generated if omitted) |
| `thread_id` | integer | No | Thread to capture variables from (default: active thread) |
| `frame_index` | integer | No | Stack frame index, 0 = top of stack (default: 0) |
| `depth` | integer | No | Expansion depth for nested objects, 0 = top-level only (default: 0) |
| `timeout_ms` | integer | No | Maximum time to wait for the snapshot to be created, in milliseconds (default: 30000). Accepted for consistency with the other inspection tools; the underlying call is synchronous and returns immediately, so this parameter currently has no effect. |

**Example:**
```json
{
  "label": "before-recalculate",
  "frame_index": 0
}
```

**Response:**
```json
{
  "success": true,
  "snapshot": {
    "id": "snap-550e8400-e29b-41d4-a716-446655440000",
    "label": "before-recalculate",
    "timestamp": "2024-01-15T10:30:45.123Z",
    "threadId": 5,
    "frameIndex": 0,
    "functionName": "OrderService.Recalculate",
    "variableCount": 6,
    "depth": 0
  }
}
```

**Real-world use case:** An AI agent suspects a method mutates an object it shouldn't. It calls `snapshot_create` right before the call, continues execution to just after it, calls `snapshot_create` again, then uses `snapshot_diff` to see exactly which fields changed.

---

### snapshot_diff

Compare two snapshots and return structured differences (added, removed, modified variables).

**Requires:** No session needed (diffs operate on already-captured, in-memory snapshots)

**When to use:** You've captured two snapshots (e.g. before/after a suspected mutation, or two
hits of the same breakpoint) and want to see exactly what changed — added variables, removed
variables, and modified variables with their before/after values — without manually comparing
two `variables_get` dumps.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `snapshot_id_1` | string | Yes | First snapshot ID (baseline) |
| `snapshot_id_2` | string | Yes | Second snapshot ID (comparison) |

**Example:**
```json
{
  "snapshot_id_1": "snap-550e8400-e29b-41d4-a716-446655440000",
  "snapshot_id_2": "snap-660e8400-e29b-41d4-a716-446655440001"
}
```

**Response:**
```json
{
  "success": true,
  "diff": {
    "snapshotIdA": "snap-550e8400-e29b-41d4-a716-446655440000",
    "snapshotIdB": "snap-660e8400-e29b-41d4-a716-446655440001",
    "threadMismatch": false,
    "timeDelta": "00:00:02.4180000",
    "summary": { "added": 1, "removed": 0, "modified": 2, "unchanged": 3 },
    "added": [
      { "name": "total", "path": "total", "type": "System.Decimal", "value": "299.99" }
    ],
    "removed": [],
    "modified": [
      { "name": "status", "path": "status", "type": "System.String", "oldValue": "\"Pending\"", "newValue": "\"Confirmed\"" }
    ]
  }
}
```

**Errors:**
- `SNAPSHOT_NOT_FOUND` — Either snapshot ID does not exist

**Real-world use case:** After stepping over a `Recalculate()` call, an AI agent diffs the before/after snapshots and immediately sees `status` flipped from `"Pending"` to `"Confirmed"` and a new `total` field appeared — confirming the method's side effects without manually inspecting every field.

---

### snapshot_delete

Delete a specific snapshot by ID, or clear all snapshots if no ID is provided.

**Requires:** No session needed (operates on already-captured, in-memory snapshots)

**When to use:** Clean up snapshots once they're no longer needed, or clear everything between
unrelated investigations within the same session.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `snapshot_id` | string | No | Snapshot ID to delete. If omitted, deletes all snapshots. |

**Example (single snapshot):**
```json
{ "snapshot_id": "snap-550e8400-e29b-41d4-a716-446655440000" }
```

**Response:**
```json
{
  "success": true,
  "deleted": "snap-550e8400-e29b-41d4-a716-446655440000",
  "remaining": 2
}
```

**Example (clear all):**
```json
{}
```

**Response:**
```json
{
  "success": true,
  "deleted": "all",
  "remaining": 0
}
```

**Errors:**
- `SNAPSHOT_NOT_FOUND` — The given `snapshot_id` does not exist

**Real-world use case:** After finishing a diff-driven investigation, an AI agent calls `snapshot_delete` with no arguments to clear all snapshots before starting a fresh, unrelated investigation in the same session.

---

## Listing snapshots

There is no `snapshot_list` MCP tool. Read the **`debugger://snapshots`** MCP resource instead
— it returns every captured snapshot's `id`, `label`, `createdAt`, `threadId`, `frameIndex`,
`functionName`, and `variableCount`, without needing a round-trip tool call.
