---
title: Modules
sidebar_position: 6
---

# Modules

Module tools let you browse loaded assemblies, explore types and their members, and search across the codebase — all without needing source code.

## When to Use

Use module tools to understand the structure of the debugged application at the metadata level. These tools work with both **running** and **paused** sessions because they only read assembly metadata.

**Typical flow:** `modules_list` → `modules_search` (find types) → `types_get` (browse a module) → `members_get` (inspect a type)

## Tools

### modules_list

List all loaded modules (assemblies) in the debugged process.

**Requires:** Active session (running or paused)

**When to use:** See what assemblies are loaded, check if your assembly has debug symbols, or filter to find application assemblies vs. framework assemblies.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `include_system` | boolean | No | Include system assemblies like mscorlib (default: true) |
| `name_filter` | string | No | Filter modules by name pattern (supports `*` wildcard) |

**Example:**
```json
{
  "include_system": false,
  "name_filter": "MyApp*"
}
```

**Response:**
```json
{
  "success": true,
  "modules": [
    {
      "name": "MyApp",
      "path": "/app/MyApp.dll",
      "version": "1.0.0.0",
      "hasSymbols": true,
      "isOptimized": false,
      "isDynamic": false,
      "moduleId": "550e8400-e29b-41d4-a716-446655440000"
    },
    {
      "name": "MyApp.Core",
      "path": "/app/MyApp.Core.dll",
      "version": "1.0.0.0",
      "hasSymbols": true,
      "isOptimized": false,
      "isDynamic": false,
      "moduleId": "550e8400-e29b-41d4-a716-446655440001"
    }
  ],
  "count": 2
}
```

**Real-world use case:** An AI agent wants to understand what libraries an application uses. It calls `modules_list` with `include_system: false` to see only application and third-party assemblies, then checks which ones have debug symbols available.

---

### modules_search

Search for types and methods across all loaded modules.

**Requires:** Active session (running or paused)

**When to use:** Find a type or method by name when you don't know which module it's in. Supports wildcard patterns.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `pattern` | string | Yes | Search pattern (supports `*` wildcard) |
| `search_type` | string | No | `types`, `methods`, or `both` (default: `both`) |
| `module_filter` | string | No | Limit to specific module (supports `*` wildcard) |
| `case_sensitive` | boolean | No | Case-sensitive matching (default: false) |
| `max_results` | integer | No | Max results (max: 100, default: 50) |

**Example:**
```json
{
  "pattern": "*Customer*",
  "search_type": "both",
  "module_filter": "MyApp*",
  "max_results": 50
}
```

**Response:**
```json
{
  "success": true,
  "query": "*Customer*",
  "types": [
    {
      "fullName": "MyApp.Models.Customer",
      "name": "Customer",
      "namespace": "MyApp.Models",
      "kind": "class",
      "visibility": "public",
      "moduleName": "MyApp"
    },
    {
      "fullName": "MyApp.Services.CustomerService",
      "name": "CustomerService",
      "namespace": "MyApp.Services",
      "kind": "class",
      "visibility": "public",
      "moduleName": "MyApp"
    }
  ],
  "methods": [
    {
      "declaringType": "MyApp.Services.CustomerService",
      "moduleName": "MyApp",
      "method": {
        "name": "GetCustomer",
        "signature": "Customer GetCustomer(int id)",
        "returnType": "Customer",
        "visibility": "public",
        "isStatic": false
      }
    }
  ],
  "totalMatches": 3,
  "returnedMatches": 3,
  "truncated": false
}
```

---

### types_get

Get types defined in a module, organized by namespace.

**Requires:** Active session (running or paused)

**When to use:** Browse the types in a specific module. Filter by namespace, kind (class/interface/struct/enum), or visibility.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `module_name` | string | Yes | Module name to browse |
| `namespace_filter` | string | No | Filter by namespace pattern (supports `*` wildcard) |
| `kind` | string | No | Filter: `class`, `interface`, `struct`, `enum`, `delegate` |
| `visibility` | string | No | Filter: `public`, `internal`, `private`, `protected` |
| `max_results` | integer | No | Max types to return (default: 100) |
| `continuation_token` | string | No | Token for pagination |

**Example:**
```json
{
  "module_name": "MyApp",
  "namespace_filter": "MyApp.Services*",
  "kind": "class",
  "visibility": "public"
}
```

**Response:**
```json
{
  "success": true,
  "moduleName": "MyApp",
  "types": [
    {
      "fullName": "MyApp.Services.UserService",
      "name": "UserService",
      "namespace": "MyApp.Services",
      "kind": "class",
      "visibility": "public",
      "isAbstract": false,
      "baseType": "System.Object",
      "interfaces": ["MyApp.Services.IUserService"],
      "moduleName": "MyApp"
    }
  ],
  "namespaces": [
    { "name": "MyApp.Services", "typeCount": 2 }
  ],
  "totalTypes": 2,
  "hasMore": false
}
```

---

### members_get

Get members (methods, properties, fields, events) of a type.

**Requires:** Active session (running or paused)

**When to use:** Understand the API surface of a type — what methods it has, what properties, whether they're public or private, static or instance.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `type_name` | string | Yes | Full type name (e.g., `MyApp.Models.Customer`) |
| `module_name` | string | No | Module containing the type (helps resolve ambiguity) |
| `include_inherited` | boolean | No | Include inherited members (default: false) |
| `member_kinds` | string | No | Comma-separated: `methods`, `properties`, `fields`, `events` |
| `visibility` | string | No | Filter: `public`, `internal`, `private`, `protected` |
| `include_static` | boolean | No | Include static members (default: true) |
| `include_instance` | boolean | No | Include instance members (default: true) |

**Example:**
```json
{
  "type_name": "MyApp.Models.Customer",
  "include_inherited": true,
  "member_kinds": "methods,properties",
  "visibility": "public"
}
```

**Response:**
```json
{
  "success": true,
  "typeName": "MyApp.Models.Customer",
  "methods": [
    {
      "name": "GetFullName",
      "signature": "string GetFullName()",
      "returnType": "string",
      "parameters": [],
      "visibility": "public",
      "isStatic": false,
      "declaringType": "MyApp.Models.Customer"
    },
    {
      "name": "UpdateEmail",
      "signature": "void UpdateEmail(string email)",
      "returnType": "void",
      "parameters": [
        { "name": "email", "type": "string", "isOptional": false }
      ],
      "visibility": "public",
      "isStatic": false,
      "declaringType": "MyApp.Models.Customer"
    }
  ],
  "properties": [
    {
      "name": "Id",
      "type": "int",
      "visibility": "public",
      "hasGetter": true,
      "hasSetter": true,
      "declaringType": "MyApp.Models.Customer"
    }
  ]
}
```

**Real-world use case:** An AI agent finds a type via `modules_search` but doesn't have source code. It uses `members_get` to list all methods and properties, understanding the type's API before deciding where to set breakpoints.
