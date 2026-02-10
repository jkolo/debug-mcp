# Quickstart: Verify MCP Tool Annotations & Best Practices

## Build

```bash
dotnet build
```

Expected: 0 errors, 0 warnings.

## Run Annotation Tests

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ToolAnnotation"
```

Expected: All annotation verification tests pass (34 per-tool assertions + coverage check + description tests).

## Run Full Test Suite

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
```

Expected: 895+ tests pass (existing tests unaffected + new annotation tests).

## Manual Verification

Connect an MCP client and list tools. Each tool should show:
- `Title`: Human-readable name in title case
- `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld`: Boolean annotation values
- Enhanced descriptions (10 tools): Include field documentation and JSON response examples
