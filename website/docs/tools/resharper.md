# ReSharper Inspections

Two tools run JetBrains **ReSharper** code inspections over your .NET code and return
structured findings — hundreds of inspections beyond what the C# compiler / Roslyn report.
They complement (do not replace) the [code analysis tools](./code-analysis.md).

| Tool | Scope |
|------|-------|
| `resharper_inspect_solution` | A whole solution (`.sln`) |
| `resharper_inspect_project` | A single project (`.csproj`) |

## Self-installing engine

On the **first** inspection, debug-mcp downloads and caches the ReSharper command-line engine
(`JetBrains.ReSharper.GlobalTools`, a ~180 MB one-time acquisition) into
`~/.debug-mcp/resharper/<version>/`. No manual installation is required. Every later
inspection reuses the cached, version-pinned engine. The download happens lazily on first use
— never at server startup — and requires network access and a .NET SDK on the host.

## Opt-out

The integration is **on by default**, exactly like the Roslyn `code_*` tools. Disable it (and
skip the engine download entirely) with:

```bash
debug-mcp --no-resharper
```

When disabled, the `resharper_*` tools are not advertised and every other tool keeps working.
Related flags: `--resharper-cache <dir>`, `--resharper-version <ver>` (and env vars
`DEBUG_MCP_NO_RESHARPER`, `DEBUG_MCP_RESHARPER_CACHE`, `DEBUG_MCP_RESHARPER_VERSION`).

## Parameters

| Param | Tools | Description |
|-------|-------|-------------|
| `solutionPath` / `projectPath` | resp. | Absolute path to the `.sln` / `.csproj`. |
| `severity` | both | Minimum **native** severity: `error` \| `warning` \| `suggestion` \| `hint`. |
| `project` | solution | Restrict a solution inspection to one project. |
| `noBuild` | both | Skip the engine's pre-analysis build when already built. |
| `timeoutSeconds` | both | Per-call inspection budget (10–1800s); excludes the one-time engine download (separate budget). |
| `maxResults` | both | Cap on returned findings (default/max 500). |

## Native severities

Findings report ReSharper's native severity **verbatim** — `error`, `warning`, `suggestion`,
`hint`. These are kept distinct (a ReSharper *suggestion* and *hint* are not merged), so the
threshold filter and `summary` counts reflect ReSharper's own classification rather than
Roslyn's coarser scale.

## Example response

```json
{
  "success": true,
  "data": {
    "target": "/abs/MyApp.sln",
    "findings": [
      {
        "id": "RedundantCast",
        "message": "Type cast is redundant",
        "severity": "warning",
        "category": "Redundancies in Code",
        "file": "/abs/MyApp/Calculator.cs",
        "line": 12,
        "project": "MyApp",
        "help_link": "https://www.jetbrains.com/help/resharper/RedundantCast.html"
      }
    ],
    "total_count": 1,
    "returned_count": 1,
    "truncated": false,
    "limited_to": 500,
    "summary": { "warning": 1 },
    "engine_version": "2026.1.2",
    "duration_ms": 8421,
    "built": true
  }
}
```

## Errors

Structured `{ "success": false, "error": { "code", "message", "details" } }` with codes:
`INVALID_PATH`, `INVALID_PARAMETER`, `PROJECT_NOT_FOUND`, `PREREQUISITE_MISSING` (no .NET SDK),
`ENGINE_ACQUISITION_FAILED` (offline / install failure), `BUILD_FAILED`, `INSPECTION_FAILED`,
and `TIMEOUT` (with `details.phase` = `acquisition` or `inspection`). A failure here never
crashes the server or affects other tools.
