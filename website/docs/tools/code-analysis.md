---
title: Code Analysis
sidebar_position: 7
---

# Code Analysis

Code analysis tools let you navigate and understand C# codebases using Roslyn's semantic analysis — without needing to run the debugger. Find symbol usages, track variable assignments, and get compilation diagnostics statically.

## When to Use

Use code analysis tools when you need to understand code structure and relationships. These tools work on the source code directly, so they're available even when the process isn't running.

**Typical flow:** `code_load` → `code_find_usages` or `code_find_assignments` → `code_goto_definition`

**Use cases:**
- Understanding how a class or method is used throughout a codebase
- Finding all places where a variable is assigned
- Navigating to symbol definitions (go-to-definition)
- Checking for compilation errors before running

## Tools

### code_load

Load a solution or project file into the analysis workspace.

**When to use:** Before using any other code analysis tools. This tool initializes Roslyn's workspace with your codebase.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `path` | string | Yes | Absolute path to .sln or .csproj file |

**Response:**
```json
{
  "success": true,
  "data": {
    "path": "/app/MyApp.sln",
    "type": "Solution",
    "projects": [
      {
        "name": "MyApp",
        "path": "/app/MyApp/MyApp.csproj",
        "documents_count": 45,
        "target_framework": "net8.0"
      },
      {
        "name": "MyApp.Tests",
        "path": "/app/MyApp.Tests/MyApp.Tests.csproj",
        "documents_count": 23
      }
    ],
    "diagnostics": [],
    "loaded_at": "2026-02-05T10:30:00Z"
  }
}
```

**Errors:**
- `INVALID_PATH` — Path doesn't exist or isn't a .sln/.csproj file
- `LOAD_FAILED` — MSBuild workspace failed to load

---

### code_find_usages

Find all usages of a symbol across the workspace.

**When to use:** Understand how a type, method, property, or variable is used throughout the codebase. Useful for refactoring decisions or understanding code impact.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `name` | string | No* | Fully qualified symbol name (e.g., "Namespace.Class.Method") |
| `symbolKind` | string | No | Filter by kind: Namespace, Type, Method, Property, Field, Event, Local, Parameter, TypeParameter |
| `file` | string | No* | Absolute path to source file (use with line/column) |
| `line` | integer | No* | 1-based line number (use with file/column) |
| `column` | integer | No* | 1-based column number (use with file/line) |

*Either `name` OR `file`+`line`+`column` must be provided.

**Example by name:**
```json
{
  "name": "MyApp.Services.UserService.GetUser",
  "symbolKind": "Method"
}
```

**Example by location:**
```json
{
  "file": "/app/Services/UserService.cs",
  "line": 42,
  "column": 20
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "symbol": {
      "name": "GetUser",
      "fully_qualified_name": "MyApp.Services.UserService.GetUser(string)",
      "kind": "Method",
      "containing_type": "MyApp.Services.UserService",
      "containing_namespace": "MyApp.Services",
      "declaration_file": "/app/Services/UserService.cs",
      "declaration_line": 15,
      "declaration_column": 12
    },
    "usages_count": 5,
    "usages": [
      {
        "file": "/app/Services/UserService.cs",
        "line": 15,
        "column": 12,
        "end_line": 15,
        "end_column": 19,
        "kind": "Declaration",
        "context": "UserService"
      },
      {
        "file": "/app/Controllers/UserController.cs",
        "line": 28,
        "column": 15,
        "end_line": 28,
        "end_column": 22,
        "kind": "Reference",
        "context": "Get"
      },
      {
        "file": "/app/Controllers/AdminController.cs",
        "line": 45,
        "column": 20,
        "end_line": 45,
        "end_column": 27,
        "kind": "Reference",
        "context": "GetUserDetails"
      }
    ]
  }
}
```

**Usage kinds:**
- `Declaration` — Where the symbol is defined
- `Reference` — Where the symbol is referenced (type/method)
- `Read` — Where a variable's value is read
- `Write` — Where a variable's value is written

