# Contract: `resharper_inspect_project`

**Class**: `DebugMcp.Tools.ReSharperInspectProjectTool`
**MCP annotations**: `ReadOnly=true, Destructive=false, Idempotent=true, OpenWorld=true`
**Title**: `Inspect Project (ReSharper)`
**Registered only when**: ReSharper integration enabled (no `--no-resharper`).

Runs ReSharper InspectCode over a single project and returns structured findings. Same engine,
acquisition, severity model, capping, and error envelope as `resharper_inspect_solution`; the
difference is the target is a `.csproj` and there is no cross-project `project` scope param.

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `projectPath` | string | yes | — | Absolute path to a `.csproj` file. |
| `severity` | string | no | engine default (suggestion and higher) | Minimum native severity: `error`\|`warning`\|`suggestion`\|`hint`. |
| `noBuild` | bool | no | `false` | Skip the engine's pre-analysis build. |
| `timeoutSeconds` | int | no | options `InspectionTimeoutSeconds` (300) | Per-call inspection budget; bounded 10–1800. |
| `maxResults` | int | no | options `MaxResults` (500) | Upper bound on returned findings; capped at 500. |

## Success response

Identical shape to `resharper_inspect_solution`, with `target` set to the `.csproj` path and
`project` on each finding equal to that project.

```json
{
  "success": true,
  "data": {
    "target": "/abs/MyApp/MyApp.csproj",
    "findings": [ /* InspectionFinding[] */ ],
    "total_count": 0, "returned_count": 0, "truncated": false,
    "limited_to": 500, "summary": {},
    "engine_version": "2026.1.2", "duration_ms": 5310, "built": true
  }
}
```

## Error responses

Same code set as `resharper_inspect_solution` except `PROJECT_NOT_FOUND` does not apply
(no scope param). `INVALID_PATH` triggers when `projectPath` is missing/not found/not a
`.csproj`.

## Behavioural contract

Same four guarantees as `resharper_inspect_solution` (server stays responsive; lazy cached
acquisition; full logging; severity filtering strictly reduces results).
