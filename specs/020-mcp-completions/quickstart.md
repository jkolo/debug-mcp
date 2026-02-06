# Quickstart: MCP Completions for Debugger Expressions

**Feature**: 020-mcp-completions | **Date**: 2026-02-06

## Overview

MCP Completions provides autocompletion for debugger expressions, helping LLM clients write valid `evaluate` tool calls without guessing variable or member names.

## Prerequisites

- DebugMcp server running
- Active debug session (attached to a .NET process)
- Process paused at a breakpoint or step

## Usage Examples

### 1. Variable Name Completion

When writing an evaluation expression and unsure of variable names:

**Request**:
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "" }
  }
}
```

**Response**:
```json
{
  "completion": {
    "values": ["customer", "orderId", "total", "this"],
    "total": 4,
    "hasMore": false
  }
}
```

### 2. Filtered Variable Completion

Narrow down with a prefix:

**Request** (partial "ord"):
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "ord" }
  }
}
```

**Response**:
```json
{
  "completion": {
    "values": ["orderId"],
    "total": 1,
    "hasMore": false
  }
}
```

### 3. Object Member Completion

After typing an object name and dot:

**Request** (customer.):
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "customer." }
  }
}
```

**Response**:
```json
{
  "completion": {
    "values": ["Name", "Email", "Id", "Orders", "GetHashCode", "ToString"],
    "total": 6,
    "hasMore": false
  }
}
```

### 4. Static Type Member Completion

For static members like `DateTime.Now`:

**Request** (DateTime.):
```json
{
  "method": "completion/complete",
  "params": {
    "ref": { "type": "ref/tool", "name": "evaluate" },
    "argument": { "name": "expression", "value": "DateTime." }
  }
}
```

**Response**:
```json
{
  "completion": {
    "values": ["Now", "UtcNow", "Today", "MinValue", "MaxValue"],
    "total": 5,
    "hasMore": false
  }
}
```

## Workflow Integration

### LLM Client Pattern

```text
1. User asks: "What's the customer's email?"
2. LLM needs to evaluate expression but doesn't know exact variable names
3. LLM requests completions with empty prefix
4. Server returns: ["customer", "order", "this"]
5. LLM picks "customer" and requests member completions
6. Server returns: ["Name", "Email", "Id", ...]
7. LLM calls evaluate with "customer.Email"
8. Server returns the actual email value
```

### Error Prevention

Without completions:
```text
LLM guesses: evaluate("cust.email")
Result: Error - "cust" not found
LLM retries: evaluate("Customer.Email")
Result: Error - "Customer" not found
...multiple failures
```

With completions:
```text
LLM requests completions("")
Server returns: ["customer", ...]
LLM requests completions("customer.")
Server returns: ["Email", ...]
LLM evaluates: evaluate("customer.Email")
Result: Success - "john@example.com"
```

## Capability Check

Clients can verify completions support via `initialize` response:

```json
{
  "capabilities": {
    "completions": {}
  }
}
```

## Limitations

- Completions only work when process is **paused**
- Running processes return empty completions (cannot enumerate)
- No session returns empty completions
- Max 100 results per request (MCP protocol limit)
- Type completion (P3) may require fully qualified names for less common types

## Debugging Tips

If completions are empty:
1. Check session state (`debug_state` tool) - must be "paused"
2. Verify you're at a valid stack frame
3. Check logs for any evaluation errors
