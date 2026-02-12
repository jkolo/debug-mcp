# Tool Contract: collection_analyze

**MCP Tool Name**: `collection_analyze`
**Title**: Analyze Collection
**Annotations**: `ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false`

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `expression` | `string` | Yes | — | Variable name or expression evaluating to a collection |
| `max_preview_items` | `int` | No | `5` | Number of first/last elements to include (1–50) |
| `thread_id` | `int?` | No | current | Thread context for evaluation |
| `frame_index` | `int` | No | `0` | Stack frame context (0 = top) |
| `timeout_ms` | `int` | No | `5000` | Evaluation timeout in milliseconds |

## Success Response

```json
{
  "success": true,
  "summary": {
    "count": 100,
    "elementType": "System.Int32",
    "collectionType": "System.Collections.Generic.List`1[System.Int32]",
    "kind": "List",
    "nullCount": 0,
    "numericStats": {
      "min": "1",
      "max": "100",
      "average": "50.5"
    },
    "typeDistribution": null,
    "firstElements": [
      { "index": 0, "value": "1", "type": "System.Int32" },
      { "index": 1, "value": "2", "type": "System.Int32" },
      { "index": 2, "value": "3", "type": "System.Int32" },
      { "index": 3, "value": "4", "type": "System.Int32" },
      { "index": 4, "value": "5", "type": "System.Int32" }
    ],
    "lastElements": [
      { "index": 95, "value": "96", "type": "System.Int32" },
      { "index": 96, "value": "97", "type": "System.Int32" },
      { "index": 97, "value": "98", "type": "System.Int32" },
      { "index": 98, "value": "99", "type": "System.Int32" },
      { "index": 99, "value": "100", "type": "System.Int32" }
    ],
    "keyValuePairs": null,
    "isSampled": false
  }
}
```

## Dictionary Response Example

```json
{
  "success": true,
  "summary": {
    "count": 500,
    "elementType": "System.Collections.Generic.KeyValuePair`2[System.String,MyApp.Order]",
    "collectionType": "System.Collections.Generic.Dictionary`2[System.String,MyApp.Order]",
    "kind": "Dictionary",
    "nullCount": 0,
    "numericStats": null,
    "typeDistribution": null,
    "firstElements": null,
    "lastElements": null,
    "keyValuePairs": [
      { "key": "\"ORD-001\"", "keyType": "System.String", "value": "{MyApp.Order}", "valueType": "MyApp.Order" },
      { "key": "\"ORD-002\"", "keyType": "System.String", "value": "{MyApp.Order}", "valueType": "MyApp.Order" }
    ],
    "isSampled": false
  }
}
```

## Mixed-Type Collection Response Example

```json
{
  "success": true,
  "summary": {
    "count": 100,
    "elementType": "System.Object",
    "collectionType": "System.Collections.Generic.List`1[System.Object]",
    "kind": "List",
    "nullCount": 25,
    "numericStats": null,
    "typeDistribution": [
      { "typeName": "System.String", "count": 40 },
      { "typeName": "System.Int32", "count": 35 },
      { "typeName": "null", "count": 25 }
    ],
    "firstElements": [
      { "index": 0, "value": "\"hello\"", "type": "System.String" },
      { "index": 1, "value": "42", "type": "System.Int32" },
      { "index": 2, "value": "null", "type": "null" }
    ],
    "lastElements": [
      { "index": 98, "value": "\"world\"", "type": "System.String" },
      { "index": 99, "value": "7", "type": "System.Int32" }
    ],
    "keyValuePairs": null,
    "isSampled": false
  }
}
```

## Error Responses

### Not a collection
```json
{
  "success": false,
  "error": {
    "code": "not_collection",
    "message": "Expression 'customer' evaluates to type 'MyApp.Customer', which is not a recognized collection type. Use object_summarize instead."
  }
}
```

### Not paused
```json
{
  "success": false,
  "error": {
    "code": "not_paused",
    "message": "Process is not paused. Cannot inspect variables while running."
  }
}
```

### Variable not found
```json
{
  "success": false,
  "error": {
    "code": "variable_unavailable",
    "message": "Variable 'items' is not available in the current scope."
  }
}
```
