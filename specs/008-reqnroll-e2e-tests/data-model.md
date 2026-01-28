# Data Model: Reqnroll E2E Tests

This feature is test-only — no new domain entities are introduced. The data model describes the shared test context objects used across Gherkin step definitions.

## DebuggerContext (Scenario-scoped)

Shared state object injected into all step definition classes via Reqnroll context injection.

| Field | Type | Description |
|-------|------|-------------|
| SessionManager | DebugSessionManager | Active debug session manager |
| ProcessDebugger | ProcessDebugger | Low-level debugger instance |
| BreakpointManager | BreakpointManager | Breakpoint lifecycle manager |
| TargetProcess | TestTargetProcess? | Test target process (if attached) |
| CurrentState | SessionState | Last observed session state |
| LastBreakpointHit | BreakpointHit? | Most recent breakpoint hit |
| SetBreakpoints | List&lt;Breakpoint&gt; | Breakpoints set during scenario |
| LastStackTrace | StackFrame[]? | Last retrieved stack trace |
| LastVariables | Variable[]? | Last retrieved variable list |
| LastEvalResult | EvalResult? | Last expression evaluation result |

## FeatureContext (Feature-scoped)

| Field | Type | Description |
|-------|------|-------------|
| SharedTargetProcess | TestTargetProcess? | Process shared across scenarios in a feature |

## State Transitions

```
Scenario lifecycle:
  [BeforeScenario] → Create DebuggerContext (fresh per scenario)
  [Given] steps    → Attach/Launch, set breakpoints
  [When] steps     → Trigger actions (continue, step, inspect)
  [Then] steps     → Assert outcomes
  [AfterScenario]  → Detach/Disconnect, cleanup
```
