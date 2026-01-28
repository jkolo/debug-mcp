# Research: .NET Tool Packaging

## R1: PackAsTool Configuration

**Decision**: Add `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>dotnet-mcp</ToolCommandName>` to .csproj PropertyGroup.

**Rationale**: These are the minimal required properties to enable .NET tool packaging. `ToolCommandName` defines the CLI command name; without it, the assembly name is used.

**Alternatives considered**:
- Omitting `ToolCommandName` (defaults to assembly name `DotnetMcp`) — rejected: `dotnet-mcp` is more conventional and recognizable

## R2: Native Dependency Bundling (DbgShim)

**Decision**: Use `linux-x64` as the only `RuntimeIdentifier` for now. The `Microsoft.Diagnostics.DbgShim.linux-x64` NuGet package provides native libraries that will be included in the tool package automatically when packed with `-r linux-x64`.

**Rationale**: The tool currently only supports Linux x64 due to the DbgShim dependency. NuGet's RID-specific packaging handles native library bundling. Single-platform keeps the build simple.

**Alternatives considered**:
- Multi-platform packages with `ToolPackageRuntimeIdentifiers` — deferred until Windows/macOS DbgShim support is added
- Self-contained deployment — rejected per clarification: framework-dependent chosen for smaller package size

## R3: NuGet Metadata

**Decision**: Add standard NuGet metadata: PackageId, Version, Authors, Description, PackageLicenseExpression, RepositoryUrl, PackageTags.

**Rationale**: Required for NuGet.org publishing and discoverability. SPDX license expression is the modern approach (not deprecated licenseUrl).

**Alternatives considered**:
- Minimal metadata (just PackageId) — rejected: poor discoverability on NuGet.org

## R4: Version Display (--version flag)

**Decision**: Implement `--version` flag in Program.cs by reading the assembly version. Set `<Version>1.0.0</Version>` in .csproj (per clarification).

**Rationale**: .NET tools don't have built-in `--version` support; this must be implemented manually. Reading `AssemblyInformationalVersion` provides the most complete version string.

**Alternatives considered**:
- Using System.CommandLine library for argument parsing — rejected per Simplicity principle: only 2 flags needed
- Hardcoding version string — rejected: diverges from package version over time

## R5: RollForward Policy

**Decision**: No special roll-forward configuration needed. Users on newer .NET versions can use `--allow-roll-forward` at install time (available since .NET 9 SDK).

**Rationale**: Framework-dependent .NET tools use the installed runtime. The default behavior (exact framework match) is appropriate.

**Alternatives considered**:
- Targeting older framework (net8.0) for broader compatibility — rejected: .NET 10 features are in use
