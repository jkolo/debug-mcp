# Phase 0 Research: ReSharper Inspections

All decisions below resolve the Technical Context. There are no remaining NEEDS CLARIFICATION
items (the three product-level ambiguities were settled in `spec.md` → Clarifications).

## R1. Engine distribution & acquisition

**Decision**: Acquire `JetBrains.ReSharper.GlobalTools` at runtime via
`dotnet tool install JetBrains.ReSharper.GlobalTools --tool-path <cacheDir>/<version> --version <version>`,
into a version-pinned isolated directory under `~/.debug-mcp/resharper/`. Invoke the engine
through the installed `jb` shim: `jb inspectcode ...`. Default pinned version: `2026.1.2`.

**Rationale**:
- Single official package; ~180 MB — far too large to bundle inside the debug-mcp dotnet
  tool package. Runtime acquisition keeps debug-mcp small (matches the symbol-server model,
  which also downloads on demand).
- `--tool-path` gives an **isolated, side-effect-free** install (no global tool list
  mutation, no machine-wide state), and lets us pin/parallel-version cleanly — directly
  analogous to `PersistentSymbolCache` writing under `~/.debug-mcp/symbols`.
- `.NET 8.0`-targeted package is forward-compatible with the host net10 runtime; it is a
  cross-platform dotnet tool (Linux/macOS/Windows, arm64).
- InspectCode is the **free** tool in the package — no license key required for inspections.

**Alternatives considered**:
- *Bundle the engine in the package* — rejected: bloats the tool ~180 MB, defeats lazy use.
- *Global install (`-g`)* — rejected: mutates the user's global tool list and a shared
  version; not isolated, harder to pin per debug-mcp.
- *Local tool-manifest restore* — rejected: requires a manifest in the user's solution dir
  and `dotnet tool restore` semantics tied to CWD; the isolated `--tool-path` cache is
  self-contained and CWD-independent.
- *`JetBrains.ReSharper.CommandLineTools` (zip package)* — rejected: not a dotnet tool;
  would require manual unzip/exec-bit handling; the GlobalTools dotnet-tool path is cleaner.

## R2. Lazy acquisition, readiness & concurrency

**Decision**: `IReSharperEngineProvider.EnsureEngineAsync(ct)` performs check-then-install:
1. Compute `toolPath = <cacheDir>/<version>` and `jbShim = toolPath/jb` (`jb.exe` on Windows).
2. **Readiness**: engine is "ready" iff the shim exists **and** a sentinel `.installed`
   marker file (written last, containing the version) exists — guarding against partially
   completed installs.
3. If not ready: acquire under a lock. Use an in-process `SemaphoreSlim(1,1)` plus a
   cross-process lock file (`<cacheDir>/<version>.lock` opened exclusively) so two debug-mcp
   processes don't install concurrently. After a successful `dotnet tool install`, write the
   `.installed` marker.
4. If a partial/corrupt install is detected (shim missing but dir exists), delete the dir and
   re-install.
5. Acquisition is invoked **only from tool calls**, never at startup (FR-004).

**Rationale**: Mirrors robust download-cache patterns; the marker-after-copy idiom is the
same "atomic completion signal" approach used by symbol caching. Lock file handles the
multi-session environment.

**Alternatives considered**:
- *No locking* — rejected: concurrent first-run installs could corrupt the tool-path.
- *Re-install every run* — rejected: defeats caching (FR-005), enormous latency.

## R3. Prerequisite & failure detection

**Decision**: Before acquisition, verify the `dotnet` CLI is available (probe `dotnet --version`
with a short timeout). Map failures to distinct error codes:
- `dotnet` missing/unrunnable → `PREREQUISITE_MISSING`.
- `dotnet tool install` non-zero / network failure → `ENGINE_ACQUISITION_FAILED` (include
  captured stderr tail + remediation hint: check connectivity or pass `--no-resharper`).
- cache dir not writable → `ENGINE_ACQUISITION_FAILED` with the offending path.
- acquisition exceeds acquisition timeout → `TIMEOUT` (phase=acquisition).

**Rationale**: FR-013 demands distinct, actionable errors that never crash the server. The
provider returns typed failures the service maps to the standard error envelope.

**Alternatives considered**: Generic catch-all error — rejected: violates FR-013's "distinct".

## R4. Invocation & command line

