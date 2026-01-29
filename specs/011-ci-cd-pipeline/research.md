# Research: CI/CD Pipeline

## R1: .NET SDK Version Pinning

**Decision**: Pin to .NET SDK `10.0.102` via `global.json` with `rollForward: latestPatch`
**Rationale**: Ensures reproducible builds locally and in CI. `latestPatch` allows automatic patch updates without breaking changes.
**Alternatives**: No pinning (risk of SDK mismatch between local and CI), exact pin without rollForward (too rigid).

## R2: GitHub Actions .NET Setup

**Decision**: Use `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'` and `dotnet-quality: 'preview'`
**Rationale**: .NET 10 is currently preview. The action handles SDK installation on `ubuntu-latest`.
**Alternatives**: Manual SDK download (fragile), container-based builds (unnecessary overhead).

## R3: NuGet Publishing Strategy

**Decision**: Use `dotnet nuget push` with `--source nuget.org` and `--source github` in separate steps
**Rationale**: Separate steps allow partial failure handling (NuGet.org success + GitHub Packages failure = partial success).
**Alternatives**: Single push to both (no partial failure control).

## R4: Version Extraction from Tag

**Decision**: Extract version via `${GITHUB_REF_NAME#v}` (shell parameter expansion stripping `v` prefix)
**Rationale**: Simplest approach, no external tools. Tag `v1.2.3` → version `1.2.3`. Works for pre-release too: `v1.0.0-beta.1` → `1.0.0-beta.1`.
**Alternatives**: Regex extraction (overkill), GitVersion (too complex for this use case).

## R5: Matrix Notification

**Decision**: Use `s3krit/matrix-message-action` (or similar) to send failure notifications to a Matrix room
**Rationale**: Lightweight GitHub Action for Matrix messaging. Requires `MATRIX_HOMESERVER`, `MATRIX_TOKEN`, `MATRIX_ROOM_ID` secrets.
**Alternatives**: Custom webhook script (more maintenance), no notification (fallback if Matrix not configured).

## R6: NuGet Package Version Override

**Decision**: Pass version via `dotnet pack -p:Version=$VERSION` instead of hardcoding in `.csproj`
**Rationale**: The `.csproj` should not contain a hardcoded version — CI derives it from the git tag. Remove `<Version>1.0.0</Version>` from `.csproj`.
**Alternatives**: Use `Directory.Build.props` (unnecessary for single project).

## R7: GitHub Packages NuGet Source

**Decision**: Configure GitHub Packages as NuGet source with `--source "https://nuget.pkg.github.com/jkolo/index.json"` using `GITHUB_TOKEN`
**Rationale**: Built-in authentication via `GITHUB_TOKEN`, no additional secrets needed.
**Alternatives**: PAT-based auth (less secure, requires manual secret rotation).
