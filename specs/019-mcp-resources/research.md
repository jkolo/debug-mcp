# Research: MCP Resources for Debugger State

**Feature**: 019-mcp-resources | **Date**: 2026-02-05

## R1: MCP SDK Resource Registration API

**Decision**: Use attribute-based resource registration with `[McpServerResourceType]` / `[McpServerResource]` and `WithResources<T>()` builder method.

**Rationale**: Consistent with the existing tool registration pattern (`[McpServerToolType]` / `WithTools()`). Attribute-based approach keeps resource declarations co-located with the handler code. The SDK supports DI injection into resource methods (same as tools).

**Alternatives considered**:
- Programmatic `McpServerResource.Create()` with delegates — more flexible for dynamic resources but less discoverable
- Handler-based (`ListResourcesHandler`, `ReadResourceHandler`) — low-level, bypasses SDK framework
- Chose attribute-based for static resources (session, breakpoints, threads) + resource template for source files

**Key API details**:
- `[McpServerResourceAttribute]`: Properties — `UriTemplate`, `Name`, `MimeType`
- `[McpServerResourceTypeAttribute]`: Class-level, marks resource provider
- `WithResources<T>()` on `IMcpServerBuilder` for registration
- Method parameters auto-bound from URI template placeholders
- Return types: `string`, `TextResourceContents`, `IEnumerable<ResourceContents>`

## R2: Dynamic Resource Availability (Session-Dependent)

**Decision**: Use custom `ListResourcesHandler` and `ReadResourceHandler` that check session state, rather than dynamically adding/removing from `ResourceCollection`.

**Rationale**: Resources must only appear when a debug session is active. While `ResourceCollection.Add/Remove` could work, using handlers gives us direct control over what's listed and when. The attribute-registered resources provide the URI/template definitions, while the handlers gate access based on session state. This avoids the SDK bug where `ResourceCollection.Changed` sends the wrong notification type (`notifications/prompts/list_changed` instead of `notifications/resources/list_changed`).

**Alternatives considered**:
- Dynamic `ResourceCollection.Add/Remove` on session attach/detach — simpler but triggers wrong notification due to SDK bug, and adds complexity of collection lifecycle management
- Always register resources, return empty/error when no session — violates MCP spec expectation that `resources/list` only shows available resources

## R3: Resource Change Notifications

**Decision**: Manually send `notifications/resources/list_changed` (on session start/end) and `notifications/resources/updated` (on state changes for subscribed resources) via `IMcpServer.SendNotificationAsync`. Use per-resource debouncing with 300ms window.

**Rationale**: The SDK's automatic notification from `ResourceCollection.Changed` has a bug (sends wrong notification type). Manual control also allows debouncing — critical when stepping through code triggers rapid state changes.

**Implementation approach**:
- New `ResourceNotifier` service (similar to existing `BreakpointNotifier` pattern)
- Subscribes to `IProcessDebugger` events: `StateChanged`, `BreakpointHit`, `ModuleLoaded`, `ModuleUnloaded`, `StepCompleted`
- Also subscribes to `BreakpointRegistry` changes (needs new event or polling)
- Per-resource `Timer` for debounce: reset on each state change, fire notification after 300ms idle
- Track client subscriptions via subscribe/unsubscribe handlers

**Alternatives considered**:
- No debouncing — risks flooding clients during stepping (20+ notifications/sec)
- Global debounce (all resources share one timer) — loses granularity, e.g., breakpoint change doesn't need to debounce thread notifications
- Channel<T> queue (like BreakpointNotifier) — over-engineered for simple debounce

## R4: Thread Snapshot Caching (Stale Flag)

**Decision**: Cache last-known thread list in the resource provider on each pause event. When process is running, serve cached data with `stale: true` and `capturedAt` timestamp.

**Rationale**: Thread enumeration requires the process to be paused (ICorDebug limitation). Caching the last pause snapshot and flagging it as stale provides useful context while being honest about data freshness. Clarified in spec session 2026-02-05.

**Implementation approach**:
- `ThreadSnapshotCache` (or inline in resource provider) — stores `IReadOnlyList<ThreadInfo>` + `DateTimeOffset capturedAt`
- Updated on `IProcessDebugger.StateChanged` when `NewState == Paused`
- Read returns cache + `stale` flag based on `IProcessDebugger.CurrentState`

## R5: Source File Security (PDB-Only Access)

**Decision**: Only serve files whose paths appear in loaded PDB symbols. Maintain a `HashSet<string>` of allowed paths, built from PDB documents on module load.

**Rationale**: Prevents the source resource from becoming a general-purpose file reader. The LLM already has dedicated file reading tools. Clarified in spec session 2026-02-05.

**Implementation approach**:
- On `ModuleLoaded` event: enumerate PDB documents via `PdbSymbolCache.GetOrCreateReader()` → `MetadataReader.Documents` → collect file paths into `AllowedSourcePaths` set
- On `ModuleUnloaded`: remove paths associated with that module
- On resource read: check path against set before serving file

## R6: Subscription Tracking

**Decision**: Track subscriptions in a simple `ConcurrentDictionary<string, bool>` (URI → subscribed). Only send `resources/updated` notifications for URIs with active subscriptions.

**Rationale**: MCP spec says clients explicitly subscribe to resources they want updates for. We should respect this to avoid unnecessary notification traffic.

**Alternatives considered**:
- Always notify for all resources (ignore subscriptions) — simpler but violates MCP protocol semantics
- Full subscription manager with per-client tracking — over-engineered for single-client stdio transport

## R7: SDK Bug Workaround

**Decision**: Do NOT use `ResourceCollection` for automatic list change notifications. Instead, manually send `notifications/resources/list_changed` via `IMcpServer.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification)`.

**Rationale**: SDK version 0.1.0-preview.13 has a bug where `ResourceCollection.Changed` event sends `notifications/prompts/list_changed` instead of `notifications/resources/list_changed`. Manual notification is trivial and avoids the bug entirely.

**Note**: This should be re-evaluated when the SDK is updated to a version that fixes this bug.

## R8: Existing Event Infrastructure

**Decision**: Subscribe to existing `IProcessDebugger` events for state change detection. Add a new `Changed` event to `BreakpointRegistry` for breakpoint list changes.

**Rationale**: `IProcessDebugger` already exposes: `StateChanged`, `BreakpointHit`, `ModuleLoaded`, `ModuleUnloaded`, `StepCompleted`, `ExceptionHit`. These cover session and thread state changes. However, `BreakpointRegistry` currently has no change event — it's a passive storage layer. Adding a `Changed` event there allows the resource notifier to react to breakpoint add/remove/update without polling.

**Data sources per resource**:

| Resource | Data Source | Change Events |
|----------|-------------|---------------|
| `debugger://session` | `IDebugSessionManager.CurrentSession` | `StateChanged`, `StepCompleted` |
| `debugger://breakpoints` | `BreakpointRegistry.GetAll()` + `GetAllExceptions()` | New `BreakpointRegistry.Changed` event |
| `debugger://threads` | `IDebugSessionManager.GetThreads()` (cached) | `StateChanged` (on pause) |
| `debugger://source/{file}` | `PdbSymbolCache` + `File.ReadAllTextAsync` | `ModuleLoaded` (new PDB paths) |
