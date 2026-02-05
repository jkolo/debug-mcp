# Quickstart: Roslyn Code Analysis

**Feature**: 015-roslyn-code-analysis

## Overview

This feature adds 5 new MCP tools for static code analysis using Roslyn:

| Tool | Purpose |
|------|---------|
| `code_load` | Load a solution or project for analysis |
| `code_find_usages` | Find all references to a symbol |
| `code_find_assignments` | Find all write operations to a symbol |
| `code_get_diagnostics` | Get compilation errors and warnings |
| `code_goto_definition` | Navigate to symbol definition |

## Prerequisites

- .NET 10.0 SDK installed
- Solution/project files accessible on local filesystem

## Quick Examples

### 1. Load a Solution

```json
{
  "tool": "code_load",
  "arguments": {
    "path": "/home/user/MyProject/MyProject.sln"
  }
}
```

### 2. Find All Usages of a Property

By fully qualified name:
```json
{
  "tool": "code_find_usages",
  "arguments": {
    "name": "MyApp.Models.Customer.Name"
  }
}
```

By file location:
```json
{
  "tool": "code_find_usages",
  "arguments": {
    "file": "/home/user/MyApp/Models/Customer.cs",
    "line": 10,
    "column": 20
  }
}
```

### 3. Find Where a Field Is Modified

```json
{
  "tool": "code_find_assignments",
  "arguments": {
    "name": "MyApp.Services.Counter._count"
  }
}
```

### 4. Get Compilation Errors

```json
{
  "tool": "code_get_diagnostics",
  "arguments": {
    "project": "MyApp",
    "severity": "Error"
  }
}
```

### 5. Go to Definition

```json
{
  "tool": "code_goto_definition",
  "arguments": {
    "file": "/home/user/MyApp/Controllers/CustomerController.cs",
    "line": 25,
    "column": 22
  }
}
```

## Symbol Identification

Symbols can be identified in two ways:

### Option A: Fully Qualified Name

Use the complete namespace path:
- Simple type: `MyNamespace.MyClass`
- Generic type: `System.Collections.Generic.List`1` (backtick + arity)
- Nested type: `OuterClass+NestedClass` (plus sign)
- Member: `MyNamespace.MyClass.MyMethod`

### Option B: File Location

Provide the exact position:
- `file`: Absolute path to source file
- `line`: 1-based line number
- `column`: 1-based column number

## Workflow Examples

### Debugging a Bug: "Where does this value come from?"

```
1. code_load → Load the solution
2. code_find_assignments → Find all places where the variable is set
3. code_goto_definition → Navigate to each assignment location
4. code_find_usages → Understand the data flow
```

### Code Review: "What uses this API?"

```
1. code_load → Load the solution
2. code_find_usages → Find all callers of the method
3. code_get_diagnostics → Check for any compilation issues
```

### Impact Analysis: "Is it safe to change this?"

```
1. code_load → Load the solution
2. code_find_usages → Find all usages across all projects
3. Review the usage contexts to assess impact
```

## Error Handling

All tools return structured responses:

**Success:**
```json
{
  "success": true,
  "data": { ... }
}
```

**Error:**
```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable message"
  }
}
```

Common error codes:
- `NO_WORKSPACE` - Call `code_load` first
- `SYMBOL_NOT_FOUND` - Symbol doesn't exist or couldn't be resolved
- `PROJECT_NOT_FOUND` - Project name not in loaded workspace
- `INVALID_PATH` - File path doesn't exist

## Independence from Debugger

These tools work independently of the debug session. You can:
- Analyze code without attaching to a process
- Use code analysis alongside active debugging
- Switch between analysis and debugging freely

The workspace persists until:
- A new `code_load` replaces it
- The MCP server session ends
