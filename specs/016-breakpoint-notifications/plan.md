# Implementation Plan: MCP Breakpoint Notifications

**Branch**: `016-breakpoint-notifications` | **Date**: 2026-02-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/016-breakpoint-notifications/spec.md`

## Summary

Add push-based MCP notifications for breakpoint events and introduce "tracepoints" - non-blocking observation points that send notifications with custom log messages containing evaluated expressions. This enables LLM agents to monitor code execution without polling `breakpoint_wait`.

## Technical Context

**Language/Version**: C# / .NET 10.0 + ClrDebug (ICorDebug wrappers)
**Primary Dependencies**: ModelContextProtocol SDK 0.1.0-preview.13, ClrDebug 0.3.4
**Storage**: N/A (in-memory breakpoint/tracepoint registry within session)
**Testing**: xUnit, Reqnroll (E2E), FluentAssertions
**Target Platform**: Linux x64, Windows x64
**Project Type**: Single project (MCP server)
**Performance Goals**: <100ms notification delivery, <5ms tracepoint overhead per hit
**Constraints**: Fire-and-forget notifications, async expression evaluation
**Scale/Scope**: Handle 100+ tracepoint hits/sec without degradation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ PASS | Uses ICorDebug breakpoint callbacks directly |
| II. MCP Compliance | ✅ PASS | Uses `SendNotificationAsync` for custom notifications, follows naming conventions |
| III. Test-First | ✅ PASS | Tests written before implementation per spec acceptance scenarios |
| IV. Simplicity | ✅ PASS | Extends existing breakpoint infrastructure, no new abstractions needed |
| V. Observability | ✅ PASS | All tracepoint/breakpoint events logged with structured logging |

**MCP Tool Standards**:
- New tool: `tracepoint_set` (noun_verb format ✅)
- Existing tools extended: `breakpoint_list` (adds type indicator)
- Notification method: Custom `debugger/breakpointHit` notification

## Project Structure

### Documentation (this feature)

```text
specs/016-breakpoint-notifications/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
DebugMcp/
├── Models/
│   └── Breakpoints/
│       ├── Breakpoint.cs              # Extend with Type property
│       ├── BreakpointType.cs          # NEW: Blocking, Tracepoint
│       └── BreakpointNotification.cs  # NEW: MCP notification payload
├── Services/
│   └── Breakpoints/
│       ├── IBreakpointManager.cs      # Extend with tracepoint methods
│       ├── BreakpointManager.cs       # Add tracepoint + notification logic
│       ├── IBreakpointNotifier.cs     # NEW: Interface for notification delivery
│       └── LogMessageEvaluator.cs     # NEW: Evaluates {expression} in log templates
├── Tools/
│   ├── BreakpointSetTool.cs           # Unchanged (blocking breakpoints)
│   ├── BreakpointListTool.cs          # Add type field to output
│   └── TracepointSetTool.cs           # NEW: Set tracepoints with log messages
└── Infrastructure/
    └── BreakpointNotifier.cs          # NEW: Sends MCP notifications

tests/
├── DebugMcp.Tests/
│   └── Unit/
│       └── Breakpoints/
│           ├── TracepointTests.cs         # NEW: Unit tests
│           └── NotificationTests.cs       # NEW: Notification unit tests
└── DebugMcp.E2E/                              # E2E notification tests DESCOPED
                                                # (MCP notifications not capturable in Reqnroll)
```

**Structure Decision**: Single project structure maintained. New files added to existing directories following established patterns.

## Complexity Tracking

> No complexity violations identified.
