# Research: Unified Debugging Timeline (032)

## Decision 1: Where to store timeline events

**Decision**: New `ITimelineStore` / `TimelineStore` singleton injected into `DebuggerResourceProvider`.

**Rationale**: Follows the same pattern as `BreakpointRegistry` (in-memory singleton) and `SnapshotStore` (feature 027). `TimelineStore` subscribes to all event sources and maintains a thread-safe `ConcurrentQueue<TimelineEvent>` capped at 10,000 entries. The resource provider reads from it on-demand.

**Alternatives considered**:
- Embed in `ProcessDebugger` — violates SRP; ProcessDebugger already does too much
- Embed in `DebugSessionManager` — same concern; session manager is lifecycle, not history
- Channel<T> — unnecessary for a read-many store (Channel is write-once-read-once)

---

## Decision 2: Thread lifecycle events (ThreadCreated / ThreadExited)

**Decision**: Add `ThreadCreated` and `ThreadExited` events to `IProcessDebugger` and fire them from `ProcessDebugger`'s `OnCreateThread` / `OnExitThread` callbacks.

**Rationale**: Currently, `OnCreateThread` and `OnExitThread` in `ProcessDebugger.cs` (lines 3086-3097) only auto-continue — no public event is fired. The same pattern used for `ModuleLoaded` (line 100 of IProcessDebugger.cs) can be applied. This keeps all process-domain events on `IProcessDebugger` in one place.

**Alternatives considered**:
- Subscribe directly to ICorDebug callbacks from `TimelineStore` — violates Constitution Principle I (ICorDebug access must be through ProcessDebugger layer); also breaks the locking invariant
- Expose a thread list snapshot and diff — too coarse, loses exact timestamps

**Thread name availability**: `ICorDebugThread` exposes a handle but not a name directly. Thread names in .NET are stored in a field on `System.Threading.Thread`. This requires a FuncEval at thread creation time, which is too expensive in a callback. Decision: `ThreadCreated` event carries thread ID only; thread name is `null` by default (matches spec FR-007: "where available").

---

## Decision 3: stdout/stderr events from ProcessIoManager

**Decision**: Add `OutputReceived` event to `ProcessIoManager` (signature: `event Action<string content, string stream>?`). The `PumpStreamAsync` loop fires this event for each line read from stdout/stderr in addition to buffering.

**Rationale**: `ProcessIoManager.PumpStreamAsync` already reads stdout/stderr line by line into `StringBuilder` buffers. We add a callback hook so `TimelineStore` can subscribe and record each line as a `StdoutWritten` / `StderrWritten` timeline event. Using `event Action<>` instead of `event EventHandler<>` avoids defining an extra `EventArgs` class for a simple two-field payload.

**Alternatives considered**:
- Poll `ProcessIoManager` buffers periodically — loses ordering relative to other timeline events (timestamps would be polling timestamps, not write timestamps)
- Have `ProcessIoManager` implement an interface `IOutputEventSource` — unnecessary abstraction for a single subscriber (TimelineStore)
- Channel<(string content, string stream)> — Channel is fire-and-forget; `TimelineStore` needs synchronous in-order recording, not async consumption

**Truncation**: Lines longer than 1024 chars are truncated before recording; a `truncated: true` flag is set on the event (spec FR-008).

---

## Decision 4: Filtering — resource vs. tool

**Decision**: `debugger://timeline` resource returns the full timeline (subject to cap). Filtering (by event type, thread ID, cursor) is provided via a new **`timeline_query` tool**, not through the resource URI.

**Rationale**: MCP resources use URI templates (`{param}`) for path parameters, which are suitable for resource identity (e.g., `debugger://source/{+file}`), not for query-style filtering with multiple independent optional parameters. A tool call is the natural MCP primitive for parameterised queries. This keeps the resource simple for US1 and US2, while US3 is served by the tool.

**Alternatives considered**:
- URI template with encoded filter — awkward (URL-encoding JSON in a URI segment); non-idiomatic MCP
- Multiple resources (`debugger://timeline/exceptions`, etc.) — combinatorial explosion with thread/cursor filters
- Return all events and let agents filter client-side — OK for small sessions, impractical for 10k-event cap at token cost

**Tool annotations**: `timeline_query` → ReadOnly=true, Destructive=false, Idempotent=true, OpenWorld=false.

---

## Decision 5: SessionStarted event timing and content

**Decision**: `SessionStarted` is recorded by `TimelineStore` when it receives `IProcessDebugger.StateChanged` with the new state being `Running` for the first time after a `NotStarted` or `Disconnected` state (i.e., session start, not every resume). It captures: timestamp, `launch` or `attach` mode, and PID from the `SessionStateChangedEventArgs`.

**Rationale**: `DebugSessionManager` fires `StateChanged` with `Running` on first attach/launch. This is the canonical "session started" signal already used by other components. No new infrastructure needed.

**Alternatives considered**:
- Record in `DebugSessionManager.LaunchAsync`/`AttachAsync` directly — would require `TimelineStore` to have a reference to the session manager; cleaner to use existing events
- Add a `SessionStarted` event to `IProcessDebugger` — redundant with the existing `StateChanged(Running)` + session info already on `StateChanged` args

---

## Decision 6: Timeline clear semantics

**Decision**: `TimelineStore.Clear()` is called when `IProcessDebugger.StateChanged` fires with `Disconnected`. This mirrors how `BreakpointManager` clears breakpoints on disconnect (feature 028 implementation).

**Rationale**: FR-011 requires timeline clear when a new session starts. `Disconnected → new session` is the lifecycle boundary. Clearing on `Disconnected` ensures the timeline is empty when the next `SessionStarted` event arrives.

**Alternatives considered**:
- Clear on the next `SessionStarted` — leaves stale events readable between disconnect and new session start; could confuse agents reading timeline immediately after reconnect

---

## Decision 7: Cap eviction strategy

**Decision**: When the 10,000-event cap is reached, new events silently overwrite the oldest (circular buffer using `ConcurrentQueue` with bounded size via a counter + `TryDequeue`). An `events_dropped` counter in `TimelineResponse` reports how many events were dropped total.

**Rationale**: Agents can always see the most recent events. `events_dropped > 0` signals that the beginning of the timeline may be incomplete. Simple to implement; no complex eviction policy needed.

**Alternatives considered**:
- Stop recording when cap reached (drop new events) — worse for debugging active issues; recent events are more valuable
- Expose cap as a configuration knob in the resource — adds complexity; default of 10,000 is sufficient per spec assumption

---

## Decision 8: timeline_query tool — spec annotation table

**Decision**: Add `timeline_query` to contract tests as a new tool. Annotations:
- `ReadOnly = true` — does not modify debugger state
- `Destructive = false`
- `Idempotent = true` — same query returns same result for same timeline state
- `OpenWorld = false`

This brings the total tool count to **37** (36 existing + `timeline_query`).

---

## New IProcessDebugger events required

| Event | EventArgs | Fired from |
|-------|-----------|------------|
| `ThreadCreated` | `ThreadCreatedEventArgs { int ThreadId }` | `OnCreateThread` callback |
| `ThreadExited` | `ThreadExitedEventArgs { int ThreadId }` | `OnExitThread` callback |

These are additions to the existing `IProcessDebugger` interface and `ProcessDebugger` implementation.

---

## New ProcessIoManager event required

| Event | Signature | Fired from |
|-------|-----------|------------|
| `OutputReceived` | `event Action<string content, string stream>?` | `PumpStreamAsync` per line |