**Decision**: `ReSharperCliRunner` runs:
`jb inspectcode <target> --output=<tmp>.sarif --format=Sarif [--severity=<NATIVE>] [--project=<name>] [--no-build]`
- `<target>`: `.sln` (solution tool) or `.csproj` (project tool).
- `--format=Sarif` passed explicitly (deterministic; SARIF is default since 2024.1 but we
  don't rely on the default).
- `--output` to a unique temp file (`Path.GetTempFileName()`-derived, unique per call), parsed
  then deleted in a `finally`.
- `--severity` passed only when a threshold is requested; values use ReSharper's native names
  (`ERROR`/`WARNING`/`SUGGESTION`/`HINT`).
- `--no-build` passed when the caller opts to skip the pre-analysis build.
- Process run via `System.Diagnostics.Process` with redirected stdout/stderr, cancellation
  wired to the inspection timeout (kill process tree on cancel).

**Rationale**: Minimal, documented flag subset (R1 + spec scope). Temp-file output avoids
parsing engine chatter from stdout. Explicit format/severity = reproducibility.

**Alternatives considered**:
- *Parse stdout text* — rejected: brittle, localized, not machine-stable.
- *XML format as primary* — rejected as primary (deprecated), but see R5 fallback.

## R5. SARIF parsing → native severity (the key fidelity risk)

**Decision**: Parse SARIF 2.1.0 with `System.Text.Json`:
- Findings from `runs[].results[]`: `ruleId` → finding id; `message.text` → message;
  `locations[0].physicalLocation.artifactLocation.uri` (+ `uriBaseId` resolution) → file;
  `...region.startLine`/`startColumn`/`endLine`/`endColumn` → location.
- **Native severity**: SARIF's result `level` is coarse (error/warning/note/none) and would
  collapse *suggestion* and *hint* into *note*. To preserve native severity verbatim
  (per clarification), resolve severity in this priority order:
  1. the per-result ReSharper severity property if present (`result.properties` — exact key
     to be confirmed against a recorded sample; ReSharper has historically emitted the issue
     severity here),
  2. else the originating rule's configured severity from
     `runs[].tool.driver.rules[]` (matched by `ruleId`),
  3. else a documented mapping from SARIF `level` (error→Error, warning→Warning, note→
     Suggestion, none→Hint) as a last resort.
- **Verification task (early, before parser GREEN)**: record one real SARIF file by running
  `jb inspectcode` on `tests/ReSharperSampleApp` and commit it as the parser fixture; the
  characterization test pins the exact property path that yields suggestion ≠ hint. If SARIF
  proves to genuinely lack the suggestion/hint distinction, **fall back to `--format=Xml`**
  whose `<Issue Severity="SUGGESTION">` attribute is unambiguous; the parser interface stays
  the same (string in → `InspectionResult` out), only the format flag + parser branch change.

**Rationale**: SARIF is the modern, structured, stable default. The clarification mandates
native severities, so the parser must not lose the suggestion/hint distinction — hence the
recorded-fixture verification and the explicit XML fallback escape hatch. Keeping
`ISarifInspectionParser` behind an interface means the fixture test drives the precise
extraction without guessing now.

**Alternatives considered**:
- *Trust SARIF `level` only* — rejected: lossy, violates "native severity verbatim".
- *Always use XML* — rejected: deprecated format; prefer SARIF unless proven necessary.

**REALIZED (2026-06-15, verified against real engine 2026.1.2 output)**: SARIF was confirmed
lossy — both `result.level` AND `rule.defaultConfiguration.level` emit `note` for suggestion
**and** hint (no distinction). The documented fallback is therefore the implemented choice:
the runner uses `--format=Xml` and the parser (`InspectionReportParser`) reads native severity
from `<IssueType Severity="ERROR|WARNING|SUGGESTION|HINT"/>`, plus `Category` and `WikiUrl`
(help link); `<Issue>` carries `TypeId`, `File` (relative → absolutized), `Line`, `Message`.
Trade-off accepted: the XML `<Issue>` has no column/end position, so `column`/`end_line`/
`end_column` are null (the data model already permits this). Interface renamed
`ISarifInspectionParser` → `IInspectionReportParser` to reflect the format.

## R6. Opt-out wiring (parity with Roslyn)

**Decision**: Add `--no-resharper` option in `Program.cs` (mirroring `--no-roslyn` at lines
27–31). Build `ReSharperOptions` (Enabled=false when flag/env set). Register services only
when enabled (mirroring lines 186–189). Extend the tool-type filter (line 192–194):
`.Where(t => resharperEnabled || !t.Name.StartsWith("ReSharper", StringComparison.Ordinal))`.
Log enabled/disabled at startup (mirroring lines 259–266). Optional extra flags for parity:
`--resharper-cache` and `--resharper-version` (+ env `DEBUG_MCP_RESHARPER_CACHE`,
`DEBUG_MCP_RESHARPER_VERSION`, `DEBUG_MCP_NO_RESHARPER`).

**Rationale**: The user explicitly asked for "the same way as Roslyn." The name-prefix filter
is exactly the existing mechanism; `ReSharper`-prefixed classes are excluded as a group.
`Code`-prefixed and `ReSharper`-prefixed filters are independent (no overlap).

**Alternatives considered**:
- *Attribute-based opt-out marker* — rejected: introduces a new mechanism where the existing
  name-prefix filter already works and is what the user referenced.

## R7. Time budget model (per clarification)

**Decision**: Two separate, overridable timeouts on `ReSharperOptions`:
- `AcquisitionTimeoutSeconds` (default **600**) — governs the one-time `dotnet tool install`.
- `InspectionTimeoutSeconds` (default **300**) — governs a single `jb inspectcode` run; also
  overridable per call via a tool `timeoutSeconds` parameter (bounded, e.g. 10–1800s).
Exceeding either yields a `TIMEOUT` error tagged with the phase (acquisition vs inspection).

**Rationale**: The constitution's 30s default is correct for in-process debugger ops but
wrong for a 180 MB download + solution build; the clarification chose separated budgets so a
rare expensive acquisition never starves recurring inspections.

## R8. Result bounding

**Decision**: `InspectionResult` caps findings at `MaxResults` (default **500**, matching
`code_get_diagnostics`' max), sets `truncated=true` and reports `total_count` (pre-cap) vs the
returned slice. Severity counts are computed over the returned (capped) set, documented as such.

**Rationale**: FR-014 + keeps AI payloads bounded. 500 matches the existing diagnostics cap
for consistency.

## R9. Tool surface & naming (per clarification)

**Decision**: Two granular tools:
- `resharper_inspect_solution(solutionPath, severity?, project?, noBuild?, timeoutSeconds?, maxResults?)`
- `resharper_inspect_project(projectPath, severity?, noBuild?, timeoutSeconds?, maxResults?)`

Annotations: `ReadOnly=true, Destructive=false, Idempotent=true, OpenWorld=true` — OpenWorld=true
because the first call may reach the network to acquire the engine (distinct from the purely
local Roslyn tools, which are OpenWorld=false). Class names `ReSharperInspectSolutionTool` /
`ReSharperInspectProjectTool`.

**Rationale**: User chose multiple granular, scope-specific tools. `noun_verb(_qualifier)`
matches `code_find_usages`/`code_goto_definition`. `OpenWorld=true` honestly signals the
possible network acquisition.

**Alternatives considered**:
- *Single parameterized `resharper_inspect`* — rejected per clarification.
- *Add a `resharper` warm-up/install tool* — rejected for v1: lazy install is implicit;
  no separate tool needed (keeps surface minimal).

## R10. Testability strategy (Test-First gate)

**Decision**:
- `SarifInspectionParser` is a **pure** function (string → `InspectionResult`), unit-tested
  against committed SARIF fixtures — covers severity extraction (incl. suggestion vs hint),
  multi-location, missing-location findings, capping/truncation, empty results, malformed JSON.
- `ReSharperInspectionService` unit-tested with **faked** `IReSharperEngineProvider` and
  `IReSharperRunner` — covers happy path, invalid-target, project-scope-not-found, inspection
  timeout, acquisition failure, prerequisite missing, build failure.
- `ToolAnnotationTests` extended with the 2 new entries; the count assertion bumps 37 → 39.
- One **opt-in** Integration test runs the real engine on `tests/ReSharperSampleApp` and
  asserts a known ReSharper-only issue is found and that `code_get_diagnostics` does not
  report it (SC-002) — tiered with the existing flaky/integration tests (skipped in dev,
  because of the 180 MB download + build).

**Rationale**: Satisfies NON-NEGOTIABLE Test-First without making the fast test suite depend
on a giant download. The interface seams exist purely to enable this.

## Summary of resolved unknowns

| # | Unknown | Resolution |
|---|---------|-----------|
| R1 | How to get/run the engine | `JetBrains.ReSharper.GlobalTools` via `--tool-path`, `jb inspectcode`, pinned 2026.1.2 |
| R2 | Lazy install + concurrency | `EnsureEngineAsync` check-then-install, sentinel marker, semaphore + lock file |
| R3 | Failure detection | Probe dotnet; distinct codes PREREQUISITE_MISSING / ENGINE_ACQUISITION_FAILED / TIMEOUT |
| R4 | Invocation | `jb inspectcode <t> -o=<tmp> -f=Sarif [--severity] [--project] [--no-build]`, temp output |
| R5 | Native severity from SARIF | Result/rule property → fixture-verified; XML `Severity` attribute as fallback |
| R6 | Opt-out | `--no-resharper` + conditional DI + `ReSharper` name-prefix tool filter (Roslyn parity) |
| R7 | Timeouts | Separate acquisition (600s) + inspection (300s, per-call overridable) budgets |
| R8 | Bounding | Cap 500 + truncated flag + pre-cap total_count |
| R9 | Tool surface | `resharper_inspect_solution` + `resharper_inspect_project`, OpenWorld=true |
| R10 | Test-First | Pure parser + faked seams; opt-in integration test for the real engine |
