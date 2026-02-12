# Quickstart: Collection & Object Summarizer

## Build & Verify

```bash
# 1. Build
dotnet build

# 2. Run unit + contract tests (should discover new tools automatically)
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"

# 3. Verify new tools are registered (check tool count increased from 34 to 36)
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ToolAnnotationTests"
```

## Manual Smoke Test

```bash
# 1. Start the MCP server
dotnet run --project DebugMcp

# 2. In a separate terminal, use an MCP client to:

# a) Launch a test app
# → debug_launch(program: "tests/DebugTestApp/bin/Debug/net10.0/DebugTestApp.dll")

# b) Set breakpoint on a method that has collection variables
# → breakpoint_set(file: "Program.cs", line: <line_with_list>)

# c) Continue to breakpoint
# → debug_continue()

# d) Test collection_analyze
# → collection_analyze(expression: "myList")
# Expected: JSON with count, element type, first/last 5 elements

# e) Test object_summarize
# → object_summarize(expression: "customer")
# Expected: JSON with fields categorized into valued, null, interesting
```

## Acceptance Criteria Verification

| Criterion | How to Verify |
|-----------|---------------|
| SC-001: Collection in 1 call | Call `collection_analyze` — verify complete summary in single response |
| SC-002: Object anomalies in 1 call | Call `object_summarize` — verify null/interesting fields identified |
| SC-003: 10K elements < 2s | Create test with large list, time the `collection_analyze` call |
| SC-004: < 500 tokens for 1K | Measure JSON output size for 1,000-element collection summary |
| SC-005: Existing tools unchanged | Run full test suite — no regressions |
