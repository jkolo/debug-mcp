---
title: Analyze a Codebase
sidebar_position: 4
---

# Workflow: Analyze a Codebase

This guide walks through using debug-mcp's code analysis tools to understand and navigate a C# codebase without running the debugger. These tools use Roslyn for static analysis.

## Scenario

You're working with an unfamiliar .NET solution and need to understand how different parts of the code interact. You want an AI agent to help you find where classes are used, track variable assignments, and check for compilation issues.

## Steps

### 1. Load the solution

```json
// code_load
{
  "path": "/app/MyApp.sln"
}
```

Response:
```json
{
  "success": true,
  "data": {
    "path": "/app/MyApp.sln",
    "type": "Solution",
    "projects": [
      { "name": "MyApp", "documents_count": 45 },
      { "name": "MyApp.Core", "documents_count": 23 },
      { "name": "MyApp.Tests", "documents_count": 15 }
    ],
    "loaded_at": "2026-02-05T10:30:00Z"
  }
}
```

The workspace is now ready for analysis. Loading only needs to happen once per session.

### 2. Find all usages of a class

You want to understand how `UserService` is used throughout the codebase.

```json
// code_find_usages
{
  "name": "MyApp.Services.UserService",
  "symbolKind": "Type"
}
```

Response:
```json
{
  "success": true,
  "data": {
    "symbol": {
      "name": "UserService",
      "fully_qualified_name": "MyApp.Services.UserService",
      "kind": "Type"
    },
    "usages_count": 8,
    "usages": [
      {
        "file": "/app/Services/UserService.cs",
        "line": 12,
        "kind": "Declaration",
        "context": "UserService"
      },
      {
        "file": "/app/Controllers/UserController.cs",
        "line": 15,
        "kind": "Reference",
        "context": "UserController"
      },
      {
        "file": "/app/Startup.cs",
        "line": 42,
        "kind": "Reference",
        "context": "ConfigureServices"
      }
    ]
  }
}
```

You can see the class is declared in `UserService.cs`, injected in `Startup.cs`, and used in `UserController.cs`.

### 3. Navigate to a method definition

You see a call to `GetUserById` and want to see its implementation.

```json
// code_goto_definition
{
  "file": "/app/Controllers/UserController.cs",
  "line": 28,
  "column": 20
}
```

Response:
```json
{
  "success": true,
  "data": {
    "symbol": {
      "name": "GetUserById",
      "fully_qualified_name": "MyApp.Services.UserService.GetUserById(int)",
      "kind": "Method"
    },
    "definitions": [
      {
        "file": "/app/Services/UserService.cs",
        "line": 35,
        "column": 12,
        "is_source": true
      }
    ]
  }
}
```

Now you know exactly where to look: line 35 of `UserService.cs`.

### 4. Track variable assignments

You're debugging an issue where `_connectionString` has the wrong value. Find all places where it's assigned.

```json
// code_find_assignments
{
  "name": "MyApp.Data.DbContext._connectionString",
  "symbolKind": "Field"
}
```

Response:
```json
{
  "success": true,
  "data": {
    "symbol": {
      "name": "_connectionString",
      "kind": "Field"
    },
    "assignments_count": 2,
    "assignments": [
      {
        "file": "/app/Data/DbContext.cs",
        "line": 8,
        "kind": "Declaration",
        "context": "DbContext",
        "value_expression": "string.Empty"
      },
      {
        "file": "/app/Data/DbContext.cs",
        "line": 15,
        "kind": "Simple",
        "context": ".ctor",
        "operator": "=",
        "value_expression": "configuration.GetConnectionString(\"Default\")"
      }
    ]
  }
}
```

The field is initialized to empty and then set in the constructor from configuration.

### 5. Check for compilation errors

Before making changes, verify the code compiles.

```json
// code_get_diagnostics
{
  "minSeverity": "Warning"
}
```

Response:
```json
{
  "success": true,
  "data": {
    "total_count": 2,
    "summary": {
      "warning": 2
    },
    "diagnostics": [
      {
        "id": "CS0168",
        "message": "The variable 'ex' is declared but never used",
        "severity": "Warning",
        "file": "/app/Services/UserService.cs",
        "line": 42,
        "project": "MyApp"
      },
      {
        "id": "CA1822",
        "message": "Member 'ProcessData' does not access instance data",
        "severity": "Warning",
        "file": "/app/Services/DataService.cs",
        "line": 28,
        "project": "MyApp"
      }
    ]
  }
}
```

Two warnings, no errors. The code compiles successfully.

## Common Use Cases

### Impact Analysis

Before renaming a method, find all its usages:
```json
{ "tool": "code_find_usages", "name": "MyApp.Services.OrderService.ProcessOrder" }
```

### Understanding Data Flow

Track where a property value comes from:
```json
{ "tool": "code_find_assignments", "name": "MyApp.Models.Order.Status" }
```

### Pre-Commit Verification

Check for errors before committing:
```json
{ "tool": "code_get_diagnostics", "minSeverity": "Error" }
```

### Code Navigation

Jump to the definition of any symbol:
```json
{ "tool": "code_goto_definition", "file": "Program.cs", "line": 15, "column": 10 }
```

## Tips

- **Load once, query many**: The workspace stays loaded until you load a different solution or the session ends.
- **Use fully qualified names**: For `code_find_usages` and `code_find_assignments`, use the full namespace path (e.g., `MyApp.Services.UserService`).
- **Combine with debugging**: Use code analysis to understand the code, then set targeted breakpoints for runtime debugging.
- **Check diagnostics first**: Before debugging runtime issues, verify there are no compilation errors.