**Errors:**
- `NO_WORKSPACE` — Call `code_load` first
- `SYMBOL_NOT_FOUND` — Symbol doesn't exist at name/location
- `INVALID_PARAMETER` — Missing or conflicting parameters

**Real-world use case:** An AI agent needs to rename a method. It uses `code_find_usages` to find all 12 places where the method is called, then generates edits for each location.

---

### code_find_assignments

Find all assignments to a variable, field, or property.

**When to use:** Track where a value comes from. Useful for debugging "why does this variable have this value?" questions.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `name` | string | No* | Fully qualified symbol name |
| `symbolKind` | string | No | Filter: Field, Property, Local, Parameter |
| `file` | string | No* | Absolute path to source file |
| `line` | integer | No* | 1-based line number |
| `column` | integer | No* | 1-based column number |

*Either `name` OR `file`+`line`+`column` must be provided.

**Example:**
```json
{
  "name": "MyApp.Services.UserService._cache",
  "symbolKind": "Field"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "symbol": {
      "name": "_cache",
      "fully_qualified_name": "MyApp.Services.UserService._cache",
      "kind": "Field",
      "containing_type": "MyApp.Services.UserService",
      "declaration_file": "/app/Services/UserService.cs",
      "declaration_line": 8
    },
    "assignments_count": 3,
    "assignments": [
      {
        "file": "/app/Services/UserService.cs",
        "line": 8,
        "column": 5,
        "end_line": 8,
        "end_column": 45,
        "kind": "Declaration",
        "context": "UserService",
        "operator": "=",
        "value_expression": "new Dictionary<string, User>()"
      },
      {
        "file": "/app/Services/UserService.cs",
        "line": 35,
        "column": 9,
        "end_line": 35,
        "end_column": 25,
        "kind": "Simple",
        "context": "ClearCache",
        "operator": "=",
        "value_expression": "new Dictionary<string, User>()"
      },
      {
        "file": "/app/Services/UserService.cs",
        "line": 42,
        "column": 9,
        "end_line": 42,
        "end_column": 15,
        "kind": "Simple",
        "context": "Dispose",
        "operator": "=",
        "value_expression": "null"
      }
    ]
  }
}
```

**Assignment kinds:**
- `Declaration` — Declaration with initializer (`int x = 5`)
- `Simple` — Simple assignment (`x = 5`)
- `Compound` — Compound assignment (`x += 5`, `x -= 3`)
- `Increment` — Increment (`x++`, `++x`)
- `Decrement` — Decrement (`x--`, `--x`)
- `OutParameter` — Out parameter (`Method(out x)`)
- `RefParameter` — Ref parameter (`Method(ref x)`)
- `Initializer` — Object/collection initializer

**Errors:**
- `NO_WORKSPACE` — Call `code_load` first
- `SYMBOL_NOT_FOUND` — Symbol doesn't exist
- `INVALID_PARAMETER` — Invalid symbol kind for assignment tracking

**Real-world use case:** An AI agent is debugging why `_connectionString` is null. It uses `code_find_assignments` to find all places where the field is set, discovering it's only assigned in the constructor — which isn't being called due to DI misconfiguration.

---

### code_get_diagnostics

Get compilation diagnostics (errors and warnings) for projects.

**When to use:** Check for compilation errors before running. Useful for validating code changes or understanding why a build fails.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `projectName` | string | No | Filter to specific project |
| `minSeverity` | string | No | Minimum severity: Hidden, Info, Warning (default), Error |
| `maxResults` | integer | No | Max diagnostics to return (default: 100, max: 500) |

**Example:**
```json
{
  "projectName": "MyApp",
  "minSeverity": "Warning",
  "maxResults": 50
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "total_count": 3,
    "limited_to": 50,
    "summary": {
      "error": 1,
      "warning": 2
    },
    "diagnostics": [
      {
        "id": "CS0246",
        "message": "The type or namespace name 'UserDto' could not be found",
        "severity": "Error",
        "category": "Compiler",
        "file": "/app/Controllers/UserController.cs",
        "line": 15,
        "column": 20,
        "end_line": 15,
        "end_column": 27,
        "project": "MyApp",
        "help_link": "https://docs.microsoft.com/en-us/dotnet/csharp/misc/cs0246"
      },
      {
        "id": "CS0168",
        "message": "The variable 'ex' is declared but never used",
        "severity": "Warning",
        "category": "Compiler",
        "file": "/app/Services/UserService.cs",
        "line": 42,
        "column": 12,
        "project": "MyApp"
      }
    ]
  }
}
```

