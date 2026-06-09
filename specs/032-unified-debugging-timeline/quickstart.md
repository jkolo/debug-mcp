# Quickstart: Unified Debugging Timeline (032)

## Prerequisites

```bash
dotnet build
cd tests/DebugTestApp && dotnet build && cd ../..
```

## Scenario 1 (US1): Read unified event stream

**Goal**: Verify `debugger://timeline` shows all event types in chronological order.

```bash
# 1. Start debug-mcp
dotnet run --project DebugMcp

# 2. In Claude/MCP client, run:
debug_launch path="tests/DebugTestApp/bin/Debug/net10.0/DebugTestApp.dll"
breakpoint_set file="Program.cs" line=10
debug_continue
# process hits breakpoint, throws exception, prints to stdout
```

**Expected `debugger://timeline` resource**:

```json
{
  "events": [
    { "event_id": 1, "event_type": "session_started", "payload": { "session_type": "launch", "pid": 12345 } },
    { "event_id": 2, "event_type": "module_loaded",   "payload": { "module_name": "DebugTestApp", "has_symbols": true } },
    { "event_id": 3, "event_type": "thread_started",  "thread_id": 1 },
    { "event_id": 4, "event_type": "breakpoint_hit",  "thread_id": 1, "payload": { "breakpoint_id": "bp-...", "file": "Program.cs", "line": 10 } },
    { "event_id": 5, "event_type": "stdout_written",  "payload": { "content": "Hello world", "truncated": false, "stream": "stdout" } },
    { "event_id": 6, "event_type": "exception_first_chance", "thread_id": 1, "payload": { "exception_type": "System.InvalidOperationException", "message": "..." } }
  ],
  "total_events": 6,
  "events_dropped": 0
}
```

**Pass criteria**: All 6 event types present, in timestamp order, with correct payloads.

---

## Scenario 2 (US2): Cross-event correlation

**Goal**: Verify that exception and breakpoint events share the same `thread_id` as the corresponding `thread_started` event.

After Scenario 1:
1. Note the `thread_id` in `thread_started` event
2. Verify `breakpoint_hit` event has same `thread_id`
3. Verify `exception_first_chance` event has same `thread_id`

**Pass criteria**: All three events share `thread_id: 1`.

---

## Scenario 3 (US3): Filter timeline events

**Goal**: Verify `timeline_query` tool returns only requested event types.

```
timeline_query event_types=["exception_first_chance","exception_user_unhandled"]
```

**Expected**: Only exception events in result; no breakpoint hits, module loads, etc.

```
timeline_query thread_id=1 max_events=5
```

**Expected**: Only events with `thread_id == 1`, max 5 results.

```
timeline_query from_event_id=3 max_events=10
```

**Expected**: Events with `event_id >= 3`, max 10.

---

## Scenario 4: Timeline clears on new session

1. Complete a debug session (disconnect or process exit)
2. Read `debugger://timeline` — should show `session_ended` as last event, then remain readable
3. Launch a new session
4. Read `debugger://timeline` — should start fresh with only the new `session_started`

**Pass criteria**: No stale events from previous session visible.

---

## Scenario 5: Events dropped counter

1. Start a session with a tight tracepoint loop (e.g., line in a `for` loop, `max_hits: 15000`)
2. After batch completes, read `debugger://timeline`
3. `total_events >= 10000`, `events_dropped > 0`

**Pass criteria**: `events_dropped` is non-zero and `total_events - events_dropped == events.length`.

---

## Running tests

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
```

Specific timeline tests:
```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Timeline"
```
