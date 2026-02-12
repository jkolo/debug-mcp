# Tool Contract: object_summarize

**MCP Tool Name**: `object_summarize`
**Title**: Summarize Object
**Annotations**: `ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false`

## Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `expression` | `string` | Yes | — | Variable name or expression evaluating to an object |
| `max_preview_items` | `int` | No | `5` | Max collection elements to preview inline for collection-typed fields (1–50) |
| `thread_id` | `int?` | No | current | Thread context for evaluation |
| `frame_index` | `int` | No | `0` | Stack frame context (0 = top) |
| `timeout_ms` | `int` | No | `5000` | Evaluation timeout in milliseconds |

## Success Response

```json
{
  "success": true,
  "summary": {
    "typeName": "MyApp.Models.Customer",
    "size": 256,
    "isNull": false,
    "totalFieldCount": 20,
    "inaccessibleFieldCount": 0,
    "fields": [
      { "name": "Id", "type": "System.Int32", "value": "42", "collectionCount": null, "collectionElementType": null },
      { "name": "Name", "type": "System.String", "value": "\"John Doe\"", "collectionCount": null, "collectionElementType": null },
      { "name": "Orders", "type": "System.Collections.Generic.List`1[MyApp.Order]", "value": "List<Order>[12]", "collectionCount": 12, "collectionElementType": "MyApp.Order" },
      { "name": "Balance", "type": "System.Decimal", "value": "1234.56", "collectionCount": null, "collectionElementType": null },
      { "name": "CreatedAt", "type": "System.DateTimeOffset", "value": "2026-01-15T10:30:00+00:00", "collectionCount": null, "collectionElementType": null }
    ],
    "nullFields": [
      "Address",
      "Phone",
      "AlternateEmail",
      "PreferredPayment",
      "Tags"
    ],
    "interestingFields": [
      { "name": "Email", "type": "System.String", "value": "\"\"", "reason": "empty_string" },
      { "name": "Score", "type": "System.Double", "value": "NaN", "reason": "nan" },
      { "name": "LastLogin", "type": "System.DateTimeOffset", "value": "0001-01-01T00:00:00+00:00", "reason": "default_datetime" }
    ]
  }
}
```

## Null Object Response

```json
{
  "success": true,
  "summary": {
    "typeName": "MyApp.Models.Customer",
    "size": 0,
    "isNull": true,
    "totalFieldCount": 0,
    "inaccessibleFieldCount": 0,
    "fields": [],
    "nullFields": [],
    "interestingFields": []
  }
}
```

## Simple Object Response (no anomalies)

```json
{
  "success": true,
  "summary": {
    "typeName": "System.Drawing.Point",
    "size": 16,
    "isNull": false,
    "totalFieldCount": 2,
    "inaccessibleFieldCount": 0,
    "fields": [
      { "name": "X", "type": "System.Int32", "value": "10", "collectionCount": null, "collectionElementType": null },
      { "name": "Y", "type": "System.Int32", "value": "20", "collectionCount": null, "collectionElementType": null }
    ],
    "nullFields": [],
    "interestingFields": []
  }
}
```

## Error Responses

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
    "message": "Variable 'customer' is not available in the current scope."
  }
}
```

### Eval error
```json
{
  "success": false,
  "error": {
    "code": "eval_exception",
    "message": "Failed to evaluate expression 'obj.Property': NullReferenceException"
  }
}
```
