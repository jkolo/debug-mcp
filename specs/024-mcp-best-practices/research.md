# Research: MCP Tool Annotations & Best Practices

**Date**: 2026-02-10 | **Feature**: 024-mcp-best-practices

## R1: SDK Annotation Support

**Decision**: Use `McpServerToolAttribute` properties (Title, ReadOnly, Destructive, Idempotent, OpenWorld) from ModelContextProtocol.Core 0.7.0-preview.1.

**Rationale**: The SDK already supports all required annotation properties as named parameters on the `[McpServerTool]` attribute. No SDK upgrade or custom implementation needed.

**Alternatives considered**:
- Custom annotation attributes → Rejected: SDK already provides this; custom attributes would be non-standard and invisible to MCP clients.
- MCP resource metadata → Rejected: Annotations belong on tools, not resources.

**Properties available on `McpServerToolAttribute`**:

| Property | Type | Default | Used |
|----------|------|---------|------|
| Name | string | null | Yes (existing) |
| Title | string | null | Yes (NEW) |
| ReadOnly | bool | false | Yes (NEW) |
| Destructive | bool | true | Yes (NEW) |
| Idempotent | bool | false | Yes (NEW) |
| OpenWorld | bool | true | Yes (NEW) |
| UseStructuredContent | bool | false | No (deferred) |
| IconSource | string | null | No |
| TaskSupport | - | - | No |

## R2: Annotation Testing Approach

**Decision**: Reflection-based unit tests in `Contract/ToolAnnotationTests.cs` using xUnit Theory + MemberData.

**Rationale**: Reflection on the tool assembly is simple, reliable, and requires no MCP server startup or mocking. It follows the same pattern as existing `SchemaValidationTests.cs` which validates contract compliance via static analysis.

**Alternatives considered**:
- Runtime MCP client test → Rejected: Requires server startup, adds complexity, tests more than just annotations.
- Manual audit → Rejected: Not automated, doesn't catch regressions.
- Build-time source generator → Rejected: Over-engineered for 34 tools; reflection is simpler.

**Tool discovery mechanism**:
```
Assembly → Types with [McpServerToolType] → Methods with [McpServerTool] → Read attribute properties
```

This matches how `Program.cs` discovers tools at startup (lines 141-143), ensuring test coverage matches runtime registration.

## R3: JSON Response Example Format

**Decision**: Embed concise single-line JSON examples in `[Description]` text using the pattern `Example response: {JSON}`.

**Rationale**: The `[Description]` attribute is a single string — the only metadata field AI clients read for tool context. JSON examples must be inline to be visible in the tool listing. Single-line format avoids multiline string complications in C# attributes.

**Alternatives considered**:
- Multiline JSON in verbatim string → Rejected: `[Description]` doesn't support verbatim strings cleanly; newlines render as literal `\n` in some clients.
- Separate documentation file → Rejected: AI clients only read the `[Description]` field; external docs are invisible at tool-selection time.
- Full response with all fields → Rejected: Too verbose; examples should show the most important fields with `...` for the rest.

## R4: Description Length Considerations

**Decision**: No explicit length limit. Keep JSON examples concise (abbreviated with `...`) to balance information density with readability.

**Rationale**: The MCP spec does not limit description length. AI clients (Claude, GPT) handle long descriptions well in context. The primary risk is human readability in source code, mitigated by keeping examples on a single line.
