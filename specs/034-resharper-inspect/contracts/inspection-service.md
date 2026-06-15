# Contract: ReSharper inspection services (internal seams)

These interfaces exist to make the feature testable without the 180 MB engine (Test-First
gate). All live in `DebugMcp.Services.ReSharper`.

## `IReSharperEngineProvider`

```csharp
public interface IReSharperEngineProvider
{
    /// Ensures the pinned engine is installed in the cache; acquires it lazily on first use.
    /// Throws ReSharperPrerequisiteException / ReSharperAcquisitionException / OperationCanceledException.
    Task<EngineInstallState> EnsureEngineAsync(CancellationToken cancellationToken);
}
```

**Contract**:
- Idempotent: a ready cache returns immediately with `Acquired=false`.
- Concurrency-safe: parallel calls do not double-install or corrupt the cache (semaphore +
  cross-process lock file).
- Partial-install detection: missing shim ⇒ re-acquire.
- Cancellation honoured against the **acquisition** timeout.

## `IReSharperRunner`

```csharp
public interface IReSharperRunner
{
    /// Runs `jb inspectcode` for the request and returns the raw SARIF document text.
    /// Throws ReSharperBuildFailedException / ReSharperRunFailedException / OperationCanceledException.
    Task<string> RunInspectCodeAsync(InspectionRunRequest request, string jbPath, CancellationToken cancellationToken);
}

public sealed record InspectionRunRequest(
    string Target, string? Severity, string? Project, bool NoBuild);
```

**Contract**:
- Writes SARIF to a unique temp file, returns its contents, deletes it in `finally`.
- Distinguishes a build failure from a run failure (separate exceptions → distinct codes).
- Cancellation kills the process tree and surfaces as `OperationCanceledException`.
- This is the seam faked in service unit tests (no real process spawned).

## `ISarifInspectionParser`

```csharp
public interface ISarifInspectionParser
{
    /// PURE: parse SARIF text into findings (native severity preserved). No I/O, no clock.
    IReadOnlyList<InspectionFinding> Parse(string sarifJson);
}
```

**Contract**:
- Preserves native severity (suggestion ≠ hint) per research R5.
- Findings with no physical location yield null file/line (never line-without-file).
- Malformed JSON throws a typed parse exception (mapped to `INSPECTION_FAILED`).
- Deterministic ordering: by file, then line, then id.

## `IReSharperInspectionService`

```csharp
public interface IReSharperInspectionService
{
    Task<InspectionResult> InspectAsync(
        string target,           // .sln or .csproj
        string? severity,
        string? project,         // null for project-scoped tool
        bool noBuild,
        int inspectionTimeoutSeconds,
        int maxResults,
        CancellationToken cancellationToken);
}
```

**Contract** (orchestration):
1. Validate target exists and has the right extension → `INVALID_PATH`.
2. Validate `severity`/bounds → `INVALID_PARAMETER`.
3. `EnsureEngineAsync` (acquisition timeout from options) → maps prerequisite/acquisition
   failures + acquisition `TIMEOUT`.
4. Apply a linked CTS with the inspection timeout for the run → inspection `TIMEOUT`.
5. `RunInspectCodeAsync` → `BUILD_FAILED` / `INSPECTION_FAILED`.
6. `Parse` → findings; for solution scope with a `project` that yields nothing AND that name
   isn't a known project → `PROJECT_NOT_FOUND`.
7. Sort, compute `total_count`, cap to `maxResults` (set `truncated`), build per-severity
   `summary`, stamp `engine_version`/`duration_ms`/`built`.

The tool classes are thin: parse/forward params, call `InspectAsync`, serialize the standard
`{success,data}` / `{success,error}` envelope, and log. Exceptions thrown by the service carry
a code the tool maps to the error envelope.
