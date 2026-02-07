---
title: Memory
sidebar_position: 5
---

# Memory

Memory tools provide low-level access to process memory — reading raw bytes, analyzing object layout, and tracing object references.

## When to Use

Use memory tools when you need to go beyond variable inspection and understand what's happening at the memory level. This is useful for debugging memory layout issues, analyzing padding, understanding GC behavior, or investigating corruption.

**Typical flow:** `object_inspect` → `layout_get` (understand field layout) → `memory_read` (examine raw bytes) → `references_get` (trace object graph)

## Tools

### memory_read

Read raw memory bytes from the debuggee process.

**Requires:** Paused session

**When to use:** Examine the actual bytes at a memory address. Useful for verifying object contents, checking for corruption, or understanding binary data.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `address` | string | Yes | Memory address in hex (e.g., `0x00007FF8A1234560`) or decimal |
| `size` | integer | No | Number of bytes to read (default: 256, max: 65536) |
| `format` | string | No | Output format: `hex`, `hex_ascii` (default), `raw` |

**Example:**
```json
{
  "address": "0x00007FF8A1234560",
  "size": 64,
  "format": "hex_ascii"
}
```

**Response:**
```json
{
  "success": true,
  "memory": {
    "address": "0x00007FF8A1234560",
    "requestedSize": 64,
    "actualSize": 64,
    "bytes": "48 65 6C 6C 6F 20 57 6F 72 6C 64 21 00 00 00 00\n...",
    "ascii": "Hello World!....\n..."
  }
}
```

**Errors:**
- `NOT_PAUSED` — Process must be paused
- `INVALID_ADDRESS` — Memory address is not accessible
- `SIZE_EXCEEDED` — Requested size exceeds 64KB limit

**Real-world use case:** After finding a suspicious object via `object_inspect`, an AI agent reads the raw bytes at the object's address to verify whether a string field contains expected UTF-8 data or has been corrupted.

---

### layout_get

Get the memory layout of a type including field offsets, sizes, and padding.

**Requires:** Paused session

**When to use:** Understand how the CLR lays out a type in memory — field order, alignment, padding gaps. Useful for performance analysis (cache-line alignment, struct packing) and understanding memory usage.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `type_name` | string | Yes | Full type name or object reference |
| `include_inherited` | boolean | No | Include inherited fields (default: true) |
| `include_padding` | boolean | No | Include padding analysis (default: true) |
| `thread_id` | integer | No | Thread ID (default: current) |
| `frame_index` | integer | No | Frame index (default: 0) |

**Example:**
```json
{
  "type_name": "MyApp.Models.Customer",
  "include_inherited": true,
  "include_padding": true
}
```

**Response:**
```json
{
  "success": true,
  "layout": {
    "typeName": "MyApp.Models.Customer",
    "totalSize": 48,
    "headerSize": 16,
    "dataSize": 32,
    "baseType": "System.Object",
    "isValueType": false,
    "fields": [
      {
        "name": "Id",
        "typeName": "System.Int32",
        "offset": 16,
        "size": 4,
        "alignment": 4,
        "isReference": false,
        "declaringType": "MyApp.Models.Customer"
      },
      {
        "name": "IsActive",
        "typeName": "System.Boolean",
        "offset": 20,
        "size": 1,
        "alignment": 1,
        "isReference": false,
        "declaringType": "MyApp.Models.Customer"
      },
      {
        "name": "Name",
        "typeName": "System.String",
        "offset": 24,
        "size": 8,
        "alignment": 8,
        "isReference": true,
        "declaringType": "MyApp.Models.Customer"
      }
    ],
    "padding": [
      {
        "offset": 21,
        "size": 3,
        "reason": "Alignment padding before Name"
      }
    ]
  }
}
```

**Errors:**
- `NOT_PAUSED` — Process must be paused
- `TYPE_NOT_FOUND` — Cannot find type with given name

**Real-world use case:** An AI agent is investigating why a struct is larger than expected. `layout_get` reveals 7 bytes of padding between fields due to alignment requirements, suggesting the fields could be reordered to save memory.

---

### references_get

Analyze object references — find what objects a target references (outbound).

**Requires:** Paused session

**When to use:** Trace the object graph from a specific object. See what other objects it holds references to. Useful for understanding ownership, finding memory leaks, or mapping object relationships.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `object_ref` | string | Yes | Object reference (variable name or expression) |
| `direction` | string | No | `outbound` (default), `inbound`, or `both` |
| `max_results` | integer | No | Max references to return (default: 50, max: 100) |
| `include_arrays` | boolean | No | Include array element references (default: true) |
| `thread_id` | integer | No | Thread ID (default: current) |
| `frame_index` | integer | No | Frame index (default: 0) |

**Example:**
```json
{
  "object_ref": "orderManager",
  "direction": "outbound",
  "max_results": 50,
  "include_arrays": true
}
```

**Response:**
```json
{
  "success": true,
  "references": {
    "targetAddress": "0x00007FF8A1234560",
    "targetType": "MyApp.Services.OrderManager",
    "outbound": [
      {
        "sourceAddress": "0x00007FF8A1234560",
        "sourceType": "MyApp.Services.OrderManager",
        "targetAddress": "0x00007FF8A1235000",
        "targetType": "MyApp.Repositories.OrderRepository",
        "path": "_repository",
        "referenceType": "Field"
      },
      {
        "sourceAddress": "0x00007FF8A1234560",
        "sourceType": "MyApp.Services.OrderManager",
        "targetAddress": "0x00007FF8A1237000",
        "targetType": "MyApp.Models.Order",
        "path": "_orders[0]",
        "referenceType": "ArrayElement"
      }
    ],
    "outboundCount": 2,
    "truncated": false
  }
}
```

**Errors:**
- `NOT_PAUSED` — Process must be paused
- `INVALID_REFERENCE` — Cannot resolve object reference

:::note
Inbound reference analysis (finding what objects point *to* a given object) is not yet implemented. Currently only outbound references are supported.
:::
