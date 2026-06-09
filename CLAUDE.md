# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

debug-mcp is an MCP server that exposes .NET debugging as 40 structured tools. It interfaces directly with the .NET runtime via ICorDebug APIs (through the ClrDebug NuGet wrapper) — the same approach JetBrains Rider uses. AI agents connect via Model Context Protocol to launch processes, set breakpoints, step through code, inspect variables, and analyze exceptions.

## Build & Test Commands

```bash
dotnet build                    # Build everything (0 errors, 0 warnings expected)
dotnet build -c Release         # Release build

# Reliable tests only (unit + contract — use this for development)
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"

# Run a single test class
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ToolAnnotationTests"

# Run a single test method
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ToolAnnotationTests.Tool_Title_MatchesSpec"

# Run the tool locally
dotnet run --project DebugMcp
```

**Do NOT run** integration or performance tests without reason — they require a live debugger and are timing-sensitive:
- `ReattachmentTests.ReattachAfterProcessTermination_ShouldSucceed` — flaky
- `TerminateLaunchedProcessTests.GetLoadedModules_OnRunningLaunchedProcess_ShouldNotHang` — timing-dependent
- `ModulePerformanceTests.GetTypesAsync_WithPagination_ReturnsWithin2Seconds` — performance flaky

## Tech Stack

- **Runtime**: .NET 10.0 (pinned in `global.json`), C#, cross-platform (Windows/macOS/Linux, x64/arm64)
- **Debugging**: ClrDebug 0.3.4 (ICorDebug wrappers), DbgShim 9.0
- **MCP SDK**: ModelContextProtocol 1.2.0
- **Code Analysis**: Roslyn (Microsoft.CodeAnalysis 5.3.0)
- **Tests**: xUnit + FluentAssertions + Moq
- **E2E**: Reqnroll (Gherkin BDD) in `tests/DebugMcp.E2E/`
- **Docs site**: Docusaurus 3.9.2 in `website/`

## Architecture

### Layer Diagram

```
MCP Client (Claude, GPT, etc.)
    ↓ stdio (JSON-RPC)
Program.cs — DI container, MCP server setup, CLI options
    ↓
Tools/ (40 tools)          Resources/ (4 resources)       Completions/
    ↓                           ↓                              ↓
Services/
├── DebugSessionManager    — Session lifecycle (launch/attach/disconnect)
├── ProcessDebugger        — Low-level ICorDebug, callbacks, locking
├── Breakpoints/
│   ├── BreakpointManager  — Hit logic, condition eval, exception matching
│   ├── BreakpointRegistry — In-memory store (thread-safe)
│   ├── BreakpointNotifier — Channel<T> async notification queue
│   └── PdbSymbolReader    — Source location resolution from PDB
├── ExceptionAutopsyService — Full exception chain analysis
├── CodeAnalysis/          — Roslyn workspace (go-to-def, find usages)
└── Symbols/               — SSQP symbol server client + disk cache
    ↓
ClrDebug (ICorDebug) → .NET Runtime
```

### ICorDebug Callback Threading (Critical)

ProcessDebugger uses **two locks** — getting this wrong causes deadlocks:

- **`_lock`** — protects user API calls (Launch, Continue, Stop, etc.)
- **`_stateLock`** — protects state fields updated by ICorDebug callbacks

**Lock ordering invariant**: `_lock` → `_stateLock` is OK. **Reverse is FORBIDDEN.** ICorDebug callbacks fire synchronously on the ICorDebug thread while other code may hold `_lock` during `Stop()` or `Continue()`. Callbacks must never acquire `_lock`.

### Event Flow: Breakpoint Hit

```
ICorDebug thread → OnBreakpoint callback
  → UpdateState(Paused) under _stateLock only
  → Fire BreakpointHit event
    → BreakpointManager.OnBreakpointHit
      → Resolve location from PDB
      → Evaluate condition (if any)
      → If tracepoint: queue notification via Channel<T>
      → Set ShouldContinue flag on EventArgs
  → If ShouldContinue: call Continue(false) and UpdateState(Running)
  → If !ShouldContinue: stay paused (session manager signals waiters)
```

### Tool Registration

