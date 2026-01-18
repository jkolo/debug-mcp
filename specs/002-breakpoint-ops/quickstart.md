# Quickstart: Breakpoint Operations

**Feature**: 002-breakpoint-ops
**Requires**: Active debug session (see 001-debug-session quickstart)

This guide shows how to use DotnetMcp's breakpoint tools to pause execution at
specific code locations during debugging.

---

## Prerequisites

1. DotnetMcp installed and configured with your AI assistant
2. An active debug session (via `debug_attach` or `debug_launch`)

---

## Basic Workflows

### Workflow 1: Set and Hit a Breakpoint

```
User: Set a breakpoint at line 42 in UserService.cs

Claude: I'll set a breakpoint at that location.
        [Calls breakpoint_set with file="/app/Services/UserService.cs", line=42]

        Breakpoint set successfully.
        ID: bp-550e8400-e29b-41d4-a716-446655440000
        Status: verified (bound to executable code)

        The application will pause when this line executes.
```

**Tool invocation**:
```json
{
  "tool": "breakpoint_set",
  "arguments": {
    "file": "/app/Services/UserService.cs",
    "line": 42
  }
}
```

### Workflow 2: Wait for Breakpoint Hit

```
User: Wait for the breakpoint to be hit

Claude: [Calls breakpoint_wait with timeout=30000]

        Breakpoint hit!
        Location: /app/Services/UserService.cs:42 in GetUser()
        Thread: 1
        Hit count: 1

        The application is now paused. What would you like to inspect?
```

**Tool invocation**:
```json
{
  "tool": "breakpoint_wait",
  "arguments": {
    "timeout": 30000
  }
}
```

### Workflow 3: List All Breakpoints

```
User: Show me all breakpoints

Claude: [Calls breakpoint_list]

        Active breakpoints (2):

        1. bp-550e8400-...000 [VERIFIED]
           /app/Services/UserService.cs:42 (GetUser)
           Hits: 1, Enabled: yes

        2. bp-550e8400-...001 [PENDING]
           /app/Services/LazyService.cs:15
           Hits: 0, Enabled: yes
           Note: Module not yet loaded
```

**Tool invocation**:
```json
{
  "tool": "breakpoint_list",
  "arguments": {}
}
```

### Workflow 4: Remove a Breakpoint

```
User: Remove the breakpoint in UserService

Claude: [Calls breakpoint_remove with id="bp-550e8400-e29b-41d4-a716-446655440000"]

        Breakpoint removed. Execution will no longer pause at that location.
```

**Tool invocation**:
```json
{
  "tool": "breakpoint_remove",
  "arguments": {
    "id": "bp-550e8400-e29b-41d4-a716-446655440000"
  }
}
```

---

## Advanced Scenarios

### Conditional Breakpoint

Only pause when a condition is true:

```json
{
  "tool": "breakpoint_set",
  "arguments": {
    "file": "/app/Services/UserService.cs",
    "line": 50,
    "condition": "userId > 100"
  }
}
```

The breakpoint will only trigger when `userId > 100` evaluates to true.

### Column-Level Breakpoint (Lambda Targeting)

When a line has multiple sequence points (e.g., lambda expressions):

```json
{
  "tool": "breakpoint_set",
  "arguments": {
    "file": "/app/Services/UserService.cs",
    "line": 42,
    "column": 30
  }
}
```

This sets a breakpoint at column 30, targeting a specific lambda on that line.

**Example**: For `items.Where(x => x.Active).Select(x => x.Name)`:
- `column: 12` → breaks in the Where lambda
- `column: 35` → breaks in the Select lambda

If the column doesn't match a sequence point, the response includes available options:
```json
{
  "error": {
    "code": "INVALID_COLUMN",
    "availableSequencePoints": [
      { "startColumn": 12, "endColumn": 28, "description": "Where predicate" },
      { "startColumn": 35, "endColumn": 51, "description": "Select projection" }
    ]
  }
}
```

### Exception Breakpoint

Break when a specific exception type is thrown:

```json
{
  "tool": "breakpoint_set_exception",
  "arguments": {
    "exceptionType": "System.NullReferenceException",
    "breakOnFirstChance": true,
    "breakOnSecondChance": true,
    "includeSubtypes": true
  }
}
```

