# Quickstart & Validation: ReSharper Inspections

End-to-end validation that the feature works. References [data-model.md](./data-model.md),
[contracts/](./contracts/), and the spec's Success Criteria (SC-001…SC-006).

## Prerequisites

- .NET 10 SDK (repo `global.json`).
- Network access for the one-time engine acquisition (first inspection only).
- A sample solution with a ReSharper-only issue: `tests/ReSharperSampleApp/` (created by this
  feature) — e.g. a `RedundantCast` or unused private member that the C# compiler does not flag.

## Build & fast tests (no engine download)

```bash
dotnet build
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
```

Expected:
- `SarifInspectionParserTests` green — native severity preserved (suggestion ≠ hint),
  locations parsed, capping/truncation, empty + malformed handled.
- `ReSharperInspectionServiceTests` green — happy path + every error mode (faked seams).
- `ReSharperOptionsTests` green — CLI > env > default precedence.
- `ToolAnnotationTests.ExpectedAnnotations_Covers39Tools` green (37 → 39).

## Manual run (real engine, first-use acquisition)

Start the server and drive it via your MCP client (or the dev harness):

```bash
dotnet run --project DebugMcp
```

### SC-001 / SC-003 — acquire-then-inspect, then cache reuse

1. Call `resharper_inspect_solution { "solutionPath": "<abs>/tests/ReSharperSampleApp/ReSharperSampleApp.sln" }`.
   - First call: engine is downloaded to `~/.debug-mcp/resharper/2026.1.2/` (watch the log:
     "ReSharper engine acquiring…" → "acquired"), build runs, findings return. No manual
     install was performed (**SC-001**).
2. Call the same tool again.
   - Log shows a cache hit (no re-download); inspection starts immediately (**SC-003**).

### SC-002 — adds coverage beyond Roslyn

3. `code_load { "path": "<abs>/tests/ReSharperSampleApp/ReSharperSampleApp.sln" }` then
   `code_get_diagnostics {}` — note the known issue (e.g. `RedundantCast`) is **absent**.
4. The `resharper_inspect_solution` result from step 1 **contains** that issue with its native
   severity and correct file/line → ReSharper adds coverage Roslyn lacks.

### SC-006 — filtering & scoping reduce results

5. Re-run with `{ "severity": "warning" }` → only error+warning findings (suggestions/hints
   gone).
6. Re-run with `{ "project": "ReSharperSampleApp" }` → only that project's findings.
7. `resharper_inspect_project { "projectPath": "<abs>/tests/ReSharperSampleApp/ReSharperSampleApp.csproj" }`
   → project-scoped subset.

### SC-005 — graceful failures

8. Invalid target: `resharper_inspect_solution { "solutionPath": "/nope.txt" }` → `INVALID_PATH`.
9. Tiny budget: `{ "solutionPath": "<sln>", "timeoutSeconds": 10 }` on a cold build →
   `TIMEOUT` with `details.phase`. Server stays responsive; other tools still work.
10. Offline first-use (no cache, network down) → `ENGINE_ACQUISITION_FAILED` with remediation
    text; server does not crash.

### SC-004 — opt-out parity with Roslyn

```bash
dotnet run --project DebugMcp -- --no-resharper
```

11. The advertised tool list contains **no** `resharper_*` tools; startup log says
    "ReSharper inspection tools disabled (--no-resharper)". Every other tool (including the
    Roslyn `code_*` tools) works unchanged.

## Opt-in integration test (CI / on demand — downloads the engine)

```bash
dotnet test tests/DebugMcp.Tests \
  --filter "FullyQualifiedName~Integration.ReSharperInspectionIntegrationTests"
```

Runs the real engine on `tests/ReSharperSampleApp` and asserts SC-002 programmatically
(ReSharper finds the seeded issue; `code_get_diagnostics` does not). Tiered with the existing
flaky/integration tests — **skipped in normal dev runs** because of the ~180 MB download +
build.

## Done-when checklist

- [ ] Fast unit+contract suite green, including the 37→39 annotation count.
- [ ] First inspection acquires the engine with zero manual steps; second reuses cache.
- [ ] A ReSharper-only issue appears in `resharper_*` output but not in `code_get_diagnostics`.
- [ ] `severity` and `project` filters demonstrably shrink the result set.
- [ ] Each failure mode returns its distinct error code without crashing the server.
- [ ] `--no-resharper` removes the `resharper_*` tools; everything else unaffected.
