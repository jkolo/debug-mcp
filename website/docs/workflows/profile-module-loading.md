---
title: Profile Module Loading
sidebar_position: 3
---

# Workflow: Profile Module Loading

This guide walks through using debug-mcp to analyze what assemblies an application loads, explore their types, and understand the application's structure from metadata.

## Scenario

You want to understand the structure of a .NET application without reading its source code — what assemblies are loaded, what types and methods they contain, and how they relate to each other.

## Steps

### 1. Launch the application

```json
// debug_launch
{
  "program": "/app/MyService.dll",
  "stop_at_entry": false
}
```

Launch without stopping at entry — module operations work on running processes too.

### 2. List loaded modules

```json
// modules_list
{
  "include_system": false,
  "name_filter": "MyApp*"
}
```

See all application assemblies (excluding .NET framework assemblies):

```json
{
  "modules": [
    { "name": "MyApp", "version": "1.0.0.0", "hasSymbols": true, "isOptimized": false },
    { "name": "MyApp.Core", "version": "1.0.0.0", "hasSymbols": true, "isOptimized": false },
    { "name": "MyApp.Data", "version": "1.0.0.0", "hasSymbols": false, "isOptimized": true }
  ]
}
```

Notice `MyApp.Data` has no symbols and is optimized — debugging will be limited.

### 3. Search for types across modules

```json
// modules_search
{
  "pattern": "*Service*",
  "search_type": "types",
  "module_filter": "MyApp*"
}
```

Find all service classes:

```json
{
  "types": [
    { "fullName": "MyApp.Services.UserService", "moduleName": "MyApp" },
    { "fullName": "MyApp.Services.OrderService", "moduleName": "MyApp" },
    { "fullName": "MyApp.Core.Interfaces.IUserService", "moduleName": "MyApp.Core" }
  ]
}
```

### 4. Browse types in a module

```json
// types_get
{
  "module_name": "MyApp",
  "namespace_filter": "MyApp.Models*",
  "kind": "class"
}
```

See all model classes:

```json
{
  "types": [
    { "fullName": "MyApp.Models.User", "kind": "class", "baseType": "System.Object" },
    { "fullName": "MyApp.Models.Order", "kind": "class", "baseType": "System.Object" },
    { "fullName": "MyApp.Models.Product", "kind": "class", "baseType": "System.Object" }
  ]
}
```

### 5. Inspect a type's members

```json
// members_get
{
  "type_name": "MyApp.Services.OrderService",
  "member_kinds": "methods,properties",
  "visibility": "public"
}
```

Understand the type's API:

```json
{
  "methods": [
    { "name": "CreateOrder", "signature": "Order CreateOrder(OrderRequest request)" },
    { "name": "GetOrder", "signature": "Order GetOrder(int id)" },
    { "name": "CancelOrder", "signature": "void CancelOrder(int id)" }
  ],
  "properties": [
    { "name": "OrderCount", "type": "int", "hasGetter": true, "hasSetter": false }
  ]
}
```

### 6. Disconnect

```json
// debug_disconnect
{
  "terminate": true
}
```

## Summary

| Step | Tool | Purpose |
|------|------|---------|
| 1 | `debug_launch` | Start the application |
| 2 | `modules_list` | See loaded assemblies |
| 3 | `modules_search` | Find types by name pattern |
| 4 | `types_get` | Browse types in a module |
| 5 | `members_get` | Inspect type API surface |
| 6 | `debug_disconnect` | Clean up |

## Tips

- Module tools work on running processes — no need to pause execution.
- Use `hasSymbols: false` in `modules_list` to identify assemblies where you'll have limited debugging ability.
- Use `include_inherited: true` in `members_get` to see the complete API including base class members.
- Use `modules_search` with `search_type: "methods"` to find methods by name across all assemblies.
