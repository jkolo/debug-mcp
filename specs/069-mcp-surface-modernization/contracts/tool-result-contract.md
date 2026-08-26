# Contract: Tool Result

**Feature**: 069-mcp-surface-modernization | Covers FR-015 … FR-021

Defines what every one of the 39 tools puts on the wire after this feature. Binding on all tools;
no per-tool exceptions.

---

## Discovery — `tools/list`

Every tool entry gains an `outputSchema` describing its success payload. `inputSchema` is
**unchanged** — this feature does not touch tool inputs.

```jsonc
{
  "name": "variables_get",
  "title": "Get Variables",
  "description": "...",
  "inputSchema": { /* unchanged */ },
  "outputSchema": {
    "type": "object",
    "properties": {
      "success":   { "type": "boolean" },
      "variables": {
        "type": "array",
        "items": {
          "type": "object",
          "properties": {
            "name":         { "type": "string" },
            "type":         { "type": "string" },
            "value":        { "type": "string" },
            "scope":        { "type": "string", "enum": ["local", "argument", "this"] },
            "has_children": { "type": "boolean" }
          },
          "required": ["name", "type", "value", "scope", "has_children"]
        }
      }
    },
    "required": ["success"]
  },
  "annotations": { /* unchanged */ }
}
```

**Requirements**
- Every tool **MUST** publish an `outputSchema`. A tool without one fails the build (FR-016, FR-020).
- The spec is binding here: *"If an output schema is provided: Servers MUST provide structured
  results that conform to this schema."* Conformance is asserted by contract test, not assumed.
- **Only `success` may be schema-required.** Every other property — including `error` — must
  declare a default (`= null` on the record parameter) so the generated schema does not require
  it. Corrected after an implementation-time pilot: a failure result omits every domain field, and
  a positional record parameter without a default is schema-required regardless of its C#
  nullability, so `"required": ["success", "variables"]` (this doc's earlier example) made every
  failure result fail its own schema. `variables` is now correctly omitted from `required`.

---

## Success result

```jsonc
{
  "jsonrpc": "2.0",
  "id": 7,
  "result": {
    "resultType": "complete",
    "content": [
      {
        "type": "text",
        "text": "{\"success\":true,\"variables\":[{\"name\":\"count\",\"type\":\"System.Int32\",\"value\":\"42\",\"scope\":\"local\",\"has_children\":false}]}"
      }
    ],
    "structuredContent": {
      "success": true,
      "variables": [
        { "name": "count", "type": "System.Int32", "value": "42",
          "scope": "local", "has_children": false }
      ]
    },
    "isError": false
  }
}
```

**Requirements**
- `structuredContent` **MUST** validate against the tool's published `outputSchema`.
- The `content[0]` text block **MUST** be present and **MUST** carry the same data serialized.
  Mandated by the specification: *"For backwards compatibility, a tool that returns structured
  content SHOULD also return the serialized JSON in a TextContent block."* This is what makes
  SC-009 achievable — a client that reads only `content[]`, as every client does today, sees no
  change (FR-017).
- Field names and meanings are **identical** to what the tool emits today (FR-021).

---

## Failure result

```jsonc
{
  "jsonrpc": "2.0",
  "id": 8,
  "result": {
    "resultType": "complete",
    "content": [
      {
        "type": "text",
        "text": "{\"success\":false,\"error\":{\"code\":\"INVALID_PARAMETER\",\"message\":\"frame_index must be >= 0\",\"details\":{\"parameter\":\"frame_index\",\"value\":-1}}}"
      }
    ],
    "structuredContent": {
      "success": false,
      "error": {
        "code": "INVALID_PARAMETER",
        "message": "frame_index must be >= 0",
        "details": { "parameter": "frame_index", "value": -1 }
      }
    },
    "isError": true
  }
}
```

**Requirements**
- One shape for all 39 tools: `code`, `message`, optional `details` (FR-018).
- `code` **MUST** come from the existing `ErrorCodes` set, defined in
  `DebugMcp/Models/ErrorResponse.cs`. A tool inventing a code fails the build (FR-019).
- `isError: true` **MUST** be set. This is new: today failure is signalled only by the `success`
  field inside the text payload, so a client cannot distinguish success from failure without
  parsing. The spec reserves `isError` for tool *execution* errors, which clients **SHOULD** feed
  back to the model for self-correction. Implemented as one `AddCallToolFilter` in `Program.cs`
  (T053) that reads `success` off every tool's `StructuredContent` after the call — no tool sets
  `isError` itself, so a tool cannot forget it.
- `success: false` is **retained** alongside `isError` so existing consumers keep working.

### Protocol errors are different

Unknown tool and malformed request remain JSON-RPC `error` responses (`-32602` and friends), not
tool results. That behaviour is the SDK's and is unchanged.

---

## Truncation

When a result is bounded, it says so. Silent trimming is forbidden.

```jsonc
"structuredContent": {
  "success": true,
  "variables": [ /* ... */ ],
  "truncation": {
    "returned": 200,
    "available": 4173,
    "reason": "result size cap"
  }
}
```

`available` may be null when the total is not cheaply knowable.

---

## Build-time enforcement (FR-020)

The build fails when any of these hold:

| # | Condition |
|---|---|
| 1 | A tool exists but publishes no `outputSchema`. |
| 2 | A tool's actual result does not validate against its own published `outputSchema`. |
| 3 | A tool exists but is not named anywhere in `website/docs/tools/*.md`. |
| 4 | `website/docs/tools/*.md` names a tool that no longer exists. |

Checks 3 and 4 match **by tool name only**. The user-facing documentation is thematic prose, not
a machine-readable catalogue, so no attempt is made to derive result shapes from it.

Natural home: alongside `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs`, which already
enumerates every tool by name.
