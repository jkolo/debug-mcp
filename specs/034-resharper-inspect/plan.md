# Implementation Plan: ReSharper Inspections

**Branch**: `034-resharper-inspect` | **Date**: 2026-06-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/034-resharper-inspect/spec.md`

## Summary

Add JetBrains ReSharper static inspection to debug-mcp as a complement to the existing
Roslyn `code_*` tools. The ReSharper command-line engine (`JetBrains.ReSharper.GlobalTools`)
is acquired lazily on first use into a version-pinned, per-user disk cache (mirroring the
symbol-server cache pattern), then invoked via `jb inspectcode` to produce SARIF output,
which is parsed into structured findings carrying ReSharper's **native** severity
(error/warning/suggestion/hint). The integration is **default-on, opt-out** via a
`--no-resharper` flag, mirroring the Roslyn `--no-roslyn` pattern (conditional DI + tool-class
name-prefix filter). Two granular, scope-specific MCP tools are exposed:
`resharper_inspect_solution` and `resharper_inspect_project`. Time budgets are split into a
long one-time **acquisition** timeout and a per-run **inspection** timeout, both overridable.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (pinned in `global.json`)

**Primary Dependencies**:
- `JetBrains.ReSharper.GlobalTools` (NuGet, acquired at runtime as a `--tool-path` dotnet tool; pinned default version `2026.1.2`) — provides `jb inspectcode`
- .NET SDK on host (`dotnet` CLI) — used to `dotnet tool install` the engine and to build the target
- `System.Text.Json` — SARIF parsing (no new package; SARIF 2.1.0 is plain JSON)
- ModelContextProtocol 1.2.0 (existing) — MCP tool surface

**Storage**: Disk cache for the acquired engine at `~/.debug-mcp/resharper/<version>/` (overridable). Per-run SARIF written to a temp file, parsed, then deleted. No database.

**Testing**: xUnit + FluentAssertions + Moq (existing). SARIF parsing and the inspection service are unit-tested with committed SARIF fixture files (no engine needed). One opt-in Integration test actually runs the engine (large download — tiered with the existing flaky/integration tests, skipped in dev).

**Target Platform**: Cross-platform (Linux/macOS/Windows, x64/arm64), consistent with debug-mcp.

**Project Type**: Single project (.NET MCP server packaged as a dotnet tool).

**Performance Goals**: Cached-engine inspection of a small solution returns in a single tool call within the default inspection timeout (300s). SARIF parsing of a typical result set (≤ a few thousand findings) completes in well under 1s. No throughput target (interactive, one inspection per call).

**Constraints**:
- First-use acquisition is a ~180 MB download + tool install — MUST be lazy (never at startup) and MUST NOT block server startup; only the invoking call waits.
- Acquisition timeout default 600s; inspection timeout default 300s; both overridable (CLI/env and per-call for inspection).
- Result sets bounded by a documented max (default 500) with a truncation indicator.
- Engine version pinned for reproducibility.

**Scale/Scope**: 2 new MCP tools, ~4 new services/abstractions (engine provider, CLI runner, SARIF parser, inspection orchestrator), 1 options record, ~4 new models, new error codes, 1 SARIF fixture + 1 tiny sample solution for the opt-in integration test. Tool count 37 → 39.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
|-----------|------------|---------|
| **I. Native First** | ReSharper inspection is **static source analysis**, entirely outside ICorDebug's domain (ICorDebug is a runtime debugger; it cannot perform source-level lint inspections). This is the same category as the existing Roslyn `code_*` tools (feature 015), which also do not use ICorDebug. Principle I permits external tooling where ICorDebug lacks the capability, with the gap documented — done here and precedented. | **PASS** (documented) |
| **II. MCP Compliance** | Tools named `noun_verb` (`resharper_inspect_solution`, `resharper_inspect_project`); JSON-Schema params with descriptions; structured JSON `{success, data|error}` responses identical to `code_*`; structured error objects with code/message/details; both blocking ops accept overridable timeouts. | **PASS** |
| **III. Test-First (NON-NEGOTIABLE)** | Design isolates a pure `SarifInspectionParser` and a fakeable `IReSharperRunner`/`IReSharperEngineProvider` so the happy path, severity extraction, capping, and every error mode are unit-testable without the 180 MB engine. Tests authored before implementation (RED→GREEN→REFACTOR). Contract tests assert tool annotations + schema. | **PASS** |
| **IV. Simplicity** | Process shell-out + JSON parse; no new abstraction layers beyond what testability requires (provider, runner, parser, orchestrator) — each ≤3 levels of indirection. Two tools per the user's granular-surface decision. CleanupCode and other engine tools explicitly deferred. | **PASS** |
| **V. Observability** | Each tool logs ToolInvoked/ToolCompleted/ToolError (sanitised params, duration, outcome) exactly like `code_*`; acquisition start/success/failure logged; disabled state logged at startup. | **PASS** |

No violations → Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/034-resharper-inspect/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (tool + service contracts)
│   ├── resharper_inspect_solution.md
│   ├── resharper_inspect_project.md
│   └── inspection-service.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
DebugMcp/
├── Program.cs                                   # + --no-resharper flag, ReSharperOptions, conditional DI, tool filter, startup log
├── Models/
│   ├── ErrorResponse.cs                         # + new ErrorCodes constants (ENGINE_ACQUISITION_FAILED, PREREQUISITE_MISSING, INSPECTION_FAILED, BUILD_FAILED)
│   └── ReSharper/                               # NEW models (positional records / JsonPropertyName records)
│       ├── InspectionFinding.cs                 # id, severity (native), message, category, file, line, column, project, help_link
│       ├── InspectionResult.cs                  # findings, counts-by-severity, truncated flag, target, duration
│       └── ReSharperSeverity.cs                 # enum: Error, Warning, Suggestion, Hint
├── Services/
│   └── ReSharper/                               # NEW
│       ├── ReSharperOptions.cs                  # Create(): CLI > env > default (mirrors SymbolServerOptions)
│       ├── IReSharperEngineProvider.cs          # EnsureEngineAsync() → path to `jb`; lazy install + cache + locking
│       ├── ReSharperEngineProvider.cs
│       ├── IReSharperRunner.cs                  # RunInspectCodeAsync(request) → raw SARIF (abstraction seam for tests)
│       ├── ReSharperCliRunner.cs               # shells out to `jb inspectcode ... -f=Sarif -o=<temp>`
│       ├── ISarifInspectionParser.cs           # PURE: SARIF string → InspectionResult
│       ├── SarifInspectionParser.cs
│       ├── IReSharperInspectionService.cs       # orchestrates provider→runner→parser, cap, error mapping
│       └── ReSharperInspectionService.cs
└── Tools/
    ├── ReSharperInspectSolutionTool.cs          # resharper_inspect_solution
    └── ReSharperInspectProjectTool.cs           # resharper_inspect_project

tests/
├── DebugMcp.Tests/
│   ├── Unit/ReSharper/
│   │   ├── SarifInspectionParserTests.cs        # fixture-driven: severity extraction, locations, capping, empty, malformed
│   │   ├── ReSharperInspectionServiceTests.cs   # fakes runner+provider: success, scope error, timeout, acquisition failure
│   │   └── ReSharperOptionsTests.cs             # CLI>env>default precedence, tilde expansion
│   ├── Contract/
│   │   └── ToolAnnotationTests.cs               # + 2 entries; count 37 → 39
│   ├── Integration/
│   │   └── ReSharperInspectionIntegrationTests.cs  # OPT-IN: real engine on sample solution (skipped in dev)
│   └── Fixtures/ReSharper/
│       └── sample-inspection.sarif              # recorded SARIF with a known ReSharper-only issue
└── ReSharperSampleApp/                          # tiny solution with a redundant-cast / unused-member (ReSharper-only)
```

**Structure Decision**: Single-project layout (debug-mcp). New code is grouped under
`Services/ReSharper/`, `Models/ReSharper/`, and two `Tools/ReSharper*` classes, mirroring the
existing `Services/CodeAnalysis/` + `Tools/Code*` and `Services/Symbols/` groupings. The
class-name prefix `ReSharper` is the hook for the opt-out registration filter.

## Complexity Tracking

> No constitution violations — section intentionally empty.
