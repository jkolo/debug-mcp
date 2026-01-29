# Implementation Plan: CI/CD Pipeline for Tag-Based NuGet Publishing

**Branch**: `011-ci-cd-pipeline` | **Date**: 2026-01-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/011-ci-cd-pipeline/spec.md`

## Summary

Add GitHub Actions workflows for automated CI (build+test on push/PR) and CD (tag-triggered NuGet publishing to NuGet.org + GitHub Packages with GitHub Release creation). Pin .NET SDK via `global.json` and bump all .NET-related NuGet packages to latest versions.

## Technical Context

**Language/Version**: C# / .NET 10.0 (pinned via `global.json`)
**Primary Dependencies**: GitHub Actions, `dotnet` CLI, NuGet.org, GitHub Packages
**Storage**: N/A
**Testing**: xUnit, Reqnroll (existing test suites run as CI gate)
**Target Platform**: GitHub Actions `ubuntu-latest` runner
**Project Type**: CI/CD configuration (YAML workflow files)
**Performance Goals**: Release pipeline completes within 5 minutes of tag push
**Constraints**: .NET 10 preview SDK required; native DbgShim dependency must be included in package
**Scale/Scope**: 2 workflow files, 1 `global.json`, NuGet package version bumps

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ Pass | No changes to debugging architecture; CI/CD only |
| II. MCP Compliance | ✅ Pass | No tool API changes |
| III. Test-First | ✅ Pass | CI workflow runs all tests as gate before publish |
| IV. Simplicity | ✅ Pass | Minimal workflow files; no custom actions or scripts |
| V. Observability | ✅ Pass | GitHub Actions provides built-in logging; Matrix notification for failures |

All gates pass. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/011-ci-cd-pipeline/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
.github/
└── workflows/
    ├── ci.yml           # Build + test on push/PR
    └── release.yml      # Tag-triggered build, pack, publish, release

global.json              # Pin .NET SDK version

DotnetMcp/
├── DotnetMcp.csproj     # Remove hardcoded Version (set by CI)
└── ...                  # Existing source unchanged

tests/
└── ...                  # Existing tests unchanged
```

**Structure Decision**: Only CI/CD configuration files added at repository root. No changes to source code structure. `global.json` pins SDK for reproducible builds.
