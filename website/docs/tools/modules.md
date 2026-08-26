---
title: Modules
sidebar_position: 6
---

# Modules

Module tools let you browse loaded assemblies, explore types and their members, and search across the codebase — all without needing source code.

## When to Use

Use module tools to understand the structure of the debugged application at the metadata level. These tools work with both **running** and **paused** sessions because they only read assembly metadata.

**Typical flow:** *(browse loaded modules via the `debugger://modules` resource)* → `modules_search` (find types) → `types_get` (browse a module) → `members_get` (inspect a type)

## Listing loaded modules

There is no `modules_list` tool. Read the **`debugger://modules`** MCP resource instead — it
returns the same information (name, path, version, whether symbols are loaded, module ID, base
address, size) for every loaded assembly in the debuggee process, without a round-trip tool
call. Use `modules_search` when you need to filter or search by name/pattern rather than list
everything.

## Tools

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
| `timeout_ms` | integer | No | Maximum time to wait for the module search, in milliseconds (default: 30000) |

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
| `timeout_ms` | integer | No | Maximum time to wait for the type listing, in milliseconds (default: 30000) |

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
| `timeout_ms` | integer | No | Maximum time to wait for the member listing, in milliseconds (default: 30000) |

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
