# Research: Reqnroll E2E Tests

## R1: Reqnroll Framework Selection

**Decision**: Use Reqnroll 3.3.2 with xUnit integration (`Reqnroll.xUnit`)

**Rationale**:
- Existing test project uses xUnit 2.9.3 — Reqnroll.xUnit integrates natively
- Reqnroll is the active successor to SpecFlow (archived)
- Latest version 3.3.2 (Jan 2026) supports .NET 10
- Cucumber Expressions and Gherkin 35.0.0 for modern BDD syntax

**Alternatives considered**:
- LightBDD — lighter approach but no Gherkin feature files (user explicitly requested Gherkin)
- SpecFlow — archived, no longer maintained
- TUnit integration — newer runner but project already uses xUnit

## R2: Project Structure

**Decision**: Create a separate test project `tests/DotnetMcp.E2E/` for Reqnroll tests

**Rationale**:
- Reqnroll requires MSBuild integration (`Reqnroll.Tools.MsBuild.Generation`) for code-behind generation from `.feature` files
- Separate project avoids polluting existing unit/integration test project with BDD tooling
- Feature files and step definitions are self-contained
- Can reference existing `TestTargetApp` and shared helpers

**Alternatives considered**:
- Adding Reqnroll to existing `DotnetMcp.Tests` project — rejected because it mixes BDD and traditional testing concerns, and Reqnroll's MSBuild targets would affect all test compilation

## R3: State Sharing Between Steps

**Decision**: Use Reqnroll's built-in context injection (ScenarioContext + constructor injection)

**Rationale**:
- Reqnroll supports constructor injection of `ScenarioContext` into step definition classes
- A shared `DebuggerContext` class can hold session state, breakpoint references, etc.
- Hooks (`[BeforeScenario]`/`[AfterScenario]`) handle setup/teardown of debug sessions
- No external DI container needed for this scope

**Alternatives considered**:
- `Reqnroll.Microsoft.Extensions.DependencyInjection` — overkill for sharing debugger state between steps

## R4: Test Target Reuse

**Decision**: Reuse existing `TestTargetApp` and `TestTargetProcess` helper

**Rationale**:
- TestTargetApp already provides command-driven scenarios (loop, method, exception, nested, object)
- TestTargetProcess helper handles process lifecycle, stdin/stdout communication, and readiness handshake
- Existing line numbers for breakpoints are well-documented in integration tests
- No need to duplicate test infrastructure

## R5: Test Isolation

**Decision**: Each scenario gets a fresh debug session; test target process is shared per feature via `[BeforeFeature]`/`[AfterFeature]` hooks

**Rationale**:
- Starting a new .NET process per scenario is slow (~1-2s each)
- ICorDebug requires sequential access (no parallel debugging) — `[Collection]` equivalent needed
- Feature-level process sharing with scenario-level attach/detach balances speed and isolation
- Launch-specific scenarios use their own process lifecycle

## R6: Serial Execution

**Decision**: Configure xUnit to run Reqnroll features serially (not in parallel)

**Rationale**:
- ICorDebug can only debug one process at a time per debugger instance
- Existing integration tests use `[Collection("ProcessTests")]` for the same reason
- Reqnroll with xUnit uses `[Collection]` attribute on generated test classes or xUnit configuration
