# Quickstart: MCP Resources for Debugger State

**Feature**: 019-mcp-resources | **Date**: 2026-02-05

## Overview

This feature adds 4 MCP Resources to DebugMcp, exposing debugger state as read-only data that LLM clients can browse without calling tools.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                   MCP Client (LLM)                  │
│                                                     │
│  resources/list  resources/read  resources/subscribe│
└──────┬──────────────┬────────────────┬──────────────┘
       │              │                │
       ▼              ▼                ▼
┌──────────────────────────────────────────────────────┐
│                  DebugMcp MCP Server                 │
│                                                      │
│  ┌──────────────────────────────────────────────┐    │
│  │          Resource Handlers (new)              │    │
│  │                                               │    │
│  │  ListResourcesHandler ──→ checks session     │    │
│  │  ReadResourceHandler  ──→ dispatches by URI  │    │
│  │  SubscribeHandler     ──→ tracks subs        │    │
│  └────────────┬──────────────────────────────────┘    │
│               │                                       │
│  ┌────────────▼──────────────────────────────────┐    │
│  │       ResourceNotifier (new)                  │    │
│  │                                               │    │
│  │  Debounce timers per resource                │    │
│  │  Subscription tracking                        │    │
│  │  Sends notifications/resources/updated        │    │
│  │  Sends notifications/resources/list_changed   │    │
│  └────────────┬──────────────────────────────────┘    │
│               │ subscribes to events                  │
│  ┌────────────▼──────────────────────────────────┐    │
│  │       Existing Services                       │    │
│  │                                               │    │
│  │  IDebugSessionManager  → session + threads    │    │
│  │  BreakpointRegistry    → breakpoints          │    │
│  │  IProcessDebugger      → events               │    │
│  │  PdbSymbolCache        → source file paths    │    │
│  └───────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────┘
```

## New Components

### 1. DebuggerResourceProvider

Resource handler class registered with `[McpServerResourceType]`. Contains methods annotated with `[McpServerResource]` for each resource URI:

- `GetSession()` → `debugger://session` (application/json)
- `GetBreakpoints()` → `debugger://breakpoints` (application/json)
- `GetThreads()` → `debugger://threads` (application/json)
- `GetSourceFile(string file)` → `debugger://source/{file}` (text/plain)

Each method checks `IDebugSessionManager.CurrentSession != null` before returning data.

### 2. ResourceNotifier

Event-driven notification service. Subscribes to:
- `IProcessDebugger.StateChanged` → notify session + threads
- `IProcessDebugger.StepCompleted` → notify session + threads
- `BreakpointRegistry.Changed` (new event) → notify breakpoints
- `IProcessDebugger.ModuleLoaded` → update allowed source paths

Debounces notifications per-resource (300ms window).

### 3. AllowedSourcePaths

Security boundary for `debugger://source/{file}`. Maintains a set of file paths extracted from PDB documents. Updated on module load/unload.

### 4. ThreadSnapshotCache

Caches thread list on each pause for serving stale data when process is running.

## Changes to Existing Code

### BreakpointRegistry
- Add `event EventHandler? Changed` — fired on Add/Remove/Update operations

### Program.cs
- Add `Resources` capability with `Subscribe = true`, `ListChanged = true`
- Register `ResourceNotifier` and related services in DI
- Register resources via `WithResources<DebuggerResourceProvider>()`

## File Layout

```
DebugMcp/
├── Services/
│   └── Resources/
│       ├── DebuggerResourceProvider.cs    # Resource handlers
│       ├── ResourceNotifier.cs            # Debounced notifications
│       ├── AllowedSourcePaths.cs          # PDB path security
│       └── ThreadSnapshotCache.cs         # Stale thread data
└── Program.cs                             # Register resources + capabilities

tests/DebugMcp.Tests/
└── Unit/
    └── Resources/
        ├── DebuggerResourceProviderTests.cs
        ├── ResourceNotifierTests.cs
        ├── AllowedSourcePathsTests.cs
        └── ThreadSnapshotCacheTests.cs
```

## Implementation Order

1. `ThreadSnapshotCache` + `AllowedSourcePaths` (no dependencies, pure logic)
2. `BreakpointRegistry.Changed` event (small change to existing code)
3. `DebuggerResourceProvider` (resource handlers, depends on existing services)
4. `ResourceNotifier` (event subscriptions, debouncing, notifications)
5. `Program.cs` wiring (DI registration, capabilities)
6. Integration tests
