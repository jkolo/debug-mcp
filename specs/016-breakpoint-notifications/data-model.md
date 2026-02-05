# Data Model: MCP Breakpoint Notifications

## Entity Diagram

```
┌────────────────────────┐
│      Breakpoint        │
├────────────────────────┤
│ Id: string             │
│ Type: BreakpointType   │◄──── NEW: Blocking | Tracepoint
│ Location: Location     │
│ State: BreakpointState │
│ Enabled: bool          │
│ Verified: bool         │
│ Condition: string?     │
│ HitCount: int          │
│ Message: string?       │
│ LogMessage: string?    │◄──── NEW: For tracepoints only
│ HitCountMultiple: int? │◄──── NEW: Notify every Nth hit
│ MaxNotifications: int? │◄──── NEW: Auto-disable after N
│ NotificationsSent: int │◄──── NEW: Counter for max limit
└────────────────────────┘
           │
           │ generates
           ▼
┌──────────────────────────────┐
│    BreakpointNotification    │
├──────────────────────────────┤
│ BreakpointId: string         │
│ Type: BreakpointType         │
│ Location: NotificationLoc    │
│ ThreadId: int                │
│ Timestamp: DateTimeOffset    │
│ HitCount: int                │
│ LogMessage: string?          │◄──── Evaluated template
│ ExceptionInfo: ExceptionInfo?│
└──────────────────────────────┘

┌────────────────────┐
│   BreakpointType   │
├────────────────────┤
│ Blocking = 0       │◄──── Default, pauses execution
│ Tracepoint = 1     │◄──── Notify only, continues
└────────────────────┘

┌──────────────────────────────┐
│     NotificationLocation     │
├──────────────────────────────┤
│ File: string                 │
│ Line: int                    │
│ Column: int?                 │
│ FunctionName: string?        │
│ ModuleName: string?          │
└──────────────────────────────┘

┌────────────────────────────────┐
│     EvaluatedExpression        │
├────────────────────────────────┤
│ Expression: string             │◄──── Original e.g. "myVar"
│ Value: string?                 │◄──── Evaluated result
│ Error: string?                 │◄──── If evaluation failed
└────────────────────────────────┘
```

## Entity Definitions

### BreakpointType (NEW)

Enum distinguishing blocking breakpoints from non-blocking tracepoints.

| Value | Description |
|-------|-------------|
| `Blocking` | Traditional breakpoint - pauses execution, waitable via `breakpoint_wait` |
| `Tracepoint` | Observation point - sends notification, continues execution immediately |

### Breakpoint (EXTENDED)

Existing entity extended with tracepoint-specific fields:

| Field | Type | Description |
|-------|------|-------------|
| Type | BreakpointType | NEW: Whether this is blocking or tracepoint |
| LogMessage | string? | NEW: Template with `{expression}` placeholders |
| HitCountMultiple | int? | NEW: Notify only every Nth hit (0 = every hit) |
| MaxNotifications | int? | NEW: Auto-disable after N notifications (0 = unlimited) |
| NotificationsSent | int | NEW: Count of notifications sent (for max limit) |

### BreakpointNotification (NEW)

Payload sent via MCP notification when breakpoint/tracepoint fires.

| Field | Type | Description |
|-------|------|-------------|
| BreakpointId | string | ID of the triggered breakpoint/tracepoint |
| Type | BreakpointType | Whether blocking or tracepoint |
| Location | NotificationLocation | File, line, function info |
| ThreadId | int | Thread that hit the breakpoint |
| Timestamp | DateTimeOffset | When the hit occurred |
| HitCount | int | Total times this breakpoint has been hit |
| LogMessage | string? | Evaluated log message (tracepoints only) |
| ExceptionInfo | ExceptionInfo? | For exception breakpoints |

### EvaluatedExpression (NEW)

Result of evaluating a single expression from a tracepoint log message.

| Field | Type | Description |
|-------|------|-------------|
| Expression | string | Original expression from template |
| Value | string? | Evaluated result if successful |
| Error | string? | Error message if evaluation failed |

## State Transitions

### Breakpoint Lifecycle (unchanged for blocking)

```
   SetBreakpoint
        │
        ▼
  ┌──────────┐    Module Load    ┌────────────┐
  │ Pending  │ ───────────────► │   Bound    │
  └──────────┘                   └────────────┘
                                       │
                                       │ Hit
                                       ▼
                                 ┌────────────┐
                                 │   Active   │ ◄── Waiting in queue
                                 └────────────┘
                                       │
                                       │ Continue
                                       ▼
                                 ┌────────────┐
                                 │   Bound    │
                                 └────────────┘
```

### Tracepoint Lifecycle (NEW)

```
   SetTracepoint
        │
        ▼
  ┌──────────┐    Module Load    ┌────────────┐
  │ Pending  │ ───────────────► │   Bound    │
  └──────────┘                   └────────────┘
                                       │
                                       │ Hit
                                       ▼
                              ┌─────────────────┐
                              │ Evaluate & Send │
                              │  notification   │
                              └─────────────────┘
                                       │
                                       │ Continue (immediate)
                                       ▼
                                 ┌────────────┐
                                 │   Bound    │
                                 └────────────┘
                                       │
                               (maxNotifications reached)
                                       │
                                       ▼
                                 ┌────────────┐
                                 │  Disabled  │
                                 └────────────┘
```

## Validation Rules

### Tracepoint Creation

1. `file` MUST be non-empty
2. `line` MUST be >= 1
3. `column` (optional) MUST be >= 1 if provided
4. `logMessage` (optional) - if provided, `{expression}` placeholders are validated syntax
5. `hitCountMultiple` (optional) MUST be >= 0 (0 = every hit)
6. `maxNotifications` (optional) MUST be >= 0 (0 = unlimited)

### Log Message Template

1. Expressions are enclosed in `{` and `}`
2. Nested braces not supported: `{{` is literal `{`, not expression
3. Empty expression `{}` is invalid
4. Expression syntax validated at evaluation time (not creation)

### Notification Delivery

1. Notifications are fire-and-forget (no delivery guarantee)
2. Failed notifications are logged but don't affect debuggee
3. Notification queue is unbounded (memory is bounded by max_notifications limits)
