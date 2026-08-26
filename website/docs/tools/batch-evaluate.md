---
title: Batch Evaluate
sidebar_position: 10
---

# Batch Evaluate

`batch_evaluate` submits up to 20 micro-experiments — each a trigger location, optional capture
expressions, an optional condition, and a max hit count — in a single call, and collects every
hit across all of them before returning.

## When to Use

Use `batch_evaluate` when you'd otherwise need many rounds of `breakpoint_set` /
`debug_continue` / *(wait for notification)* / `evaluate` / `breakpoint_remove` to answer a
question that spans several locations — for example "what values does `x` take across these 5
call sites during one run?" Pre-existing breakpoints are automatically disabled for the
duration of the batch and restored afterward, so it doesn't interfere with breakpoints you've
already set.

**Typical flow:** *(process running or paused)* → `batch_evaluate` with a JSON array of experiments → inspect the returned hits per experiment

## Tools

### batch_evaluate

Submit a batch of up to 20 micro-experiments in one call.

**Requires:** Active session (running or paused)

**When to use:** You want captured values from multiple source locations in a single debugging
pass instead of setting and removing breakpoints one at a time. Each experiment can run in
`blocking` mode (pauses execution, like a normal breakpoint) or `non_blocking` mode (captures
values and continues automatically, like a tracepoint), independently per experiment.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `experiments` | string | Yes | JSON array of experiment objects (see shape below) |
| `timeoutSeconds` | integer | No | Timeout in seconds before the batch returns partial results (default: 30) |
| `evalMode` | string | No | `safe` (default — blocks unsafe expressions, same rules as `evaluate_safe`) or `full` (allows all expressions, same rules as `evaluate`) |
| `maxTotalHits` | integer | No | Maximum total hits across all experiments before ending early (default: 500) |

**Experiment object shape** (each entry of the `experiments` JSON array):

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `trigger.file` | string | Yes | Source file path |
| `trigger.line` | integer | Yes | Line number (1-based) |
| `mode` | string | No | `blocking` (default) or `non_blocking` |
| `capture` | string[] | No | Expressions to evaluate and record on each hit |
| `condition` | string | No | Only counts as a hit when this expression is truthy |
| `max_hits` | integer | No | Stop this experiment after N hits (default: 1) |

**Example:**
```json
{
  "experiments": "[{\"trigger\":{\"file\":\"Services/OrderService.cs\",\"line\":55},\"mode\":\"non_blocking\",\"capture\":[\"order.Total\",\"order.Status\"],\"max_hits\":5},{\"trigger\":{\"file\":\"Services/OrderService.cs\",\"line\":80},\"mode\":\"blocking\",\"condition\":\"order.Total < 0\",\"capture\":[\"order.Total\"],\"max_hits\":1}]",
  "timeoutSeconds": 30,
  "evalMode": "safe",
  "maxTotalHits": 500
}
```

**Response:**
```json
{
  "success": true,
  "completion_reason": "all_triggered",
  "total_experiments": 2,
  "triggered": 2,
  "not_triggered": 0,
  "errors": 0,
  "experiments": [
    {
      "index": 0,
      "status": "triggered",
      "hit_count": 3,
      "hits": [
        {
          "timestamp": "2024-01-15T10:30:45.123Z",
          "thread_id": 5,
          "location": { "file": "Services/OrderService.cs", "line": 55 },
          "values": { "order.Total": "299.99", "order.Status": "\"Pending\"" }
        }
      ]
    },
    {
      "index": 1,
      "status": "not_triggered",
      "hit_count": 0,
      "hits": []
    }
  ]
}
```

**Errors:**

Structured `{ "success": false, "error": { "code", "message" } }`. These five codes are the only
ones in the shared `ErrorCodes` catalog that keep their pre-existing lowercase form instead of
`UPPER_SNAKE_CASE` — preserved as-is to avoid a wire-visible change to this tool's error codes:
- `validation_error` — Batch request parameters failed validation
- `batch_already_running` — A batch is already running; only one batch can run at a time
- `invalid_json` — The `experiments` JSON could not be parsed
- `cancelled` — The batch evaluation was cancelled
- `internal_error` — An unexpected error occurred while running the batch

**Real-world use case:** An AI agent investigating why some orders end up with a negative total sets one non-blocking experiment logging `order.Total` at every stage of the pipeline, and one blocking experiment with `condition: "order.Total < 0"` at the final stage — a single `batch_evaluate` call traces the value across the whole pipeline and pauses execution exactly when it goes negative, without ever using `breakpoint_set` directly.
