# Data Model: MCP Completions for Debugger Expressions

**Feature**: 020-mcp-completions | **Date**: 2026-02-06

## Entities

### CompletionKind

**Purpose**: Categorizes the type of completion being requested based on expression context.

```csharp
public enum CompletionKind
{
    /// <summary>Variable names in current scope (locals, parameters, this)</summary>
    Variable,

    /// <summary>Instance members of an object (properties, fields, methods)</summary>
    Member,

    /// <summary>Static members of a type (e.g., DateTime.Now, Math.PI)</summary>
    StaticMember,

    /// <summary>Types or namespaces (e.g., System.Collections.)</summary>
    Namespace
}
```

### CompletionContext

**Purpose**: Parsed context from the partial expression, used to determine what completions to provide.

```csharp
public sealed record CompletionContext(
    CompletionKind Kind,
    string Prefix,
    string? ObjectExpression = null,
    string? TypeName = null);
```

| Field | Type | Description |
|-------|------|-------------|
| `Kind` | `CompletionKind` | What type of completion is needed |
| `Prefix` | `string` | Partial text to filter completions (e.g., "cust" for "customer") |
| `ObjectExpression` | `string?` | For Member kind: the expression before the dot (e.g., "user" in "user.Na") |
| `TypeName` | `string?` | For StaticMember/Namespace kind: the type name (e.g., "DateTime") |

**Examples**:
- `""` → `CompletionContext(Variable, "")`
- `"cust"` → `CompletionContext(Variable, "cust")`
- `"user."` → `CompletionContext(Member, "", "user")`
- `"user.Na"` → `CompletionContext(Member, "Na", "user")`
- `"DateTime."` → `CompletionContext(StaticMember, "", "DateTime")`
- `"System."` → `CompletionContext(Namespace, "", TypeName: "System")`

### CompletionItem

**Purpose**: A single completion suggestion with metadata.

```csharp
public sealed record CompletionItem(
    string Value,
    CompletionItemKind Kind,
    string? Type = null);
```

| Field | Type | Description |
|-------|------|-------------|
| `Value` | `string` | The completion text (e.g., "customer", "Name", "PI") |
| `Kind` | `CompletionItemKind` | Category for the item |
| `Type` | `string?` | Optional type info (e.g., "string", "int") |

### CompletionItemKind

**Purpose**: Categorizes completion items for potential UI hints.

```csharp
public enum CompletionItemKind
{
    Variable,
    Parameter,
    Field,
    Property,
    Method,
    Type,
    Namespace
}
```

## State Transitions

Completion requests are stateless — each request is independent. However, completions depend on debug session state:

```text
Session State → Completion Availability
─────────────────────────────────────────
None          → Empty (no completions)
Running       → Empty (cannot enumerate)
Paused        → Full completions available
Disconnected  → Empty (no completions)
```

## Relationships

```text
┌─────────────────────────────────────────────────────────────────┐
│                    MCP completion/complete Request               │
├─────────────────────────────────────────────────────────────────┤
│  ref: { type: "ref/tool", name: "evaluate" }                    │
│  argument: { name: "expression", value: "user.Na" }             │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    CompletionContextParser                       │
│  Parse "user.Na" → CompletionContext(Member, "Na", "user")      │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                ExpressionCompletionProvider                      │
├─────────────────────────────────────────────────────────────────┤
│  1. Check session state (must be Paused)                        │
│  2. For Member kind:                                            │
│     - Evaluate "user" → get type                                │
│     - Get members of type                                       │
│     - Filter by "Na" prefix                                     │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    CompleteResult                                │
│  { completion: { values: ["Name"], total: 1, hasMore: false } } │
└─────────────────────────────────────────────────────────────────┘
```

## Integration Points

| Component | Role | Interface |
|-----------|------|-----------|
| `IDebugSessionManager` | Session state, variable enumeration, expression evaluation | Existing |
| `MembersGetTool` | Type member enumeration pattern | Reference |
| `ModulesSearchTool` | Type name resolution | Reference |
| `BreakpointRegistry` | N/A | Not used |
| MCP Server | Completion handler registration | `WithCompleteHandler()` |

## Validation Rules

| Rule | Validation |
|------|------------|
| Prefix length | No minimum (empty string allowed) |
| Max results | 100 items (MCP protocol limit) |
| Expression syntax | Graceful handling — return empty on parse error |
| Type resolution | Best effort — return empty if type not found |