**Errors:**
- `NO_WORKSPACE` — Call `code_load` first
- `PROJECT_NOT_FOUND` — Specified project doesn't exist

**Real-world use case:** After making code edits, an AI agent runs `code_get_diagnostics` to verify there are no compilation errors before suggesting the changes are complete.

---

### code_goto_definition

Navigate to the definition of a symbol.

**When to use:** Find where a type, method, or property is defined. Essential for understanding code and navigating large codebases.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `file` | string | Yes | Absolute path to source file |
| `line` | integer | Yes | 1-based line number where symbol appears |
| `column` | integer | Yes | 1-based column number where symbol appears |

**Example:**
```json
{
  "file": "/app/Controllers/UserController.cs",
  "line": 28,
  "column": 15
}
```

**Response (source symbol):**
```json
{
  "success": true,
  "data": {
    "symbol": {
      "name": "GetUser",
      "fully_qualified_name": "MyApp.Services.UserService.GetUser(string)",
      "kind": "Method",
      "containing_type": "MyApp.Services.UserService",
      "containing_namespace": "MyApp.Services"
    },
    "definitions_count": 1,
    "definitions": [
      {
        "file": "/app/Services/UserService.cs",
        "line": 15,
        "column": 12,
        "end_line": 15,
        "end_column": 19,
        "is_source": true
      }
    ]
  }
}
```

**Response (metadata symbol):**
```json
{
  "success": true,
  "data": {
    "symbol": {
      "name": "WriteLine",
      "fully_qualified_name": "System.Console.WriteLine(string)",
      "kind": "Method",
      "containing_type": "System.Console",
      "containing_namespace": "System"
    },
    "definitions_count": 1,
    "definitions": [
      {
        "is_source": false,
        "assembly_name": "System.Console",
        "assembly_version": "8.0.0.0"
      }
    ]
  }
}
```

**Note:** For metadata symbols (types from NuGet packages or the BCL), the definition returns assembly information instead of a source location.

**Errors:**
- `NO_WORKSPACE` — Call `code_load` first
- `SYMBOL_NOT_FOUND` — No symbol at the specified location

**Real-world use case:** An AI agent sees a method call `_userService.GetUser(id)` and needs to understand what it does. It uses `code_goto_definition` to navigate to the method implementation.

---

## Workflow Examples

### Understanding a Bug Report

1. **Load the codebase:**
   ```json
   { "tool": "code_load", "path": "/app/MyApp.sln" }
   ```

2. **Find where the problematic variable is assigned:**
   ```json
   { "tool": "code_find_assignments", "name": "MyApp.Services.OrderService._taxRate" }
   ```

3. **Navigate to the assignment location:**
   ```json
   { "tool": "code_goto_definition", "file": "/app/Services/OrderService.cs", "line": 45, "column": 10 }
   ```

### Pre-flight Check Before Running

1. **Load the project:**
   ```json
   { "tool": "code_load", "path": "/app/MyApp.csproj" }
   ```

2. **Check for compilation errors:**
   ```json
   { "tool": "code_get_diagnostics", "minSeverity": "Error" }
   ```

3. **If errors exist, investigate the first one:**
   ```json
   { "tool": "code_goto_definition", "file": "/app/Controllers/UserController.cs", "line": 15, "column": 20 }
   ```

### Impact Analysis for Refactoring

1. **Load the solution:**
   ```json
   { "tool": "code_load", "path": "/app/MyApp.sln" }
   ```

2. **Find all usages of the method to rename:**
   ```json
   { "tool": "code_find_usages", "name": "MyApp.Services.UserService.GetUserById" }
   ```

3. **Review each usage location to plan the refactoring.**