When the exception is thrown, the wait response includes exception details:
```json
{
  "hit": true,
  "breakpointId": "ex-abc123",
  "exceptionInfo": {
    "type": "System.NullReferenceException",
    "message": "Object reference not set to an instance of an object",
    "isFirstChance": true
  }
}
```

### Breakpoint Before Module Loads

When setting a breakpoint in code that hasn't loaded yet:

```json
{
  "tool": "breakpoint_set",
  "arguments": {
    "file": "/app/Plugins/LazyPlugin.cs",
    "line": 20
  }
}
```

**Response**:
```json
{
  "success": true,
  "breakpoint": {
    "id": "bp-abc123",
    "state": "pending",
    "verified": false,
    "message": "Module not yet loaded; breakpoint will bind when module loads"
  }
}
```

The breakpoint becomes verified automatically when the module loads.

### Disable Without Removing

Temporarily disable a breakpoint:

```json
{
  "tool": "breakpoint_enable",
  "arguments": {
    "id": "bp-550e8400-e29b-41d4-a716-446655440000",
    "enabled": false
  }
}
```

Re-enable later with `enabled: true`.

### Extended Wait Timeout

For long-running operations, extend the timeout:

```json
{
  "tool": "breakpoint_wait",
  "arguments": {
    "timeout": 120000
  }
}
```

---

## Error Handling

### No Active Session

```
Error: No active debug session
Action: Use debug_attach or debug_launch first
```

### Invalid Line Number

```
Error: Line 99 does not contain executable code in UserService.cs
Action: Check that the line contains code (not blank, comment, or declaration only)
Suggestion: Nearest valid lines are 97 and 102
```

### Breakpoint Not Found

```
Error: No breakpoint with ID bp-invalid-id
Action: Use breakpoint_list to see available breakpoints
```

### Condition Syntax Error

```
Error: Condition expression has syntax error at position 5
Details: "userId >> 100" - expected comparison operator
Action: Fix the condition expression syntax
```

### Wait Timeout

```
Result: No breakpoint hit within 30000ms
Action: Either:
  - Increase timeout and wait again
  - Verify the breakpoint is on code that will execute
  - Check if a condition is preventing the hit
```

---

## Tool Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| `breakpoint_set` | Create source/function breakpoint | `file`, `line` (required); `column`, `condition` (optional) |
| `breakpoint_set_exception` | Create exception breakpoint | `exceptionType` (required); first/second chance options |
| `breakpoint_remove` | Delete a breakpoint | `id` (required) |
| `breakpoint_list` | List all breakpoints | None |
| `breakpoint_wait` | Wait for hit | `timeout` (optional, default 30s) |
| `breakpoint_enable` | Enable/disable | `id`, `enabled` (required) |

---

## Tips

1. **Always have a session first**: Breakpoint tools require an active debug session.
   Use `debug_state` to verify before setting breakpoints.

2. **Understand pending vs verified**: Pending breakpoints are valid but waiting for
   their module to load. They'll become verified automatically.

3. **Use conditions wisely**: Conditional breakpoints are powerful in loops but add
   overhead. For simple cases, consider letting the breakpoint hit and inspecting
   manually.

4. **Wait timeout strategy**: Start with the default 30s timeout. Only increase if
   you know the operation takes longer (e.g., waiting for user input).

5. **Clean up breakpoints**: Remove breakpoints when done investigating to avoid
   unintended pauses in subsequent execution.

6. **Check hit counts**: Use `breakpoint_list` to see how many times each breakpoint
   has been hit - useful for understanding code flow.

---

## Complete Debugging Workflow Example

```
1. Claude: [debug_launch program="/app/MyApp.dll" stopAtEntry=true]
   → Process launched, paused at entry

2. Claude: [breakpoint_set file="/app/Services/UserService.cs" line=42]
   → Breakpoint bp-001 created (verified)

3. Claude: [breakpoint_set file="/app/Services/UserService.cs" line=50 condition="userId > 100"]
   → Breakpoint bp-002 created with condition (verified)

4. Claude: [debug_continue]  // (from 001-debug-session, future feature)
   → Process running

5. Claude: [breakpoint_wait timeout=60000]
   → Hit! bp-001 at UserService.cs:42, thread 1

6. // Inspect variables, step through code...

7. Claude: [breakpoint_remove id="bp-001"]
   → Breakpoint removed

8. Claude: [breakpoint_list]
   → 1 breakpoint remaining: bp-002 (conditional)
```
