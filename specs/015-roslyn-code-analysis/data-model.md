# Data Model: Roslyn Code Analysis

**Feature**: 015-roslyn-code-analysis
**Date**: 2026-02-04

## Entity Definitions

### WorkspaceInfo

Represents a loaded solution or project with summary statistics.

| Field | Type | Description |
|-------|------|-------------|
| `path` | string | Absolute path to .sln or .csproj file |
| `type` | enum | `Solution` or `Project` |
| `projects` | ProjectInfo[] | List of loaded projects |
| `diagnostics` | WorkspaceDiagnostic[] | Warnings/errors from loading |
| `loaded_at` | datetime | Timestamp when workspace was loaded |

### ProjectInfo

Summary of a single project in the workspace.

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Project name |
| `path` | string | Absolute path to .csproj |
| `documents_count` | int | Number of C# source files |
| `target_framework` | string | e.g., "net10.0" |

### WorkspaceDiagnostic

Warning or error encountered during loading.

| Field | Type | Description |
|-------|------|-------------|
| `kind` | enum | `Warning` or `Failure` |
| `message` | string | Description of the issue |
| `project` | string? | Affected project name (if applicable) |

---

### SymbolUsage

A location where a symbol is referenced.

| Field | Type | Description |
|-------|------|-------------|
| `file` | string | Absolute path to source file |
| `line` | int | 1-based line number |
| `column` | int | 1-based column number |
| `end_line` | int | 1-based end line |
| `end_column` | int | 1-based end column |
| `context` | string | Containing member name (method, property, etc.) |
| `kind` | enum | `Read`, `Write`, `Declaration`, `Reference` |

---

### SymbolAssignment

A location where a symbol is assigned/written.

| Field | Type | Description |
|-------|------|-------------|
| `file` | string | Absolute path to source file |
| `line` | int | 1-based line number |
| `column` | int | 1-based column number |
| `assignment_kind` | enum | `Simple`, `Compound`, `Increment`, `Decrement`, `OutParam`, `RefParam`, `Deconstruction` |
| `context` | string | Containing member name |

---

### SymbolDefinition

Location(s) where a symbol is defined.

| Field | Type | Description |
|-------|------|-------------|
| `file` | string? | Absolute path (null for metadata symbols) |
| `line` | int? | 1-based line number (null for metadata) |
| `column` | int? | 1-based column number (null for metadata) |
| `is_from_source` | bool | True if defined in solution source |
| `assembly_name` | string? | Assembly name for metadata symbols |
| `symbol_name` | string | Fully qualified name of the symbol |
| `symbol_kind` | enum | `Class`, `Struct`, `Interface`, `Enum`, `Method`, `Property`, `Field`, `Event`, `Local`, `Parameter` |

---

### DiagnosticInfo

A compiler diagnostic (error, warning, or info).

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Diagnostic code (e.g., "CS0103") |
| `message` | string | Human-readable message |
| `severity` | enum | `Error`, `Warning`, `Info`, `Hidden` |
| `file` | string? | Source file (null for project-level) |
| `line` | int? | 1-based line number |
| `column` | int? | 1-based column number |
| `end_line` | int? | 1-based end line |
| `end_column` | int? | 1-based end column |

---

## Enumerations

### WorkspaceType
- `Solution` - Loaded from .sln file
- `Project` - Loaded from single .csproj file

### UsageKind
- `Read` - Symbol value is read
- `Write` - Symbol value is written
- `Declaration` - Symbol is declared here
- `Reference` - Type/method reference (not read/write)

### AssignmentKind
- `Simple` - Direct assignment: `x = value`
- `Compound` - Compound assignment: `x += value`
- `Increment` - Pre/post increment: `x++`, `++x`
- `Decrement` - Pre/post decrement: `x--`, `--x`
- `OutParam` - Out parameter: `Method(out x)`
- `RefParam` - Ref parameter: `Method(ref x)`
- `Deconstruction` - Deconstruction: `(x, y) = tuple`

### SymbolKind
- `Class`, `Struct`, `Interface`, `Enum`, `Delegate`
- `Method`, `Property`, `Field`, `Event`
- `Local`, `Parameter`
- `Namespace`, `TypeParameter`

### DiagnosticSeverity
- `Error` - Compilation error
- `Warning` - Compiler warning
- `Info` - Informational message
- `Hidden` - Hidden diagnostic

---

## Response Wrappers

All tool responses follow the established pattern:

### Success Response
```json
{
  "success": true,
  "data": { /* WorkspaceInfo | SymbolUsage[] | etc. */ }
}
```

### Error Response
```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable message",
    "details": { /* optional context */ }
  }
}
```

## Error Codes

| Code | Description |
|------|-------------|
| `NO_WORKSPACE` | No solution/project loaded |
| `INVALID_PATH` | Path does not exist or is not .sln/.csproj |
| `LOAD_FAILED` | Workspace failed to load |
| `SYMBOL_NOT_FOUND` | Could not resolve symbol at location |
| `INVALID_LOCATION` | File/line/column out of range |
| `PROJECT_NOT_FOUND` | Project name not in workspace |
| `ANALYSIS_FAILED` | Unexpected analysis error |
