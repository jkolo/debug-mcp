# Data Model: Unified Debugging Timeline (032)

## New Models

### TimelineEvent (positional record)

```
TimelineEvent(
    int EventId,                      // monotonically increasing, 1-based, stable within session
    DateTimeOffset Timestamp,         // UTC, set at event capture time
    TimelineEventType EventType,      // discriminated type
    int? ThreadId,                    // null for session-level events (SessionStarted, SessionEnded, ModuleLoaded)
    TimelineEventPayload Payload      // discriminated union — one subtype per EventType
)
```

### TimelineEventType (enum)

```
SessionStarted
BreakpointHit
TracepointHit
ExceptionFirstChance
ExceptionUserUnhandled
ModuleLoaded
ThreadStarted
ThreadExited
StdoutWritten
StderrWritten
SessionEnded
```

### TimelineEventPayload (abstract record, one subtype per event type)

```
SessionStartedPayload(string SessionType,  // "launch" | "attach"
                      int Pid)

BreakpointHitPayload(string BreakpointId,
                     string File,
                     int Line)

TracepointHitPayload(string TracepointId,
                     string File,
                     int Line)

ExceptionPayload(string ExceptionType,
                 string Message,           // may be empty
                 bool IsUserUnhandled)

ModuleLoadedPayload(string ModuleName,
                    string? AssemblyPath,
                    bool HasSymbols)

ThreadStartedPayload(string? ThreadName)   // null if unavailable

ThreadExitedPayload(string? ThreadName)    // null if unavailable

OutputPayload(string Content,             // truncated at 1024 chars if needed
              bool Truncated,
              string Stream)              // "stdout" | "stderr"

SessionEndedPayload(string Reason)        // "process_exited" | "disconnected"
```

### TimelineResponse (read model, returned by resource/tool)

```
{
  "events": TimelineEvent[],
  "total_events": int,          // total ever recorded this session (including dropped)
  "events_dropped": int,        // events evicted due to cap
  "session_id": string | null   // session process ID as string, null if no session
}
```

### TimelineFilter (tool input model)

```
{
  "event_types": string[] | null,   // null = all types; e.g. ["exception_first_chance", "breakpoint_hit"]
  "thread_id": int | null,          // null = all threads
  "from_event_id": int | null,      // null = from start; inclusive
  "max_events": int                 // default 200, max 1000
}
```

---

## New Events on IProcessDebugger

```csharp
event EventHandler<ThreadCreatedEventArgs>? ThreadCreated;
event EventHandler<ThreadExitedEventArgs>? ThreadExited;
```

**ThreadCreatedEventArgs**: `record ThreadCreatedEventArgs(int ThreadId)`

**ThreadExitedEventArgs**: `record ThreadExitedEventArgs(int ThreadId)`

---

## New Event on ProcessIoManager

```csharp
event Action<string content, string stream>? OutputReceived;
// fired synchronously from PumpStreamAsync on each line read
// "stream" is "stdout" or "stderr"
```

---

## Storage

**TimelineStore** — new singleton:

```
ConcurrentQueue<TimelineEvent> _events
int _nextEventId (Interlocked.Increment)
int _eventsDropped (Interlocked.Increment when evicting)
const int MaxCapacity = 10_000
```

Eviction: when `_events.Count >= MaxCapacity`, `TryDequeue` one before `Enqueue` new.

---

## File Layout (new files)

```
DebugMcp/
├── Models/
│   └── Timeline/
│       ├── TimelineEvent.cs           # positional record
│       ├── TimelineEventType.cs       # enum
│       ├── TimelineEventPayload.cs    # abstract + concrete subtypes
│       ├── TimelineFilter.cs          # filter input record
│       └── TimelineResponse.cs        # read DTO
├── Services/
│   └── Timeline/
│       ├── ITimelineStore.cs          # interface: Record, GetAll, GetFiltered, Clear, counts
│       └── TimelineStore.cs           # singleton, subscribes to events, owns ConcurrentQueue
└── Tools/
    └── TimelineQueryTool.cs           # timeline_query tool (ReadOnly=true)
```

Modified files:

```
DebugMcp/Services/IProcessDebugger.cs          # +ThreadCreated, +ThreadExited events
DebugMcp/Services/ProcessDebugger.cs           # fire ThreadCreated/ThreadExited from callbacks
DebugMcp/Services/ProcessIoManager.cs          # +OutputReceived event, fire from PumpStreamAsync
DebugMcp/Services/Resources/DebuggerResourceProvider.cs  # +debugger://timeline resource method
DebugMcp/Program.cs                            # register TimelineStore; wire event subscriptions
tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs     # +timeline_query annotation entry
```
