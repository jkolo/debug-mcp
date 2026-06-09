# Research: MCP Event-Driven Debugger Interface (030)

## R-001: sessionStateChanged — custom notification vs resource-only

**Decision**: Dodać nową custom MCP notification `debugger/sessionStateChanged` OBOK istniejącej aktualizacji resource `debugger://session`.

**Rationale**:
- `McpResourceNotifier.OnStateChanged` już wywołuje `NotifyResourceUpdated("debugger://session")` — to informuje klienta że zasób się zmienił, ale wymaga aktywnej subskrypcji i fetch
- Custom notification `debugger/sessionStateChanged` z pełnym payloadu (newState, oldState, pauseReason, location, threadId) daje agentowi wszystkie potrzebne informacje w jednym atocie
- Obie ścieżki są potrzebne: resource subscription dla wstępnego odczytu, notification dla reaktywnego przetwarzania
- Istniejący `SessionStateChangedEventArgs` zawiera: `NewState`, `OldState`, `PauseReason?`, `Location?`, `ThreadId?` — dokładnie to co jest w payload

**Implementacja**: W `McpResourceNotifier.OnStateChanged` dodać wywołanie `_ = SendSessionStateNotificationAsync(e)` obok istniejących `NotifyResourceUpdated(...)`. Bez nowej klasy — bezpośrednio w McpResourceNotifier.

**Alternatives considered**:
- Only resource update (Option A): klient musi fetch cały resource po każdym update — dwa round-tripy
- Osobna klasa SessionNotifier: niepotrzebna kompleksowość — McpResourceNotifier już ma `GetServer()` i wzorzec

---

## R-002: Locals enrichment w breakpointHit notification

**Decision**: Wzbogacić `BreakpointNotification` o opcjonalne `locals` z top frame. Ewaluację wykonać synchronicznie w `BreakpointManager.OnBreakpointHit()` przez istniejący mechanizm zmiennych, z budżetem 100 ms.

**Rationale**:
- Kiedy `OnBreakpointHit` jest wywołany, debuggee jest paused — ICorDebug pozwala na ewaluację zmiennych w tym stanie
- `BreakpointManager` już wykonuje synchroniczną ewaluację warunków (`_conditionEvaluator.Evaluate(...)`) na tym samym wątku
- Nie trzeba zmieniać `BreakpointHitEventArgs` — locals mogą być pobrane bezpośrednio w `CreateNotification()` z injected `IDebugSessionManager`
- Failure strategy: jeśli ewaluacja się nie uda lub przekroczy 100 ms, wysłać notification z `"locals": null` i `"localsError": "timeout"/"unavailable"` zamiast nie wysyłać w ogóle

**Implementacja**:
1. Inject `IDebugSessionManager` do `BreakpointManager` 
2. W `CreateNotification()`: wywołać `sessionManager.GetVariablesAsync(threadId, frameIndex: 0, "locals")` z CancellationToken z 100 ms timeout, złapać wyjątek
3. Rozszerzyć `BreakpointNotification` o `IReadOnlyList<VariableSummary>? Locals` i `string? LocalsError`
4. W `BreakpointNotifier.SendNotificationToMcpAsync()` dodać `locals` do JSON payload

**Alternatives considered**:
- Nie dodawać locals (tylko obecny payload): spec wymaga SC-004 (locals w 80% scenariuszy) — odrzucono
- Async locals fetch po wysłaniu notification: race condition — debuggee mógłby być resumed zanim locals są fetched
- Dodawać stack trace: zbyt kosztowne synchronicznie; stack top (functionName, file, line) jest już dostępny w lokalizacji breakpointu

---

## R-003: debugger://modules — nowy zasób

**Decision**: Dodać metodę `GetModulesJson()` do `DebuggerResourceProvider`, dodać `NotifyResourceUpdated("debugger://modules")` w istniejących handler'ach `McpResourceNotifier.OnModuleLoaded` i `OnModuleUnloaded`.

**Rationale**:
- `McpResourceNotifier` JUŻ subskrybuje `ModuleLoaded` i `ModuleUnloaded` events — tylko side effect (AllowedSourcePaths) bez notyfikacji zasobu
- Dodanie `NotifyResourceUpdated("debugger://modules")` w tych handler'ach to JEDNA linijka per handler
- `DebuggerResourceProvider` potrzebuje dostępu do listy modułów — istniejący `IDebugSessionManager.GetModulesAsync()` powinien to obsługiwać (do zweryfikowania; alternatywnie `ProcessDebugger.GetLoadedModulesAsync()`)
- Debounce 300 ms w `ResourceNotifier` automatycznie obsłuży burst module loads przy starcie

**Schema debugger://modules**:
```json
{
  "modules": [
    {
      "name": "DebugTestApp.dll",
      "fullName": "DebugTestApp, Version=1.0.0.0",
      "path": "/path/to/DebugTestApp.dll",
      "version": "1.0.0.0",
      "isManaged": true,
      "isDynamic": false,
      "hasSymbols": true,
      "symbolStatus": "Loaded",
      "baseAddress": "0x7f8b4a000000",
      "size": 524288
    }
  ],
  "count": 1
}
```

**Alternatives considered**:
- Zostawić `modules_list` jako tool: duplikacja z resource — odrzucono zgodnie z wymaganiami

---

## R-004: debugger://snapshots — nowy zasób i eventy

