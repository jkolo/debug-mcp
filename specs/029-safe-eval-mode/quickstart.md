# Quickstart: Safe Evaluation Mode (029)

## Verification Steps

After implementation, verify feature correctness with these steps.

### 1. Build passes

```bash
dotnet build
# Expected: 0 errors, 0 warnings
```

### 2. Unit tests pass

```bash
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~Unit.SafeEval|FullyQualifiedName~Contract"
# Expected: all green
```

### 3. Smoke test — safe expression allowed

Launch a .NET process with `debug_launch`, set a breakpoint, wait for it to pause, then call:

```json
{ "expression": "user.Name" }
```
→ `evaluate_safe` with a member read.

**Expected**: `{"success": true, "value": "Alice", "type": "System.String", "has_children": false}`

### 4. Smoke test — method call blocked

With same session paused, call:

```json
{ "expression": "File.Delete(\"test.txt\")" }
```

**Expected**:
```json
{
  "success": false,
  "error": {
    "code": "safe_eval_rejected",
    "details": {
      "rejection_category": "MethodCall",
      "offending_expression": "File.Delete(\"test.txt\")"
    }
  }
}
```

### 5. Smoke test — allowlisted method permitted

```json
{ "expression": "Math.Abs(delta)" }
```

**Expected**: `{"success": true, "value": "5", ...}`

### 6. Smoke test — rejection before process interaction

Start the MCP server but do NOT attach to any process. Call `evaluate_safe` with a blocked expression:

```json
{ "expression": "db.Drop()" }
```

**Expected**: `{"success": false, "error": {"code": "safe_eval_rejected", ...}}`  
(Returns immediately — no `no_session` error, because safety check runs before session check.)

Wait — actually the spec and tool should check session first (existing `evaluate` behavior) or safety first? Let's define: **safety check first** — an agent should be able to validate expression safety without a live session. This is faster and lets agents pre-screen expressions.

Actually, re-checking the spec: FR-004 says "Rejection MUST occur before the expression is executed in the debugged process." This implies safety check first, session check second is acceptable. Let's do safety first.

### 7. Verify CLI allowlist extension

Start the server with extra allowlist:

```bash
dotnet run --project DebugMcp -- --safe-eval-allowlist "Console.*"
```

Then `evaluate_safe` with `Console.WriteLine("x")` while paused.

**Expected**: allowed through to the runtime evaluator (not rejected by safety check).

### 8. Verify tool annotation

```bash
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~ToolAnnotationTests"
# evaluate_safe must appear with ReadOnly=true, Destructive=false
```
