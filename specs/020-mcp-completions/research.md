# Research: MCP Completions for Debugger Expressions

**Feature**: 020-mcp-completions | **Date**: 2026-02-06

## R1: MCP SDK Completion Handler Registration

**Decision**: Use `WithCompleteHandler()` builder method to register a custom completion handler that intercepts `completion/complete` requests.

**Rationale**: The MCP SDK 0.7.0-preview.1 provides `WithCompleteHandler()` similar to the resource subscription handlers used in Feature 019. This allows custom completion logic while staying within SDK patterns.

**Implementation approach**:
```csharp
builder.Services
    .AddMcpServer(options =>
    {
        options.Capabilities ??= new();
        options.Capabilities.Completions = new();  // Advertise capability
    })
    .WithCompleteHandler((request, ct) => HandleCompletionAsync(request, ct));
```

**Alternatives considered**:
- Attribute-based `[McpServerCompletion]` — Not available in SDK 0.7.0
- Prompts with completable arguments — Over-complicated for expression completion
- Resource templates with completable URI segments — Would require restructuring evaluate tool

## R2: Completion Request Processing

**Decision**: Parse the `argument.value` to determine completion context (variable name vs. member access vs. static type).

**Rationale**: The completion context depends on the partial expression structure:
- Empty or no-dot prefix → Variable/scope completion
- Contains dot → Member or namespace completion
- Type name followed by dot → Static member completion

**Implementation approach**:
```csharp
public class CompletionContext
{
    public CompletionKind Kind { get; init; }      // Variable, Member, StaticMember, Namespace
    public string Prefix { get; init; } = "";      // Partial text to match
    public string? ObjectExpression { get; init; } // For member access: "user" in "user.Na"
    public string? TypeName { get; init; }         // For static: "DateTime" in "DateTime."
}
```

**Context parsing rules**:
1. No dot → `Kind = Variable`, `Prefix = value`
2. Dot present, left side is variable → `Kind = Member`, `ObjectExpression = left`, `Prefix = right`
3. Dot present, left side is type name → `Kind = StaticMember`, `TypeName = left`, `Prefix = right`
4. Multiple dots with namespace prefix → `Kind = Namespace`, detect hierarchy

## R3: Variable Enumeration Strategy

**Decision**: Use existing `IDebugSessionManager.GetVariables()` for scope-aware variable names.

**Rationale**: The `variables_get` tool already enumerates variables via ICorDebug. Reusing this infrastructure ensures consistency and avoids duplication.

**Data flow**:
1. `ExpressionCompletionProvider` calls `_sessionManager.GetVariables(threadId, frameIndex, "all")`
2. Extract variable names from result
3. Filter by prefix
4. Return as completion values

**Edge cases**:
- No session → Empty completions
- Running state → Empty completions (variables unavailable)
- Empty prefix → Return all variables (let client filter/limit display)

## R4: Object Member Enumeration Strategy

**Decision**: Evaluate the object expression to get its type, then enumerate type members via reflection metadata.

**Rationale**: To complete `user.Na`, we need:
1. Evaluate `user` to determine its runtime type
2. Get members of that type (properties, fields, methods)
3. Filter by prefix `Na`

**Implementation approach**:
1. Call `_sessionManager.EvaluateAsync(objectExpression)` with short timeout
2. Get `result.Type` (e.g., "User")
3. Use module metadata to enumerate members:
   - Properties: `TypeInfo.Properties`
   - Fields: `TypeInfo.Fields`
   - Methods: `TypeInfo.Methods`
4. Include both public and non-public (debugger has full access)

**Performance note**: Evaluation + member enumeration should stay under 100ms. Use caching if needed.

## R5: Static Type Member Completion

**Decision**: Search loaded modules for type name, then enumerate static members.

**Rationale**: For `DateTime.`, we search for `System.DateTime` in loaded modules and return static members.

**Implementation approach**:
1. Parse type name (may be simple like `DateTime` or qualified like `System.DateTime`)
2. Search loaded modules via `modules_search` pattern
3. Get type info and filter to static members only
4. Common types like `Math`, `String`, `DateTime` should work without full qualification

**Fallback**: If type not found, return empty (no error, just no suggestions).

## R6: Argument Reference Handling

**Decision**: Accept completion requests for any tool argument named `expression`, referencing the `evaluate` tool.

**Rationale**: MCP completion requests include a `ref` indicating what's being completed. We support:
- `{ ref: { type: "ref/tool", name: "evaluate" }, argument: { name: "expression", value: "..." } }`

**Validation**:
- If ref is not for `evaluate` tool → Return empty (graceful, not error)
- If argument name is not `expression` → Return empty

## R7: Response Format

**Decision**: Return `CompleteResult` with up to 100 completion values, ordered alphabetically.

**Rationale**: MCP spec limits completion values to 100. Alphabetical ordering provides predictable results.

**Response structure**:
```csharp
new CompleteResult
{
    Completion = new()
    {
        Values = filteredCompletions.Take(100).ToArray(),
        Total = allCompletions.Count,
        HasMore = allCompletions.Count > 100
    }
}
```

## R8: Error Handling Strategy

**Decision**: Always return empty completions instead of errors. Log issues for debugging.

**Rationale**: Completion is advisory — returning an error would be disruptive to the LLM workflow. Better to return nothing and let the user type manually.

**Graceful handling**:
| Condition | Response |
|-----------|----------|
| No session | Empty completions |
| Running (not paused) | Empty completions |
| Invalid expression | Empty completions |
| Evaluation error | Empty completions |
| Type not found | Empty completions |

## R9: Completions Capability Advertisement

**Decision**: Add `Completions = new()` to server capabilities in `Program.cs`.

**Rationale**: MCP clients check capabilities before sending completion requests. The empty object signals support.

```csharp
options.Capabilities.Completions = new();
```

## R10: Logging Requirements

**Decision**: Log completion requests at Debug level, with Info for slow completions (>100ms).

**Rationale**: Constitution V (Observability) requires traceability. Completions are high-frequency, so Debug level avoids noise.

**Log points**:
- Completion request received (Debug): context, argument value
- Completion response sent (Debug): value count, duration
- Slow completion (Info): when >100ms, include context for investigation
