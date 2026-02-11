# Quickstart: Cross-Platform Support Verification

## Prerequisites

- .NET 10 SDK installed
- Git checkout on `025-cross-platform` branch

## Step 1: Build (all platforms)

```bash
dotnet build
```

Expected: 0 errors, 0 warnings. Build succeeds without `RuntimeIdentifier` hardcoded.

## Step 2: Run unit + contract tests

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
```

Expected: All tests pass, including new `PlatformDetectionTests`.

## Step 3: Verify platform detection (current platform)

```bash
dotnet run --project DebugMcp -- --version
```

Expected: Version prints on any supported platform (Linux, Windows, macOS).

## Step 4: Verify NuGet pack (RID-agnostic)

```bash
dotnet pack DebugMcp/DebugMcp.csproj -c Release -o ./nupkg
```

Expected:
- Single `.nupkg` file produced (no RID suffix in filename)
- Package size < 50 MB
- Inspect contents: `unzip -l ./nupkg/*.nupkg | grep dbgshim` shows all 6 native libraries

## Step 5: Verify DbgShim discovery

```bash
dotnet run --project DebugMcp -- --help
```

Expected: Tool starts without "dbgshim library not found" warnings in debug output.

## Step 6: CI validation

Push branch to GitHub and verify:
- CI runs on 3 runners (ubuntu, windows, macos)
- Build succeeds on all 3
- Unit tests pass on all 3

## Smoke Test (manual, per platform)

On each target platform (Windows, macOS, Linux):

```bash
# Install from local nupkg
dotnet tool install -g debug-mcp --add-source ./nupkg

# Verify launch + breakpoint + inspect workflow
# (requires a test .NET app to debug)
debug-mcp --version
```
