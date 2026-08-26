---
title: Timeline
sidebar_position: 11
---

# Timeline

`timeline_query` queries a unified, chronological event timeline spanning session, breakpoint,
exception, module, thread, and process-output events — one call to answer "what happened, and
in what order?" instead of correlating separate breakpoint hits, output captures, and module
loads by hand.

## When to Use

Use `timeline_query` when you need cross-cutting, chronological visibility into a debugging
session — for example, to see whether stdout output happened before or after a given breakpoint
hit, or to see every event on one specific thread. Events are held in an in-memory ring buffer
(capacity 10,000, oldest evicted first) for the duration of the session.

**Typical flow:** *(debugging session runs, generating events)* → `timeline_query` (optionally filtered by event type / thread, paginated via `fromEventId`)

## Tools

### timeline_query

Query the unified debugging timeline.

**Requires:** No session needed — queries the in-memory timeline, which persists for the process lifetime (including after the debugged process has exited, until the server restarts)

**When to use:** You want debug events in chronological order across multiple sources in one
call — session start/end, breakpoint and tracepoint hits, first-chance and user-unhandled
exceptions, module loads, thread start/exit, and stdout/stderr writes.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `eventTypes` | string | No | JSON array of event type names to include, e.g. `["breakpoint_hit","exception_first_chance"]`. Null/empty returns all types. |
| `threadId` | integer | No | Filter to events from this thread ID only. Null returns events from all threads. |
| `fromEventId` | integer | No | Return only events with `eventId >= this value` — a cursor for pagination. Use the last `eventId` from a previous response to page forward. |
| `maxEvents` | integer | No | Maximum number of events to return (default: 200, max: 1000) |

**Supported event type names:** `session_started`, `breakpoint_hit`, `tracepoint_hit`,
`exception_first_chance`, `exception_user_unhandled`, `module_loaded`, `thread_started`,
`thread_exited`, `stdout_written`, `stderr_written`, `session_ended`.

**Example:**
```json
{
  "eventTypes": "[\"breakpoint_hit\",\"stdout_written\"]",
  "maxEvents": 100
}
```

**Response:**
```json
{
  "success": true,
  "events": [
    {
      "eventId": 1,
      "timestamp": "2024-01-15T10:30:45.123Z",
      "eventType": 1,
      "payload": {},
      "threadId": 5
    }
  ],
  "totalEvents": 42,
  "eventsDropped": 0
}
```

`eventType` is currently emitted as the underlying enum's raw integer ordinal (not the string
name shown in the parameter description above), and `payload` is currently always `{}` — both
are pre-existing characterized behaviors of this tool, not something to rely on changing
without a version bump. Use `eventId` order and `threadId`/`timestamp` for correlation today.

**Real-world use case:** An AI agent debugging an intermittent race condition calls `timeline_query` filtered to a specific `threadId` to see the exact interleaving of breakpoint hits and output writes on that thread, without manually cross-referencing separate breakpoint and process-output logs.
