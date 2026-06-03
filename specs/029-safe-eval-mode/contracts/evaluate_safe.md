# MCP Tool Contract: evaluate_safe

## Overview

| Field       | Value |
|-------------|-------|
| Tool name   | `evaluate_safe` |
| Title       | `Evaluate Expression (Safe Mode)` |
| ReadOnly    | `true` |
| Destructive | `false` |
| Idempotent  | `false` |
| OpenWorld   | `false` |

## Description

Evaluate a C# expression in the debuggee context with safety guardrails. Blocks method calls that could have side effects before they reach the debugged process. Suitable for autonomous agents that must not accidentally mutate state.

Permitted operations: member reads, property access, arithmetic (+, -, *, /, %), comparisons (==, !=, <, >, <=, >=), logical operators (&&, ||, !), ternary (?:), indexer reads, null-conditional access (?., ?[]), and methods explicitly on the safe-eval allowlist.

Blocked operations: all method invocations not on the allowlist, object construction (`new T()`), and assignment expressions (`=`, `+=`, etc.).

Returns the same value/type/has_children shape as `evaluate` on success. On rejection, returns `success: false` with `code: safe_eval_rejected` and details identifying the offending sub-expression.

## Parameters

| Name         | Type    | Required | Default | Description |
|--------------|---------|----------|---------|-------------|
| `expression` | string  | yes      | —       | C# expression to evaluate safely. Cannot be empty. |
| `thread_id`  | integer | no       | current | Thread context for variable resolution. |
| `frame_index`| integer | no       | 0       | Stack frame context (0 = top frame). Must be >= 0. |
| `timeout_ms` | integer | no       | 5000    | Evaluation timeout in milliseconds (100–60000). Applied only if the expression passes the safety check. |

## Responses

### Success

```json
{
  "success": true,
  "value": "<string representation of result>",
  "type": "<CLR type name>",
  "has_children": false
}
```

### Rejected by safety check

```json
{
  "success": false,
  "error": {
    "code": "safe_eval_rejected",
    "message": "Expression contains a method call that is not on the safe-eval allowlist",
    "details": {
      "rejection_category": "MethodCall | ObjectCreation | Assignment",
      "offending_expression": "<the problematic sub-expression>",
      "allowed_operations": "member reads, property access, arithmetic, comparisons, logical operators, allowlisted methods"
    }
  }
}
```

### Evaluation error (expression passed safety, but runtime evaluation failed)

Same error shape as `evaluate` tool — codes: `eval_timeout`, `eval_exception`, `null_reference`, etc.

### Other errors (same as `evaluate`)

| Code               | When |
|--------------------|------|
| `syntax_error`     | Expression cannot be parsed |
| `no_session`       | No active debug session |
| `not_paused`       | Process not paused |
| `invalid_parameter`| Parameter validation failed (frame_index < 0, timeout out of range) |

## Allowlist Configuration

The default allowlist includes (at minimum):

```
String.Format, String.Concat, String.IsNullOrEmpty, String.IsNullOrWhiteSpace,
String.Join, String.Compare, String.Equals,
Math.* (all Math members),
Enumerable.Count, Enumerable.Any, Enumerable.First, Enumerable.FirstOrDefault,
Enumerable.Last, Enumerable.LastOrDefault, Enumerable.ToList, Enumerable.ToArray,
Object.ToString, Object.Equals, Object.GetHashCode
```

Extended at startup via `--safe-eval-allowlist "Pattern1,Pattern2"`:
- `Math.*` — all members of any type named `Math`
- `String.Format` — specific method
- `MyHelper.*` — all members of `MyHelper`

## Comparison with `evaluate`

| Feature                    | `evaluate` | `evaluate_safe` |
|----------------------------|-----------|-----------------|
| Method calls               | Allowed   | Blocked (unless allowlisted) |
| Object construction        | Allowed   | Blocked |
| Assignments                | Allowed   | Blocked |
| Safety check before exec   | No        | Yes |
| For autonomous agents      | Risky     | Recommended |
| For intentional side effects | Yes     | No |
