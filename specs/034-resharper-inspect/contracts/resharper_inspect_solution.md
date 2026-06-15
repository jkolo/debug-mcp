# Contract: `resharper_inspect_solution`

**Class**: `DebugMcp.Tools.ReSharperInspectSolutionTool`
**MCP annotations**: `ReadOnly=true, Destructive=false, Idempotent=true, OpenWorld=true`
**Title**: `Inspect Solution (ReSharper)`
**Registered only when**: ReSharper integration enabled (no `--no-resharper`).

Runs ReSharper InspectCode over an entire solution and returns structured findings. On first
use, the engine is acquired automatically (lazy, cached); the call waits for acquisition.

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `solutionPath` | string | yes | — | Absolute path to a `.sln` file. |
| `severity` | string | no | engine default (suggestion and higher) | Minimum native severity: `error`\|`warning`\|`suggestion`\|`hint`. |
| `project` | string | no | all projects | Restrict the inspection to a single project in the solution. |
| `noBuild` | bool | no | `false` | Skip the engine's pre-analysis build (use when already built). |
| `timeoutSeconds` | int | no | options `InspectionTimeoutSeconds` (300) | Per-call inspection budget; bounded 10–1800. Does not include one-time engine acquisition (separate 600s budget). |
| `maxResults` | int | no | options `MaxResults` (500) | Upper bound on returned findings; capped at 500. |

## Success response

```json
{
  "success": true,
  "data": {
    "target": "/abs/MyApp.sln",
    "findings": [
      {
        "id": "RedundantCast",
        "message": "Redundant cast to 'int'",
        "severity": "warning",
        "category": "Redundancies in Code",
        "file": "/abs/MyApp/Calculator.cs",
        "line": 20, "column": 17, "end_line": 20, "end_column": 30,
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

- Zero findings at/above the threshold → `success:true`, `findings:[]`, `total_count:0`
  (NOT an error).
- Findings without a physical location omit `file`/`line` (solution-level issues).

## Error responses (envelope: `{ "success": false, "error": { "code", "message", "details?" } }`)

| Code | Trigger |
|------|---------|
| `INVALID_PATH` | `solutionPath` missing, not found, or not a `.sln`. |
| `INVALID_PARAMETER` | bad `severity` value or out-of-range `timeoutSeconds`/`maxResults`. |
| `PROJECT_NOT_FOUND` | `project` names a project not in the solution. |
| `PREREQUISITE_MISSING` | `dotnet` CLI unavailable to acquire the engine. |
| `ENGINE_ACQUISITION_FAILED` | engine download/install failed (offline / cache unwritable); `details` includes remediation. |
| `BUILD_FAILED` | the engine's pre-analysis build failed; `details` carries the build error tail. |
| `INSPECTION_FAILED` | engine crashed or produced unparseable output. |
| `TIMEOUT` | acquisition or inspection exceeded its budget; `details.phase` = `acquisition`\|`inspection`. |

## Behavioural contract

1. The server MUST remain responsive and all other tools functional regardless of any failure
   here.
2. Acquisition occurs only inside this call path (never at startup) and is reused on
   subsequent calls.
3. Every invocation logs ToolInvoked (sanitised params) → ToolCompleted(duration) or
   ToolError(code).
4. `severity` filtering and `project` scoping each strictly reduce the result set.
