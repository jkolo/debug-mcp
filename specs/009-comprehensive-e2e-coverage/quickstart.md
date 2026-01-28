# Quickstart: Comprehensive E2E Test Coverage

## Prerequisites

- .NET 10.0 SDK
- Linux x64 (ICorDebug native interop)

## Build & Run Tests

```bash
# Build everything (including TestTargetApp)
dotnet build DotnetMcp.slnx
dotnet build tests/TestTargetApp/TestTargetApp.csproj

# Run E2E tests
dotnet test tests/DotnetMcp.E2E/

# Run specific feature
dotnet test tests/DotnetMcp.E2E/ --filter "FeatureTitle=Expression Evaluation"
```

## Adding a New E2E Scenario

1. **Add test target code** to the appropriate Libs/ project (e.g., `Recursion/` for recursive methods)
2. **Add command** to `TestTargetApp/Program.cs` command loop if needed
3. **Write Gherkin scenario** in the appropriate `.feature` file
4. **Add/reuse step definitions** in `StepDefinitions/`
5. **Add state properties** to `DebuggerContext.cs` if new result types are captured
6. **Build TestTargetApp** before running E2E tests: `dotnet build tests/TestTargetApp/TestTargetApp.csproj`
7. **Run tests**: `dotnet test tests/DotnetMcp.E2E/`

## Library Project Mapping

| Library | Domain | Example Code |
|---------|--------|-------------|
| BaseTypes | Enums, structs, primitives | TestEnum, TestStruct, NullableHolder |
| Collections | List, Dict, arrays | CollectionHolder |
| Exceptions | Try/catch/throw | ExceptionThrower, custom exceptions |
| Recursion | Recursive methods | RecursiveCalculator |
| Expressions | Expression eval targets | Properties, methods, null refs |
| Threading | Thread scenarios | ThreadSpawner with barrier |
| AsyncOps | Async operations | Reserved for async stack traces |
| MemoryStructs | Structs, layout | LayoutStruct with known offsets |
| ComplexObjects | Deeply nested objects | DeepObject (3+ levels) |
| Scenarios | Top-level entry | Cross-library orchestration |
