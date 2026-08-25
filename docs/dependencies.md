# Dependency management

This document explains *why* the versions pinned in [`Directory.Packages.props`](../Directory.Packages.props) are what they are — the reasoning that doesn't fit in a one-line comment. If you're bumping a dependency, read the relevant section first.

## Central Package Management

All package versions live in one file: `Directory.Packages.props` at the repo root (`ManagePackageVersionsCentrally=true`). Every `.csproj` references packages by name only (`<PackageReference Include="..." />`, no `Version` attribute). To bump a version, edit `Directory.Packages.props` — nothing else needs to change unless the bump also renames a package or a package moves in/out of a project's dependency list.

## Roslyn and MSBuild must track the installed SDK

`Microsoft.CodeAnalysis.CSharp.Workspaces`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`, `Microsoft.Build`, `Microsoft.Build.Framework`, `Microsoft.Build.Tasks.Core`, and `Microsoft.NET.StringTools` are referenced with `ExcludeAssets="runtime" PrivateAssets="all"` in `DebugMcp/DebugMcp.csproj`. That's deliberate, not an oversight: `MSBuildLocator` (used by the code-analysis feature to spin up `MSBuildWorkspace`) loads the *actual* MSBuild and Roslyn assemblies from the .NET SDK installed on the machine at runtime, not from NuGet. If the `PackageVersion` pins in `Directory.Packages.props` don't match what the installed SDK ships, you get assembly-version mismatches — typically a `FileNotFoundException` or a binding-redirect failure the first time `code_load` actually touches `MSBuildWorkspace`, not at build time.

**Rule: whenever you bump the SDK (`global.json`), re-check these two families against what that SDK actually ships**, and bump them to match:

```bash
# Read the exact versions the installed SDK carries:
ls /usr/share/dotnet/sdk/<version>/Microsoft.Build.dll
exiftool -FileVersion /usr/share/dotnet/sdk/<version>/Microsoft.Build.dll
exiftool -FileVersion /usr/share/dotnet/sdk/<version>/Roslyn/bincore/Microsoft.CodeAnalysis.CSharp.dll
```

As of SDK 10.0.400: `Microsoft.Build.*` → 18.9.6, `Microsoft.CodeAnalysis.*` → 5.9.0. These are the versions currently pinned.

`Microsoft.Build.Locator` is *not* part of this family — it's a small shim that just finds and registers the SDK's MSBuild at startup; it doesn't need to track the SDK version itself.

## Test assertion library: AwesomeAssertions, not FluentAssertions

FluentAssertions 7.2.2 is the last release under the free Apache-2.0-equivalent license. Starting with v8, FluentAssertions requires a paid Xceed Community/Commercial license for most usage. That's incompatible with this project's MIT-adjacent (AGPL-3.0) open-source posture, so the project does not track FluentAssertions past 7.x.

[AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions) is a community fork of FluentAssertions, forked from the same 8.x lineage, published under Apache-2.0. As of its 9.0.0 release it renamed its own namespace from `FluentAssertions` to `AwesomeAssertions` (the project itself was renamed), so migrating is **not** a silent drop-in — every `using FluentAssertions;` becomes `using AwesomeAssertions;`, and any API surface that changed between FluentAssertions 7.x and 8.x (which AwesomeAssertions carries forward) needs auditing. In this codebase that meant two renamed assertion methods:

| Old (FluentAssertions 7.x) | New (AwesomeAssertions 9.x) |
|---|---|
| `BeLessOrEqualTo` | `BeLessThanOrEqualTo` |
| `HaveCountLessOrEqualTo` | `HaveCountLessThanOrEqualTo` |

(`BeGreaterThanOrEqualTo` was already spelled that way in FA7 — unaffected.)

If a future AwesomeAssertions bump breaks the build, it's almost certainly another renamed/removed assertion method — check the [AwesomeAssertions changelog](https://github.com/AwesomeAssertions/AwesomeAssertions/releases) and grep for the failing method name across `tests/`.

## MCP SDK: the `MCP9005` warning is intentional debt

`ModelContextProtocol`/`ModelContextProtocol.Core` moved to 2.x, aligned with MCP spec `2026-07-28`. That spec deprecated the Logging, Sampling, and Roots capabilities ([SEP-2577](https://modelcontextprotocol.io/seps/2577-deprecate-roots-sampling-and-logging)) — Sampling and Roots are unused here, but `DebugMcp/Infrastructure/McpLogger.cs` implements MCP Logging (`notifications/message`, `McpServer.LoggingLevel`) as `ILogger` push-through to the connected client (feature 016).

The SDK marks the underlying APIs `[Obsolete]`, which surfaces as compiler warning `MCP9005`. Per the SDK's versioning policy the deprecated APIs keep working for **at least 12 months** from the spec revision. Rather than silence this project-wide, `DebugMcp/DebugMcp.csproj` suppresses it narrowly with a comment pointing here and to ROADMAP #065 — the eventual migration to stderr/OpenTelemetry, which `McpLogger` already partially supports via `LoggingOptions.EnableStderr`.

**Do not widen the `<NoWarn>` scope** (e.g. to `Directory.Build.props`) to silence a *different* warning — each new suppression needs its own justification and its own line.

## Deliberately not adopted from MCP SDK v2

Reviewed and rejected as not applicable to this server's shape (stdio, single client, no HTTP transport):

- **Multi-round-trip requests (MRTR)** and **`subscriptions/listen`** (SEP-2575) — both assume richer client/server interaction patterns than a single stdio session. Revisit only if the server ever grows an HTTP transport.
- **Stateless-HTTP-by-default, discovery-first negotiation, OAuth hardening** — the entire HTTP/OAuth breaking-change surface of MCP SDK v2.0. Not applicable; the server has no HTTP transport at all.

## ReSharper engine version lives in code, not in CPM

`JetBrains.ReSharper.GlobalTools` is *not* a `PackageVersion` in `Directory.Packages.props` — it's a dotnet tool the server installs lazily at runtime into `~/.debug-mcp/resharper/<version>/` on first use of `resharper_inspect_*`. The pinned version is `ReSharperOptions.DefaultVersion` in `DebugMcp/Services/ReSharper/ReSharperOptions.cs`.

Bumping it has a cost the other dependencies don't: every user re-downloads the engine (~180–650 MB depending on platform) once, on their next first use, because the cache is keyed by version. Verify a bump with the opt-in `ReSharperInspectionIntegrationTests` (`dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ReSharperInspectionIntegrationTests" --blame-hang-timeout 15m`) before merging — a new engine major/minor can change inspection output format (this happened once already: SARIF became lossy for `suggestion` vs `hint` severity between versions, which is why the parser reads native XML output instead).

Users can override the pinned version without a code change via `--resharper-version` / `DEBUG_MCP_RESHARPER_VERSION`.
