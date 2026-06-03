# Data Model: Safe Evaluation Mode (029)

## Entities

### SafeEvalAllowlist

Immutable value object populated at server startup from default entries + optional CLI-supplied patterns.

```
SafeEvalAllowlist
  entries: IReadOnlySet<AllowlistEntry>   # default + user-supplied, normalized
  
  IsAllowed(receiverSimpleName: string, methodName: string) → bool
  ParseAndAdd(patterns: string)            # parses comma-separated patterns
```

**Validation rules:**
- Empty `receiverSimpleName` is treated as a global method — not matched by any type-qualified entry (e.g. bare `Format(...)` calls are blocked unless the receiver pattern is also unqualified).
- Wildcard (`*`) in `MethodName` position matches any method on the receiver type.
- `TypeSimpleName == null` matches any receiver for the given method — used exclusively for `ToString`, `Equals`, `GetHashCode` in the default built-in set. CLI patterns always produce type-qualified entries (non-null `TypeSimpleName`).
- Patterns are case-sensitive (C# is case-sensitive).

---

### AllowlistEntry

```
AllowlistEntry (value type / record)
  TypeSimpleName: string?   # last segment of type name, e.g. "Math" from "System.Math"
                            # null means "any receiver type" — used for universal methods in the default set
  MethodName: string        # exact method name or "*" for wildcard
```

**Parsing:** `"System.Math.*"` → `AllowlistEntry("Math", "*")`.  
**Parsing:** `"String.Format"` → `AllowlistEntry("String", "Format")`.  
**Parsing:** `"Math.*"` → `AllowlistEntry("Math", "*")` (same result, namespace prefix stripped).  
**Any-receiver entries** (default set only, not user-configurable via CLI): `ToString`, `Equals`, `GetHashCode` are stored as `AllowlistEntry(null, "ToString")` etc. — `TypeSimpleName = null` means the entry matches any receiver (e.g. `user.ToString()`, `order.ToString()`).

---

### SafeAnalysisResult

Discriminated union returned by `ISafeExpressionAnalyzer.Analyze`.

```
SafeAnalysisResult
  IsAllowed: bool
  Rejection: SafeEvalRejection?    # null when IsAllowed = true

  static Allowed()   → SafeAnalysisResult { IsAllowed = true }
  static Rejected(r) → SafeAnalysisResult { IsAllowed = false, Rejection = r }
```

---

### SafeEvalRejection

```
SafeEvalRejection (record)
  Category: RejectionCategory      # enum: MethodCall, ObjectCreation, Assignment, ParseError
  OffendingExpression: string      # the sub-expression text that triggered rejection
  Message: string                  # human-readable explanation
```

**RejectionCategory values:**
- `MethodCall` — a non-allowlisted method invocation was found
- `ObjectCreation` — a `new T(...)` constructor call was found
- `Assignment` — a write (`=`, `+=`, etc.) was found
- `ParseError` — expression could not be parsed (syntax error)

---

## State Transitions

No persistent state changes — the safe eval feature is stateless. `SafeEvalAllowlist` is constructed once at startup and is read-only thereafter. `SafeAnalysisResult` is produced per tool call and not stored.

## Response Schema (MCP tool output)

**Success (expression safe + evaluated):**
```json
{
  "success": true,
  "value": "42",
  "type": "System.Int32",
  "has_children": false
}
```

**Rejected (safety check failed):**
```json
{
  "success": false,
  "error": {
    "code": "safe_eval_rejected",
    "message": "Expression contains a method call that is not on the safe-eval allowlist",
    "details": {
      "rejection_category": "MethodCall",
      "offending_expression": "repo.Save(entity)",
      "allowed_operations": "member reads, property access, arithmetic, comparisons, logical operators, allowlisted methods"
    }
  }
}
```

**Parse error:**
```json
{
  "success": false,
  "error": {
    "code": "syntax_error",
    "message": "Expression could not be parsed: unexpected token '}'",
    "position": 12
  }
}
```
