# Implementation Plan: MCP Event-Driven Debugger Interface

**Branch**: `030-mcp-event-driven` | **Date**: 2026-06-09 | **Spec**: [spec.md](spec.md)

## Summary

Usunięcie 6 polling tools (breakpoint_wait, breakpoint_list, debug_state, threads_list, modules_list, snapshot_list) na rzecz istniejących i nowych zasobów MCP oraz notyfikacji push. Dodanie 2 nowych zasobów (debugger://modules, debugger://snapshots), nowej notyfikacji (debugger/sessionStateChanged), wzbogacenie payloadu debugger/breakpointHit o locals oraz naprawa fake-async w process I/O tools.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**: ModelContextProtocol 1.3.0, ClrDebug 0.3.4, System.Threading.Channels, xUnit + FluentAssertions + Moq  
**Storage**: In-memory (SnapshotStore — ConcurrentDictionary, existing)  
**Testing**: xUnit + FluentAssertions + Moq  
**Target Platform**: Linux/macOS/Windows x64/arm64  
**Project Type**: Single project (DebugMcp + tests/DebugMcp.Tests)  
**Performance Goals**: Notification delivery < 500 ms (SC-003); locals evaluation budget 100 ms per notification  
**Constraints**: Notification queue must not grow unboundedly; locals evaluation is best-effort (failure → partial payload)  
**Scale/Scope**: 41 → 35 tools, 4 → 6 resources, +1 custom notification method

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ PASS | Feature nie dotyka ICorDebug APIs; modyfikuje tylko MCP layer |
| II. MCP Compliance | ✅ PASS (see below) | Usunięcie tools i dodanie resources jest zgodne z MCP spec |
| III. Test-First | ✅ PASS | TDD obowiązkowe; testy najpierw |
| IV. Simplicity | ✅ PASS | Net: więcej kodu usuwamy niż dodajemy; nowy SnapshotStore.Changed event jest minimalny |
| V. Observability | ✅ PASS | Nowe notyfikacje i resource reads logowane przez istniejący McpLoggerProvider |

**Principle II szczegóły**:
Konstytucja mówi "Long-running operations MUST support progress reporting or timeout mechanisms". `breakpoint_wait` zapewniał jawny timeout. Uzasadnienie usunięcia:
- `debug_continue` zwraca natychmiast — nie blokuje klienta MCP
- `debug_pause` jest zawsze dostępny: agent może przerwać wykonanie kiedy chce → funkcjonalny odpowiednik server-side timeout
- `debugger/sessionStateChanged` notification zastępuje completion signal
- Połączenie: `debug_continue` + `debug_pause` (jeśli brak notification w N ms) = equivalent `breakpoint_wait(timeout=N)` — z tą różnicą że timeout jest po stronie agenta, nie serwera

## Project Structure

### Documentation (this feature)

```text
specs/030-mcp-event-driven/
├── plan.md              ← this file
├── research.md          ← R-001 to R-008 decisions
├── data-model.md        ← modified/new entities
├── quickstart.md        ← verification scenarios
└── tasks.md             ← Phase 2 output (/speckit.tasks)
```

### Source Code (affected paths)

```text
DebugMcp/
├── Models/Breakpoints/
│   ├── BreakpointNotification.cs      ← +Locals, +LocalsError fields
│   └── VariableSummary.cs             ← NEW record
├── Services/
│   ├── Breakpoints/
│   │   ├── IBreakpointManager.cs      ← remove WaitForBreakpointAsync
│   │   └── BreakpointManager.cs       ← remove _pendingHit/_hitWaiter/_hitLock/WaitForBreakpointAsync; add locals fetch
│   ├── Resources/
│   │   ├── DebuggerResourceProvider.cs ← +GetModulesJson(), +GetSnapshotsJson()
│   │   └── McpResourceNotifier.cs     ← +NotifyResourceUpdated("debugger://modules") in OnModuleLoaded/Unloaded
│   │                                     +SendSessionStateNotificationAsync()
│   │                                     +subscribe ISnapshotStore.Changed
│   └── Snapshots/
│       ├── ISnapshotStore.cs          ← +event Changed
│       └── SnapshotStore.cs           ← +fire Changed in Add/Remove/Clear
├── Infrastructure/
│   └── BreakpointNotifier.cs          ← +locals/localsError in JSON payload
├── Tools/
│   ├── BreakpointWaitTool.cs          ← DELETE
│   ├── BreakpointListTool.cs          ← DELETE
│   ├── DebugStateTool.cs              ← DELETE
│   ├── ThreadsListTool.cs             ← DELETE
│   ├── ModulesListTool.cs             ← DELETE
│   ├── SnapshotListTool.cs            ← DELETE
│   ├── ProcessReadOutputTool.cs       ← sync signature fix
│   └── ProcessWriteInputTool.cs       ← sync signature fix
└── Services/Snapshots/
    └── SnapshotChangedEventArgs.cs    ← NEW

tests/DebugMcp.Tests/
├── Contract/
│   └── ToolAnnotationTests.cs         ← count 41→35, remove 6 entries
└── Unit/
    ├── Resources/
    │   ├── ModulesResourceTests.cs     ← NEW
    │   └── SnapshotsResourceTests.cs   ← NEW
    ├── Notifications/
    │   ├── SessionStateNotificationTests.cs ← NEW
    │   └── BreakpointNotificationLocalsTests.cs ← NEW
    ├── ProcessIo/
    │   └── ProcessIoAsyncTests.cs      ← NEW (reflection check sync signature)
    └── Breakpoints/
        └── BreakpointManagerPollingRemovalTests.cs ← verify removed methods/fields
```

**Structure Decision**: Single project pattern — DebugMcp + tests/DebugMcp.Tests. Brak nowych projektów.

## Implementation Phases

### Phase A: Cleanup — Usuń tools i polling (prerequisite)

Bez tej fazy testy kontrakt nie przejdą. Musi być pierwsza.

1. **Usuń 6 tool files** (delete): BreakpointWaitTool.cs, BreakpointListTool.cs, DebugStateTool.cs, ThreadsListTool.cs, ModulesListTool.cs, SnapshotListTool.cs
2. **Usuń WaitForBreakpointAsync z IBreakpointManager i BreakpointManager**: metoda + `_pendingHit`, `_hitWaiter`, `_hitLock` fields
3. **Aktualizuj ToolAnnotationTests**: count 41→35, usuń 6 wpisów z ExpectedAnnotations

### Phase B: Nowe zasoby (US2 — data sources)

4. **debugger://modules**: Dodaj `GetModulesJson()` do `DebuggerResourceProvider` + `NotifyResourceUpdated("debugger://modules")` w McpResourceNotifier.OnModuleLoaded/OnModuleUnloaded
5. **debugger://snapshots**: Dodaj `event Changed` do `ISnapshotStore`/`SnapshotStore` + `GetSnapshotsJson()` do `DebuggerResourceProvider` + subskrypcja w McpResourceNotifier

### Phase C: Nowe notyfikacje (US1 + US3)

6. **sessionStateChanged notification**: Dodaj `SendSessionStateNotificationAsync()` do McpResourceNotifier, wywołaj obok istniejącego `NotifyResourceUpdated("debugger://session")`
7. **Locals w breakpointHit**: Dodaj `VariableSummary`, rozszerz `BreakpointNotification`, inject `IDebugSessionManager` do `BreakpointManager`, fetch locals w `CreateNotification()` z 100 ms timeout, aktualizuj payload w `BreakpointNotifier.SendNotificationToMcpAsync()`

### Phase D: Process I/O fix (US4)

8. **Sync signature**: Zmień ProcessReadOutputTool.ReadOutputAsync → ReadOutput(string), ProcessWriteInputTool.WriteInputAsync → WriteInput(string)

---

## Key Design Decisions

### Locals w breakpointHit — synchronous fetch

`BreakpointManager.OnBreakpointHit()` jest wywoływany na ICorDebug callback thread gdy debuggee jest paused. Ewaluacja zmiennych jest możliwa w tym stanie. Pattern jest identyczny jak dla condition evaluation (`_conditionEvaluator.Evaluate(...).GetAwaiter().GetResult()`). Używamy `IDebugSessionManager` które jest już dostępne w wielu services — inject przez konstruktor.

### SnapshotStore.Changed — granular vs single event

Wybraliśmy jeden event `Changed` z `SnapshotChangedEventArgs` zamiast osobnych `Added`/`Removed`. Zasób `debugger://snapshots` zawsze zwraca pełną listę — subskrybent czyta całość na każdy update. Granularity eventów nie wnosi wartości po stronie resource notification.

### sessionStateChanged — obok resource update

`McpResourceNotifier.OnStateChanged()` już aktualizuje `debugger://session` i `debugger://threads`. Dodajemy DODATKOWO custom notification z bogatym payloadu (oldState, newState, pauseReason, location). Resource update pozostaje dla klientów korzystających z subskrypcji zasobów.

### Modules data source when no session

`debugger://modules` zwraca `{"modules":[],"count":0}` gdy brak aktywnej sesji. Nie rzuca InvalidOperationException. Justification: zasób powinien być zawsze czytelny (może być subskrybowany zanim sesja się otworzy).

---

## Re-evaluation: Constitution Check Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ PASS | Bez zmian w ICorDebug layer |
| II. MCP Compliance | ✅ PASS | Resources + notifications = standard MCP patterns; timeout przez debug_pause |
| III. Test-First | ✅ PASS | Każda faza: testy przed implementacją |
| IV. Simplicity | ✅ PASS | ~6 plików usuniętych, ~8 zmodyfikowanych, ~6 nowych (testy + artefakty) |
| V. Observability | ✅ PASS | Nowe ścieżki logują przez McpLoggerProvider i ILogger |

## Complexity Tracking

*(Brak naruszeń konstytucji wymagających uzasadnienia)*
