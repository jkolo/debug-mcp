# Feature Specification: CI/CD Pipeline for Tag-Based NuGet Publishing

**Feature Branch**: `011-ci-cd-pipeline`
**Created**: 2026-01-29
**Status**: Draft
**Input**: User description: "przygotuj na github pipeline CI/CD, który przy każdym tago vX.X.X będzie budował toola i publikował release nuget na github oraz na nuget.org. Pamiętaj że ma się instalować jako tool dotnet-mcp"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automated Release on Version Tag (Priority: P1)

A maintainer pushes a version tag (e.g., `v1.0.0`) to the repository. A GitHub Actions workflow automatically builds the tool, creates a NuGet package, publishes it to both NuGet.org and GitHub Packages, and creates a GitHub Release with the package attached.

**Why this priority**: This is the core CI/CD capability. Without automated publishing, releases require manual steps that are error-prone and time-consuming.

**Independent Test**: Can be tested by pushing a tag `v0.0.1-test` and verifying the workflow runs, builds, and produces artifacts.

**Acceptance Scenarios**:

1. **Given** a commit on `main` is tagged with `v1.2.3`, **When** the tag is pushed to GitHub, **Then** a GitHub Actions workflow triggers automatically
2. **Given** the workflow is triggered by a version tag, **When** the build completes successfully, **Then** a NuGet package `dotnet-mcp` version `1.2.3` is created
3. **Given** the package is built, **When** publishing step runs, **Then** the package is published to NuGet.org and users can install it via `dotnet tool install -g dotnet-mcp`
4. **Given** the package is built, **When** publishing step runs, **Then** the package is also published to GitHub Packages for the repository
5. **Given** publishing succeeds, **When** the workflow completes, **Then** a GitHub Release is created with tag name and the `.nupkg` file attached

---

### User Story 2 - Build Validation on Every Push (Priority: P2)

A contributor pushes code or opens a pull request. A CI workflow automatically builds the project and runs tests to catch regressions before merge.

**Why this priority**: Continuous integration prevents broken code from reaching `main`, ensuring that tagged releases always build from a healthy codebase.

**Independent Test**: Can be tested by opening a PR with a deliberate test failure and verifying the workflow reports failure.

**Acceptance Scenarios**:

1. **Given** a push to any branch or a pull request is opened, **When** the CI workflow triggers, **Then** the project is built and all tests are executed
2. **Given** the build or tests fail, **When** the workflow completes, **Then** the failure is reported clearly in the PR checks
3. **Given** the build and tests succeed, **When** the workflow completes, **Then** the PR shows a green check

---

### Edge Cases

- What happens when a tag doesn't match the `vX.X.X` pattern (e.g., `v1.0.0-beta.1`)?
  - Pre-release tags like `v1.0.0-beta.1` should also trigger the pipeline and publish as a pre-release NuGet package
- What happens when the NuGet.org publish fails (e.g., version already exists)?
  - The workflow should fail with a clear error message; GitHub Release should not be created if publishing fails
- What happens when the tag is pushed to a non-main branch?
  - Only tags on `main` branch should trigger the release workflow
- What happens when GitHub Packages publish fails but NuGet.org succeeds?
  - The workflow should report partial failure but not roll back the NuGet.org publish

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A GitHub Actions workflow MUST trigger on tags matching the pattern `v*.*.*`
- **FR-002**: The workflow MUST extract the version number from the tag (stripping the `v` prefix) and use it as the NuGet package version
- **FR-003**: The workflow MUST build the project in Release configuration for `linux-x64` runtime
- **FR-004**: The workflow MUST run all tests before publishing (build validation gate)
- **FR-005**: The workflow MUST publish the NuGet package to NuGet.org
- **FR-006**: The workflow MUST publish the NuGet package to GitHub Packages
- **FR-007**: The workflow MUST create a GitHub Release with the tag name and attach the `.nupkg` file
- **FR-008**: The published package MUST be installable as a .NET tool via `dotnet tool install -g dotnet-mcp`
- **FR-009**: A separate CI workflow MUST run build and tests on every push and pull request
- **FR-010**: The release workflow MUST use repository secrets for NuGet.org API key (`NUGET_API_KEY`)
- **FR-011**: The repository MUST include a `global.json` pinning the .NET SDK to the latest available .NET 10 version
- **FR-012**: All .NET-related NuGet packages MUST be bumped to their latest versions matching the pinned .NET SDK
- **FR-013**: The workflow SHOULD send a Matrix webhook notification on failure; if Matrix integration is not feasible, default GitHub notifications suffice

### Key Entities

- **Version Tag**: Git tag matching `vX.X.X` pattern that triggers the release pipeline
- **NuGet Package**: The `.nupkg` artifact published to NuGet.org and GitHub Packages, installable as `dotnet-mcp`
- **GitHub Release**: A release entry on the repository with attached artifacts
- **Repository Secrets**: `NUGET_API_KEY` for NuGet.org publishing; `GITHUB_TOKEN` is provided automatically

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A release is fully automated — from tag push to published NuGet package — with zero manual steps
- **SC-002**: Users can install the newly released version via `dotnet tool install -g dotnet-mcp` within 5 minutes of tag push
- **SC-003**: Every pull request receives automated build and test feedback before merge
- **SC-004**: The pipeline correctly handles semantic versioning including pre-release tags (e.g., `v1.0.0-beta.1`)

## Assumptions

- The repository is hosted on GitHub and uses GitHub Actions for CI/CD
- NuGet.org API key will be provided by the maintainer as a repository secret (`NUGET_API_KEY`)
- The project already has `PackAsTool` and `ToolCommandName` configured in `.csproj` (done in feature 010)
- The `.csproj` `Version` property will be overridden by the workflow at build time (not hardcoded)
- GitHub Packages uses the built-in `GITHUB_TOKEN` for authentication
- The workflow targets `ubuntu-latest` runner with .NET 10 SDK
- .NET SDK version is pinned via `global.json` to the latest available .NET 10 release

## Clarifications

### Session 2026-01-29

- Q: Should the .NET SDK version be pinned in CI? → A: Yes, pin to latest available .NET 10 SDK via `global.json`; also bump all .NET-related NuGet packages to latest versions
- Q: How should maintainers be notified of workflow failures? → A: Send Matrix webhook notification on failure if feasible; otherwise default GitHub notifications only
