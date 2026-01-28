# Quickstart: Reqnroll E2E Tests

## Prerequisites

- .NET 10 SDK
- Existing `TestTargetApp` builds successfully (`dotnet build tests/TestTargetApp`)

## Project Setup

```bash
# Create the E2E test project
dotnet new classlib -n DotnetMcp.E2E -o tests/DotnetMcp.E2E --framework net10.0
# Convert to test project by editing csproj (add xUnit + Reqnroll packages)

# Required NuGet packages:
dotnet add tests/DotnetMcp.E2E package Reqnroll.xUnit
dotnet add tests/DotnetMcp.E2E package Reqnroll.Tools.MsBuild.Generation
dotnet add tests/DotnetMcp.E2E package Microsoft.NET.Test.Sdk
dotnet add tests/DotnetMcp.E2E package xunit.runner.visualstudio
dotnet add tests/DotnetMcp.E2E package FluentAssertions

# Add project references:
dotnet add tests/DotnetMcp.E2E reference DotnetMcp/DotnetMcp.csproj
dotnet add tests/DotnetMcp.E2E reference tests/DotnetMcp.Tests/DotnetMcp.Tests.csproj
# (for TestTargetProcess helper)

# Add to solution:
dotnet sln add tests/DotnetMcp.E2E
```

## Project Layout

```
tests/DotnetMcp.E2E/
├── DotnetMcp.E2E.csproj
├── Features/
│   ├── SessionLifecycle.feature
│   ├── Breakpoints.feature
│   ├── Stepping.feature
│   ├── VariableInspection.feature
│   └── StackTrace.feature
├── StepDefinitions/
│   ├── SessionSteps.cs
│   ├── BreakpointSteps.cs
│   ├── SteppingSteps.cs
│   ├── InspectionSteps.cs
│   └── StackTraceSteps.cs
├── Hooks/
│   └── DebuggerHooks.cs
└── Support/
    └── DebuggerContext.cs
```

## Running Tests

```bash
# Run all E2E tests
dotnet test tests/DotnetMcp.E2E

# Run a specific feature
dotnet test tests/DotnetMcp.E2E --filter "Feature:SessionLifecycle"
```

## Writing a New Scenario

1. Add scenario to an existing `.feature` file:
```gherkin
Scenario: My new scenario
    Given the debugger is attached to the test target
    When I do something
    Then something should happen
```

2. If step definitions don't exist, Reqnroll will report unbound steps. Add bindings to the appropriate `StepDefinitions/*.cs` file.

3. Run with `dotnet test` to verify.
