# MCP Completions Contract

**Feature**: 020-mcp-completions | **Date**: 2026-02-06

## Overview

This document defines the MCP `completion/complete` request/response contract for debugger expression autocompletion.

## Capability Advertisement

The server advertises completions support via capabilities:

```json
{
  "capabilities": {
    "completions": {}
  }
}
```

## Request: `completion/complete`

### Request Schema

```typescript
interface CompleteRequest {
  method: "completion/complete";
  params: CompleteRequestParams;
}

interface CompleteRequestParams {
  ref: ToolReference;
  argument: {
    name: string;    // Argument name (e.g., "expression")
    value: string;   // Partial value for completion
  };
}

interface ToolReference {
  type: "ref/tool";
  name: string;      // Tool name (e.g., "evaluate")
}
```

### Request Examples

**Variable name completion (empty prefix)**:
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "" }
  }
}
```

**Variable name completion (partial)**:
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "cust" }
  }
}
```

**Object member completion**:
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "user." }
  }
}
```

**Object member completion (partial)**:
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "user.Na" }
  }
}
```

**Static type member completion**:
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "DateTime." }
  }
}
```

## Response: `CompleteResult`

### Response Schema

```typescript
interface CompleteResult {
  completion: {
    values: string[];        // Completion suggestions (max 100)
    total?: number;          // Total available completions
    hasMore?: boolean;       // Whether more results exist
  };
}
```

### Response Examples

**Variable completions**:
```json
{
  "completion": {
    "values": ["customer", "customerId", "count", "i", "this"],
    "total": 5,
    "hasMore": false
  }
}
```

**Filtered variable completions** (prefix "cust"):
```json
{
  "completion": {
    "values": ["customer", "customerId"],
    "total": 2,
    "hasMore": false
  }
}
```

**Object member completions** (user.):
```json
{
  "completion": {
    "values": ["Name", "Email", "Id", "GetHashCode", "ToString", "Equals"],
    "total": 6,
    "hasMore": false
  }
}
```

**Static member completions** (DateTime.):
```json
{
  "completion": {
    "values": ["Now", "UtcNow", "Today", "MinValue", "MaxValue", "Parse", "TryParse"],
    "total": 7,
    "hasMore": false
  }
}
```

**Empty completions** (no session or running):
```json
{
  "completion": {
    "values": [],
    "total": 0,
    "hasMore": false
  }
}
```

## Supported Tool Arguments

| Tool | Argument | Completion Type |
|------|----------|-----------------|
| `evaluate` | `expression` | Variable, Member, StaticMember, Namespace |

## Behavior by Session State

| Session State | Completion Behavior |
|---------------|---------------------|
| No session | Empty completions |
| Attached, Running | Empty completions |
| Attached, Paused | Full completions |
| Disconnected | Empty completions |

## Completion Rules

### Variable Completion (no dot in expression)
- Returns: Local variables, parameters, `this` (if in instance method)
- Filtering: Case-insensitive prefix match
- Ordering: Alphabetical

### Member Completion (expression contains dot)
- Returns: Properties, fields, methods of object's runtime type
- Includes: Public and non-public members (debugger access)
- Filtering: Case-insensitive prefix match on member name
- Ordering: Alphabetical

### Static Member Completion (dot after type name)
- Returns: Static properties, fields, methods of the type
- Type resolution: Searches loaded modules
- Common types: Math, String, DateTime, Console work without full qualification
- Filtering: Case-insensitive prefix match
- Ordering: Alphabetical

### Namespace Completion (P3 - lower priority)
- Returns: Child namespaces and types
- Example: `System.` → Collections, IO, Text, String, Int32
- Filtering: Case-insensitive prefix match
- Ordering: Alphabetical

## Error Handling

Completions NEVER return errors. Invalid conditions result in empty completion lists:

| Condition | Response |
|-----------|----------|
| Unknown tool reference | `{ completion: { values: [] } }` |
| Unknown argument name | `{ completion: { values: [] } }` |
| Syntax error in expression | `{ completion: { values: [] } }` |
| Evaluation failure | `{ completion: { values: [] } }` |
| Type not found | `{ completion: { values: [] } }` |
| Timeout | `{ completion: { values: [] } }` |

## Performance Requirements

| Metric | Target |
|--------|--------|
| Response time (typical) | <100ms |
| Response time (max) | <500ms |
| Max results | 100 items |
