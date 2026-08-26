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

## Tools

### resharper_inspect_solution

Run ReSharper's code inspections over an entire .NET solution.

**Requires:** No session needed (works anytime — this is static analysis, not a debug session)

**When to use:** You want ReSharper-grade findings (hundreds of inspections beyond the C#
compiler / Roslyn) across every project in a solution at once. See [Self-installing
engine](#self-installing-engine) and [Opt-out](#opt-out) above.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `solutionPath` | string | Yes | Absolute path to the `.sln` file |
| `severity` | string | No | Minimum **native** severity: `error` \| `warning` \| `suggestion` \| `hint` |
| `project` | string | No | Restrict the inspection to a single project within the solution |
| `noBuild` | boolean | No | Skip the engine's pre-analysis build when already built (default: false) |
| `timeoutSeconds` | integer | No | Per-call inspection budget, 10–1800s; excludes the one-time engine download (separate budget) (default: 300) |
| `maxResults` | integer | No | Cap on returned findings (default/max 500) |

**Example:**
```json
{
  "solutionPath": "/abs/MyApp.sln",
  "severity": "warning",
  "project": "MyApp.Core"
}
```

See [Example response](#example-response) and [Errors](#errors) below — both tools share the same response shape and error codes.

**Real-world use case:** Before opening a pull request, an AI agent runs `resharper_inspect_solution` across the whole solution to catch redundancies, dead code, and style violations that Roslyn's built-in analyzers don't cover.

---

### resharper_inspect_project

Run ReSharper's code inspections over a single .NET project.

**Requires:** No session needed (works anytime — this is static analysis, not a debug session)

**When to use:** You want ReSharper findings scoped to one project rather than a whole
solution — faster turnaround when you only care about the project you're actively changing. See
[Self-installing engine](#self-installing-engine) and [Opt-out](#opt-out) above.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `projectPath` | string | Yes | Absolute path to the `.csproj` file |
| `severity` | string | No | Minimum **native** severity: `error` \| `warning` \| `suggestion` \| `hint` |
| `noBuild` | boolean | No | Skip the engine's pre-analysis build when already built (default: false) |
| `timeoutSeconds` | integer | No | Per-call inspection budget, 10–1800s; excludes the one-time engine download (separate budget) (default: 300) |
| `maxResults` | integer | No | Cap on returned findings (default/max 500) |

**Example:**
```json
{
  "projectPath": "/abs/MyApp/MyApp.csproj",
  "severity": "suggestion"
}
```

See [Example response](#example-response) and [Errors](#errors) below — both tools share the same response shape and error codes.

**Real-world use case:** An AI agent iterating on a single library project runs `resharper_inspect_project` after each change for fast feedback, saving the full-solution `resharper_inspect_solution` sweep for before a PR.

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
