# Implementation Plan: MCP Protocol Logging

**Branch**: `014-mcp-logging` | **Date**: 2026-02-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/014-mcp-logging/spec.md`

## Summary

Replace native console/stderr logging with MCP protocol logging using the `notifications/message` notification method. The existing Microsoft.Extensions.Logging infrastructure will be extended with an MCP-aware ILoggerProvider that sends structured log messages to connected MCP clients, while optionally maintaining stderr output via CLI flag.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: ModelContextProtocol SDK 0.1.0-preview.13, Microsoft.Extensions.Logging, System.CommandLine
**Storage**: N/A (in-memory log level state)
**Testing**: xUnit, Reqnroll E2E tests
**Target Platform**: Linux (linux-x64), cross-platform .NET
**Project Type**: Single project (CLI tool / MCP server)
**Performance Goals**: Non-blocking log delivery, no measurable impact on debugger operations
**Constraints**: 64KB max payload size, default "info" log level
**Scale/Scope**: ~15 existing log points in Infrastructure/Logging.cs

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ PASS | Not applicable - logging feature doesn't involve ICorDebug |
| II. MCP Compliance | ✅ PASS | Using standard `notifications/message` protocol method |
| III. Test-First | ⚠️ REQUIRED | Must write tests before implementation |
| IV. Simplicity | ✅ PASS | Extending existing ILogger infrastructure, single ILoggerProvider |
| V. Observability | ✅ PASS | Core purpose is to improve observability via MCP |

**Gate Result**: PASS - proceed to Phase 0

### Post-Phase 1 Re-check

| Principle | Status | Design Validation |
|-----------|--------|-------------------|
| I. Native First | ✅ PASS | No ICorDebug involvement |
| II. MCP Compliance | ✅ PASS | Uses `notifications/message`, declares `logging` capability |
| III. Test-First | ⚠️ ENFORCED | Tasks must include test-first workflow |
| IV. Simplicity | ✅ PASS | Single `McpLoggerProvider` class, no new abstractions |
| V. Observability | ✅ PASS | All logs flow through standard ILogger → MCP |

**Post-Design Gate Result**: PASS - ready for `/speckit.tasks`

## Project Structure

### Documentation (this feature)

```text
specs/014-mcp-logging/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
DebugMcp/
├── Infrastructure/
│   ├── Logging.cs                    # Existing - log message definitions
│   └── McpLoggerProvider.cs          # NEW - ILoggerProvider for MCP
├── Program.cs                        # MODIFY - add CLI flag, register provider
└── ...

tests/
├── DebugMcp.Tests/
│   └── Infrastructure/
│       └── McpLoggerProviderTests.cs # NEW - unit tests
└── DebugMcp.E2E/
    └── Features/
        └── Logging.feature           # NEW - E2E logging scenarios
```

**Structure Decision**: Single project structure maintained. New `McpLoggerProvider` class in Infrastructure folder alongside existing `Logging.cs`.

## Complexity Tracking

> No violations - design stays within constitution bounds.

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| ILoggerProvider | Single class | MCP SDK provides `ClientLoggerProvider` pattern; we follow it |
| CLI flag | System.CommandLine option | Already used in Program.cs |
| Log level state | In-memory field | MCP SDK tracks via `LoggingLevel` property |