**Decision**: Dodać `event EventHandler? Changed` do `ISnapshotStore` i `SnapshotStore`. Subskrybować w `McpResourceNotifier`. Dodać `GetSnapshotsJson()` do `DebuggerResourceProvider`.

**Rationale**:
- `ISnapshotStore` i `SnapshotStore` nie mają żadnych event'ów — prosty `ConcurrentDictionary`
- Nie ma sensu dodawać osobnych `Added`/`Removed` event'ów — wystarczy generyczny `Changed` (zasób jest zawsze pełnym snapshot listy)
- `SnapshotStore.Add()` i `SnapshotStore.Remove()` i `SnapshotStore.Clear()` są naturalnymi punktami do wywołania `Changed`
- `SnapshotService.ClearAll()` wywołuje `_store.Clear()` przy disconnect — nie potrzeba osobnego handlera

**Schema debugger://snapshots**:
```json
{
  "snapshots": [
    {
      "id": "snap-{guid}",
      "label": "Before assignment",
      "createdAt": "2026-06-09T10:05:00Z",
      "threadId": 1,
      "functionName": "Program.Main",
      "variableCount": 12
    }
  ],
  "count": 1
}
```

**Alternatives considered**:
- Osobne eventy `SnapshotAdded`/`SnapshotRemoved`: nadmiarowe dla resource notification — jeden `Changed` wystarczy
- Hook przez `SnapshotService` zamiast `SnapshotStore`: service jest dalej od danych; Store jest właściwym miejscem

---

## R-005: process_read_output / process_write_input — fake-async fix

**Decision**: Zmienić sygnaturę obu metod na `string` (synchroniczną) — usunąć `Task.FromResult()` wrapper.

**Rationale**:
- `ProcessIoManager.ReadOutput()` jest synchroniczne — czyta z thread-safe bufora (`lock` + `StringBuilder`)
- `ProcessIoManager.WriteInput()` jest synchroniczne — pisze do `StreamWriter` + `Flush()`
- Zachowanie z `Task.FromResult()` jest technicznie poprawne, ale sygnatura `Task<string>` kłamie: implikuje async I/O
- SDK MCP obsługuje zarówno synchroniczne (`string`) jak i asynchroniczne (`Task<string>`) metody narzędzi — zmiana nie wymaga zmian w SDK
- Inne synchroniczne tools (`StacktraceGetTool`, `VariablesGetTool`) zwracają `string` — spójność
- Prefix `Async` na metodzie MCP tool (`ReadOutputAsync`, `WriteInputAsync`) jest mylący — zmiana nazwy na `ReadOutput` / `WriteInput`

**Alternatives considered**:
- Zostawić `Task.FromResult()` z poprawionym komentarzem: leczy symptom, nie przyczynę
- Zaimplementować prawdziwe async (streaming I/O): poza zakresem tej featurki; bufferowany model jest wystarczający

---

## R-006: WaitForBreakpointAsync / _pendingHit / _hitWaiter — usunięcie

**Decision**: Usunąć `WaitForBreakpointAsync` z `IBreakpointManager`, `BreakpointManager` i związane pola `_pendingHit`, `_hitWaiter`, `_hitLock`.

**Rationale**:
- Te elementy służą WYŁĄCZNIE `BreakpointWaitTool` który jest usuwany
- Mechanizm single-waiter TCS (`_hitWaiter`) jest problematyczny: drugi agent czekający na ten sam breakpoint traci event
- `BreakpointNotifier` (Channel<T>) jest właściwym mechanizmem — fire-and-forget, wielu konsumentów możliwych
- Usunięcie czyści ~60 linii kodu i upraszcza `BreakpointManager`

---

## R-007: Constitution — long-running operations timeout

**Conflict**: Konstytucja mówi "Long-running operations MUST support progress reporting or timeout mechanisms". `breakpoint_wait` zapewniał jawny timeout. Po usunięciu — co jest zastępstwem?

**Resolution**: Nie jest to naruszenie konstytucji po następującym rozumowaniu:
- `debug_continue` zwraca natychmiast — nie jest to long-running operation w sensie MCP (nie blokuje klienta)
- Klient ZAWSZE może wywołać `debug_pause` żeby przerwać wykonanie — to jest timeout escape hatch
- `debugger/sessionStateChanged` notification informuje kiedy sesja się zatrzymała — analogia do "completion signal"
- Nowa kombinacja: agent woła `debug_continue`, opcjonalnie nastawia własny timer, jeśli nie dostaje `sessionStateChanged` w N sekundach woła `debug_pause`
- Konstytucja wymaga że SERWER wspiera timeout mechanisms — agent ma `debug_pause` (server-side operation) który jest zawsze dostępny

**Documented**: W plan.md Constitution Check sekcja zawiera uzasadnienie.

---

## R-008: Blocking breakpoints — weryfikacja że notyfikacje są wysyłane

**Finding**: Potwierdzone — `BreakpointManager.OnBreakpointHit()` wywołuje `_notifier.SendBreakpointHitAsync(notification)` dla WSZYSTKICH typów breakpointów (blocking i tracepoints). Jedynym suppressorem jest `ShouldSendNotification()` (warunki: enabled, maxNotifications, hitCountMultiple). Blokowanie (pauza) jest oddzielną ścieżką od notyfikacji.

**Implication**: `BreakpointWaitTool` jest zbędny nie tylko architekturalnie — notyfikacje działają już dla blocking breakpointów. Usunięcie go nie wymaga zmiany logiki notyfikacji dla blocking breakpointów.
