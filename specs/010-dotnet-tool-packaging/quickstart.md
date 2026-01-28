# Quickstart: .NET Tool Packaging

## Install as Global Tool

```bash
# From NuGet.org
dotnet tool install -g dotnet-mcp

# From local package
dotnet tool install -g dotnet-mcp --add-source ./nupkg
```

## Install as Local Tool

```bash
# Initialize tool manifest (if not exists)
dotnet new tool-manifest

# Add the tool
dotnet tool install dotnet-mcp

# Run via tool manifest
dotnet tool run dotnet-mcp
```

## Run from Source

```bash
git clone <repo-url>
cd DotnetMcp
dotnet run --project DotnetMcp/DotnetMcp.csproj
```

## CLI Flags

```bash
dotnet-mcp --version    # Display version
dotnet-mcp --help       # Display usage
dotnet-mcp              # Start MCP server (stdio)
```

## Build Package Locally

```bash
dotnet pack DotnetMcp/DotnetMcp.csproj -c Release -o ./nupkg
```

## Verify Installation

```bash
dotnet-mcp --version
# Expected output: dotnet-mcp 1.0.0
```
