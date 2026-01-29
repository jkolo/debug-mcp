---
title: Inspect Memory Layout
sidebar_position: 2
---

# Workflow: Inspect Memory Layout

This guide walks through using debug-mcp to analyze how objects are laid out in memory — field offsets, padding, sizes, and object references.

## Scenario

You want to understand how the CLR lays out a specific type in memory. This is useful for optimizing struct packing, investigating unexpected memory consumption, or understanding GC behavior.

## Steps

### 1. Attach to the running process

```json
// debug_attach
{
  "pid": 12345
}
```

### 2. Set a breakpoint where the object exists

```json
// breakpoint_set
{
  "file": "Services/OrderService.cs",
  "line": 50
}
```

### 3. Continue and wait for the breakpoint

```json
// debug_continue
{}
```

```json
// breakpoint_wait
{
  "timeout_ms": 30000
}
```

### 4. Inspect the object

```json
// object_inspect
{
  "object_ref": "customer",
  "depth": 2
}
```

See all fields with their values, addresses, and sizes:

```json
{
  "inspection": {
    "address": "0x00007FF8A1234560",
    "typeName": "MyApp.Models.Customer",
    "size": 48,
    "fields": [
      { "name": "Id", "typeName": "System.Int32", "value": "42", "offset": 8, "size": 4 },
      { "name": "IsActive", "typeName": "System.Boolean", "value": "true", "offset": 12, "size": 1 },
      { "name": "Name", "typeName": "System.String", "value": "\"John Doe\"", "offset": 16, "size": 8 }
    ]
  }
}
```

### 5. Get the type's memory layout

```json
// layout_get
{
  "type_name": "MyApp.Models.Customer",
  "include_padding": true
}
```

See field offsets, alignment, and padding gaps:

```json
{
  "layout": {
    "typeName": "MyApp.Models.Customer",
    "totalSize": 48,
    "headerSize": 16,
    "dataSize": 32,
    "fields": [
      { "name": "Id", "offset": 16, "size": 4, "alignment": 4 },
      { "name": "IsActive", "offset": 20, "size": 1, "alignment": 1 },
      { "name": "Name", "offset": 24, "size": 8, "alignment": 8 }
    ],
    "padding": [
      { "offset": 21, "size": 3, "reason": "Alignment padding before Name" }
    ]
  }
}
```

### 6. Read raw memory at the object's address

```json
// memory_read
{
  "address": "0x00007FF8A1234560",
  "size": 48,
  "format": "hex_ascii"
}
```

See the actual bytes:

```json
{
  "memory": {
    "bytes": "00 00 00 00 2A 00 00 00 01 00 00 00 00 00 00 00\n...",
    "ascii": "....*..........."
  }
}
```

### 7. Trace object references

```json
// references_get
{
  "object_ref": "customer",
  "direction": "outbound"
}
```

See what other objects this object holds references to:

```json
{
  "references": {
    "outbound": [
      { "path": "Name", "targetType": "System.String", "referenceType": "Field" },
      { "path": "Orders", "targetType": "List<Order>", "referenceType": "Field" }
    ]
  }
}
```

### 8. Disconnect

```json
// debug_disconnect
{
  "terminate": false
}
```

## Summary

| Step | Tool | Purpose |
|------|------|---------|
| 1 | `debug_attach` | Connect to running process |
| 2-3 | `breakpoint_set` + `breakpoint_wait` | Stop where the object exists |
| 4 | `object_inspect` | See field values and sizes |
| 5 | `layout_get` | Analyze field layout and padding |
| 6 | `memory_read` | Examine raw bytes |
| 7 | `references_get` | Trace object graph |
| 8 | `debug_disconnect` | Detach without terminating |

## Tips

- Use `layout_get` with `include_padding: true` to find wasted space in struct layout.
- The CLR adds a 16-byte header (sync block + method table pointer) to every reference type object.
- Value types (structs) have no header and `headerSize` will be 0.
- Reordering fields in a struct can reduce padding. The CLR may already reorder class fields for optimal layout.
