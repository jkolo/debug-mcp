# Phase 1 Data Model: ReSharper Inspections

All models are immutable records following project conventions (positional or
`required`-init records; `[JsonPropertyName]` snake_case to match `code_*` JSON; timestamps
as `DateTimeOffset`). Namespaces: models in `DebugMcp.Models.ReSharper`, options/services in
`DebugMcp.Services.ReSharper`.

## Enums

### `ReSharperSeverity`

Native ReSharper severity vocabulary, exposed verbatim (no remap to Roslyn's scale).

| Value | Native name | SARIF `level` (coarse) |
|-------|-------------|------------------------|
| `Error` | ERROR | error |
| `Warning` | WARNING | warning |
| `Suggestion` | SUGGESTION | note |
| `Hint` | HINT | note |

- Serialized lower-case (`error`/`warning`/`suggestion`/`hint`) to match `code_*` severity
  rendering (`ToLowerInvariant`).
- Ordering for threshold filtering (descending importance): Error > Warning > Suggestion > Hint.
  A `minSeverity=warning` returns Error+Warning only.

## Models

### `InspectionFinding`

One reported ReSharper issue. Mirrors `DiagnosticInfo` field-for-field where meaningful so AI
consumers see a consistent shape across Roslyn and ReSharper.

| Field | JSON | Type | Notes |
|-------|------|------|-------|
| Id | `id` | `string` (required) | ReSharper inspection/rule id (e.g. `RedundantCast`) |
| Message | `message` | `string` (required) | Human-readable issue text |
| Severity | `severity` | `ReSharperSeverity` (required) | Native severity, verbatim |
| Category | `category` | `string?` | ReSharper inspection category/group |
| File | `file` | `string?` | Absolute path; null for non-file-scoped (solution-level) issues |
| Line | `line` | `int?` | 1-based start line |
| Column | `column` | `int?` | 1-based start column |
| EndLine | `end_line` | `int?` | 1-based end line |
| EndColumn | `end_column` | `int?` | 1-based end column |
| Project | `project` | `string?` | Originating project name where known |
| HelpLink | `help_link` | `string?` | Rule help URL if provided by the engine |

**Validation**: `Id`, `Message`, `Severity` always present. Location fields are all-or-nothing
per finding (a finding either has a physical location or none — never a line without a file).

### `InspectionResult`

Outcome of one inspection run (the `data` payload of a tool success response).

| Field | JSON | Type | Notes |
|-------|------|------|-------|
| Target | `target` | `string` (required) | Absolute path of the inspected `.sln`/`.csproj` |
| Findings | `findings` | `IReadOnlyList<InspectionFinding>` | Capped to `max_results` |
| TotalCount | `total_count` | `int` | Pre-cap count of findings at/above the requested severity |
| ReturnedCount | `returned_count` | `int` | `findings.Count` (post-cap) |
| Truncated | `truncated` | `bool` | True when `TotalCount > ReturnedCount` |
| MaxResults | `limited_to` | `int` | The cap applied (default 500) |
| Summary | `summary` | `IReadOnlyDictionary<string,int>` | Count by native severity over returned set (`{"warning":3,"suggestion":12}`) |
| EngineVersion | `engine_version` | `string` | Pinned engine version used |
| DurationMs | `duration_ms` | `long` | Inspection wall-clock (excludes one-time acquisition) |
| BuiltTarget | `built` | `bool` | Whether the engine built before analysis (false when no-build) |

**Relationships**: `InspectionResult` 1→* `InspectionFinding`.

### `ReSharperOptions` (record, `Services/ReSharper`)

Configuration resolved CLI > env > default (mirrors `SymbolServerOptions.Create`).

| Field | Default | CLI | Env |
|-------|---------|-----|-----|
| `Enabled` | `true` | `--no-resharper` (sets false) | `DEBUG_MCP_NO_RESHARPER` (1/true/yes) |
| `CacheDirectory` | `~/.debug-mcp/resharper` | `--resharper-cache` | `DEBUG_MCP_RESHARPER_CACHE` |
| `Version` | `2026.1.2` | `--resharper-version` | `DEBUG_MCP_RESHARPER_VERSION` |
| `AcquisitionTimeoutSeconds` | `600` | — | `DEBUG_MCP_RESHARPER_ACQUIRE_TIMEOUT` |
| `InspectionTimeoutSeconds` | `300` | — | `DEBUG_MCP_RESHARPER_INSPECT_TIMEOUT` |
| `MaxResults` | `500` | — | `DEBUG_MCP_RESHARPER_MAX_RESULTS` |

- `CacheDirectory` expands a leading `~`. The engine for a given version lives at
  `CacheDirectory/<Version>/` with the `jb` shim and an `.installed` sentinel.

### `EngineInstallState` (internal, transient — not serialized)

Returned by `IReSharperEngineProvider` to the service.

| Field | Type | Notes |
|-------|------|-------|
| JbPath | `string` | Absolute path to the `jb`/`jb.exe` shim |
| Version | `string` | Installed/pinned version |
| Acquired | `bool` | True if this call performed an install (vs cache hit) — for logging |

## Inspection Request (parameters, not a persisted entity)

Captured directly as tool method parameters (no request record needed):

| Param | Tool(s) | Type | Default | Notes |
|-------|---------|------|---------|-------|
| `solutionPath` | solution | `string` (required) | — | Absolute `.sln` path |
| `projectPath` | project | `string` (required) | — | Absolute `.csproj` path |
| `severity` | both | `string?` | none (engine default: suggestion+) | One of error/warning/suggestion/hint; native |
| `project` | solution only | `string?` | none | Scope a solution inspection to one project |
| `noBuild` | both | `bool` | `false` | Skip the pre-analysis build |
| `timeoutSeconds` | both | `int?` | options `InspectionTimeoutSeconds` | Per-call inspection budget (bounded 10–1800) |
| `maxResults` | both | `int?` | options `MaxResults` | Bounded ≤ 500 |

## Error Codes (added to `Models/ErrorResponse.cs` → `ErrorCodes`)

| Constant | Value | When |
|----------|-------|------|
| `PrerequisiteMissing` | `PREREQUISITE_MISSING` | `dotnet` CLI not available for acquisition |
| `EngineAcquisitionFailed` | `ENGINE_ACQUISITION_FAILED` | install failed / offline / cache dir unwritable |
| `InspectionFailed` | `INSPECTION_FAILED` | engine ran but errored / unparolseable output / crash |
| `BuildFailed` | `BUILD_FAILED` | engine's pre-analysis build failed |

Reused existing codes: `Timeout` (TIMEOUT, with `details.phase` = acquisition\|inspection),
`InvalidPath` (bad/missing target), `InvalidParameter` (bad severity/bounds),
`ProjectNotFound` (solution scope names a non-existent project).