Tools are discovered via reflection: classes with `[McpServerToolType]`, methods with `[McpServerTool]`. Each tool receives dependencies via constructor DI. Tools return JSON strings with `{success: true/false, ...}` structure.

### Key Model Conventions

- **Positional records** for all models: `record Breakpoint(string Id, BreakpointLocation Location, ...)`
- **Immutable state** via `with` expressions: `var updated = bp with { HitCount = bp.HitCount + 1 };`
- **DateTimeOffset everywhere** — never use `DateTime`
- **ID prefixes**: breakpoints `bp-{guid}`, tracepoints `tp-{guid}`, exception breakpoints `ebp-{guid}`

## Project Layout

```
DebugMcp/                        # Main project (packaged as dotnet tool)
├── Program.cs                   # Entry point, DI, MCP server config
├── Tools/                       # 36 MCP tool classes
├── Services/                    # Core business logic
├── Models/                      # Positional records (Breakpoints/, Inspection/, Memory/, Modules/)
└── Infrastructure/              # Logging, MCP logger provider

tests/
├── DebugMcp.Tests/              # Main test project
│   ├── Unit/                    # Unit tests (mock-based)
│   ├── Contract/                # Contract tests (reflection, schema validation)
│   ├── Integration/             # Live debugger tests (flaky, skip in dev)
│   └── Performance/             # Benchmarks (flaky, skip in dev)
├── DebugMcp.E2E/                # Reqnroll BDD tests
├── DebugTestApp/                # Test target application
└── TestTargetApp/               # Another test target with sub-libraries

specs/                           # Feature specifications (001–028)
website/                         # Docusaurus docs site
```

## Feature Specification Workflow

Features are developed using the `speckit` workflow. Each feature gets a numbered directory under `specs/` (e.g., `specs/024-mcp-best-practices/`) containing:

- `spec.md` — requirements, user stories, acceptance criteria
- `plan.md` — implementation design
- `research.md` — technical investigation
- `tasks.md` — ordered task breakdown
- `quickstart.md` — verification steps
- `checklists/` — quality gates

Branch naming: `{number}-{short-name}` (e.g., `024-mcp-best-practices`).

## ClrDebug API Pitfalls

- `CorDebugStringValue`: use `stringValue.GetString((int)stringValue.Length)` — there is no `.String` property
- `IMcpServer.SendNotificationAsync` is an **extension method** — cannot be mocked with Moq
- Module cache uses a separate `_moduleCacheLock` to avoid deadlock between callback thread and module enumeration
- `TestProcessIoManager.Instance` is the singleton used in tests as the 3rd param to ProcessDebugger constructor


## Active Technologies
- C# 13 / .NET 10.0 + `Microsoft.CodeAnalysis.CSharp.Workspaces` (already referenced) — `CSharpSyntaxTree`, `CSharpSyntaxWalker`, `InvocationExpressionSyntax` (029-safe-eval-mode)
- N/A — stateless per-call analysis; `SafeEvalAllowlist` singleton constructed once at startup (029-safe-eval-mode)
- C# 13 / .NET 10.0 + ModelContextProtocol 1.3.0, ClrDebug 0.3.4, System.Threading.Channels, xUnit + FluentAssertions + Moq (030-mcp-event-driven)
- In-memory (SnapshotStore — ConcurrentDictionary, existing) (030-mcp-event-driven)
- C# 13 / .NET 10.0 + ClrDebug 0.3.4, ModelContextProtocol 1.3.0, System.Threading.Channels (already in use) (031-batch-evaluate)
- In-memory only; batch state lives in `BatchRunner` singleton for the duration of a run (031-batch-evaluate)

## Recent Changes
- 030-mcp-event-driven: Added 2 new MCP resources (`debugger://modules`, `debugger://snapshots`), `debugger/sessionStateChanged` notification, enriched `breakpointHit` payload with locals; removed 6 polling tools (35 tools total); fixed fake-async in `process_read_output`/`process_write_input`
- 029-safe-eval-mode: Added C# 13 / .NET 10.0 + `Microsoft.CodeAnalysis.CSharp.Workspaces` (already referenced) — `CSharpSyntaxTree`, `CSharpSyntaxWalker`, `InvocationExpressionSyntax`

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->
