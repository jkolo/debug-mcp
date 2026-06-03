# Implementation Plan: Safe Evaluation Mode

**Branch**: `029-safe-eval-mode` | **Date**: 2026-06-03 | **Spec**: [spec.md](spec.md)

## Summary

Add an `evaluate_safe` MCP tool that performs static analysis (Roslyn AST walk) on a C# expression before executing it in the debugged process. Method invocations and object construction are blocked by default; a configurable allowlist (default: 27 pure methods) permits known-safe calls. The safety check runs before any session/process interaction, giving autonomous AI agents a reliable guardrail against accidentally triggering destructive side effects.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0  
**Primary Dependencies**: `Microsoft.CodeAnalysis.CSharp.Workspaces` (already referenced) — `CSharpSyntaxTree`, `CSharpSyntaxWalker`, `InvocationExpressionSyntax`  
**Storage**: N/A — stateless per-call analysis; `SafeEvalAllowlist` singleton constructed once at startup  
**Testing**: xUnit + FluentAssertions (existing stack)  
**Target Platform**: Linux/Windows/macOS x64+ARM64 (same as project)  
**Performance Goals**: Safety check <50ms (SC-003) — syntactic only, no compilation  
**Constraints**: No new NuGet packages required; reuses existing Roslyn dependency  
**Scale/Scope**: One new tool, one new service, one new model directory, CLI option

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | ✅ Pass | Safety check is pre-execution gate, not an external debugger |
| II. MCP Compliance | ✅ Pass | Tool name `evaluate_safe` follows `noun_verb`; structured JSON responses; ReadOnly=true; Destructive=false |
| III. Test-First | ✅ Required | All implementation steps use RED→GREEN→REFACTOR; unit tests for analyzer, contract tests for tool |
| IV. Simplicity | ✅ Pass | No new packages; syntactic AST walk (<100 lines); no abstraction layers beyond what's needed |
| V. Observability | ✅ Required | Tool invocations must log via `ToolInvoked`/`ToolCompleted`/`ToolError` helpers (existing pattern) |

**Complexity tracking**: No violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/029-safe-eval-mode/
├── plan.md              ← this file
├── spec.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── evaluate_safe.md
├── checklists/
│   └── requirements.md
└── tasks.md             ← created by /speckit.tasks
```

### Source Code

```text
DebugMcp/
├── Services/
│   └── SafeEval/                          ← NEW
│       ├── ISafeExpressionAnalyzer.cs
│       ├── SafeExpressionAnalyzer.cs
│       ├── SafeEvalAllowlist.cs
│       └── SafeAnalysisResult.cs          # contains SafeEvalRejection, RejectionCategory
├── Tools/
│   └── EvaluateSafeTool.cs                ← NEW
└── Program.cs                             ← MODIFIED (CLI option + DI registration)

tests/DebugMcp.Tests/
├── Unit/
│   └── SafeEval/                          ← NEW
│       ├── SafeExpressionAnalyzerTests.cs
│       └── SafeEvalAllowlistTests.cs
└── Contract/
    └── (existing ToolAnnotationTests.cs)  ← WILL auto-cover new tool via reflection
```

**Structure Decision**: Single project, feature-local service directory under `Services/SafeEval/` — mirrors `Services/Resources/`, `Services/Inspection/` pattern.

## Implementation Steps

Steps follow TDD (RED → GREEN → REFACTOR). Build and run relevant tests after each step.

### Step 1 — Core models (RED: write types; no implementation yet)

Create `DebugMcp/Services/SafeEval/SafeAnalysisResult.cs`:
- `RejectionCategory` enum: `MethodCall`, `ObjectCreation`, `Assignment`, `ParseError`
- `SafeEvalRejection` positional record: `(RejectionCategory Category, string OffendingExpression, string Message)`
- `SafeAnalysisResult` record: `(bool IsAllowed, SafeEvalRejection? Rejection)` with static `Allowed()` and `Rejected(SafeEvalRejection)` factory methods

Create `DebugMcp/Services/SafeEval/ISafeExpressionAnalyzer.cs`:
- Interface with `SafeAnalysisResult Analyze(string expression)`

Verify: `dotnet build` (0 errors)

---

### Step 2 — Allowlist (RED → GREEN → REFACTOR)

> **Note on ordering**: `tasks.md` implements US1 (analyzer without allowlist, blocks all invocations) BEFORE US2 (allowlist). This plan groups the allowlist step here for conceptual clarity — in practice, follow `tasks.md` phase ordering during incremental delivery.

**RED**: Write `SafeEvalAllowlistTests.cs`:
- `Parse_SimpleMethod_Matches` — `"String.Format"` allows `String.Format`
- `Parse_Wildcard_MatchesAnyMethod` — `"Math.*"` allows `Math.Abs` and `Math.Round`
- `Parse_QualifiedName_StripsNamespace` — `"System.Math.*"` allows `Math.Abs`
- `Parse_UnknownMethod_NotAllowed` — `"File.Delete"` is NOT on default set
- `Default_ContainsRequiredMethods` — default list has ≥20 entries
- `Default_ContainsToString` — `IsAllowed("User", "ToString") = true` and `IsAllowed("Order", "ToString") = true` (any receiver matches; stored as `AllowlistEntry(null, "ToString")`)
- `IsAllowed_CaseSensitive` — `math.abs` is NOT allowed when allowlist has `Math.Abs`

Run: `dotnet test --filter "FullyQualifiedName~SafeEvalAllowlistTests"` → all RED

**GREEN**: Create `DebugMcp/Services/SafeEval/SafeEvalAllowlist.cs`:
- `AllowlistEntry` record: `(string? TypeSimpleName, string MethodName)` — `IsWildcard = MethodName == "*"`; `TypeSimpleName == null` = any-receiver entry (for `ToString`, `Equals`, `GetHashCode`)
- Parse pattern: strip to last `.`-separated segment for type, take method name (or `*`)
- `IsAllowed(string receiverSimpleName, string methodName)` — an entry matches when `(TypeSimpleName == null || TypeSimpleName == receiverSimpleName) && (IsWildcard || MethodName == methodName)`
- Default entries: at minimum the 27 methods from research.md §4; `ToString`, `Equals`, `GetHashCode` stored as any-receiver entries (`TypeSimpleName = null`)

Run: `dotnet test --filter "FullyQualifiedName~SafeEvalAllowlistTests"` → all GREEN

**REFACTOR**: Extract `ParseEntry(string pattern)` helper; ensure default entries are a static `IReadOnlySet`.

---

### Step 3 — Expression analyzer (RED → GREEN → REFACTOR)

**RED**: Write `SafeExpressionAnalyzerTests.cs`:

*Allowed expressions:*
- `"user.Name"` → `IsAllowed = true`
- `"list.Count"` → `IsAllowed = true` (property, not invocation)
- `"a + b * 2"` → `IsAllowed = true`
- `"x > 0 ? y : z"` → `IsAllowed = true`
- `"arr[0]"` → `IsAllowed = true`
- `"user?.Name"` → `IsAllowed = true`
- `"42"` → `IsAllowed = true`
- `"Math.Abs(delta)"` → `IsAllowed = true` (in default allowlist)
- `"String.Format(\"{0}\", x)"` → `IsAllowed = true` (in default allowlist)

*Blocked expressions (method calls):*
- `"File.Delete(\"x\")"` → `IsAllowed = false`, Category = `MethodCall`, OffendingExpression contains `File.Delete`
- `"db.Drop()"` → `IsAllowed = false`, Category = `MethodCall`
- `"obj.GetList().Count"` → `IsAllowed = false`, Category = `MethodCall` (GetList is blocked even though Count is safe)
- `"list.Add(x)"` → `IsAllowed = false`, Category = `MethodCall`

*Blocked expressions (object creation):*
- `"new List<int>()"` → `IsAllowed = false`, Category = `ObjectCreation`
- `"new StringBuilder()"` → `IsAllowed = false`, Category = `ObjectCreation`

*Blocked expressions (assignment):*
- `"x = 5"` → `IsAllowed = false`, Category = `Assignment`
- `"x += 1"` → `IsAllowed = false`, Category = `Assignment`

*Parse error:*
- `"{ broken"` → `IsAllowed = false`, Category = `ParseError`

Run: `dotnet test --filter "FullyQualifiedName~SafeExpressionAnalyzerTests"` → all RED

**GREEN**: Create `DebugMcp/Services/SafeEval/SafeExpressionAnalyzer.cs`:
- Implement `ISafeExpressionAnalyzer`
- Parse with `CSharpSyntaxTree.ParseText($"_ = {expression};", CSharpParseOptions.Default.WithKind(SourceCodeKind.Script))`
- Check for parse diagnostics with `Error` severity → return `SafeAnalysisResult.Rejected(ParseError)`
- Walk with inner `CSharpSyntaxWalker`:
  - `VisitInvocationExpression`: extract receiver simple name + method name; check allowlist; if not allowed → reject
  - `VisitObjectCreationExpression`: always reject (unless we add ctor allowlist later)
  - `VisitAssignmentExpression`: always reject
- Fail-fast: stop walking after first violation

Run: `dotnet test --filter "FullyQualifiedName~SafeExpressionAnalyzerTests"` → all GREEN

**REFACTOR**: Extract `ExtractReceiverName(InvocationExpressionSyntax)` helper.

---

### Step 4 — EvaluateSafeTool (RED → GREEN → REFACTOR)

**RED**: The contract test `ToolAnnotationTests` uses reflection to verify all `[McpServerTool]` annotations. It will fail (or miss the new tool) until the tool exists. Add explicit assertion for `evaluate_safe` having `ReadOnly = true`, `Destructive = false`.

Run: contract tests → fail for `evaluate_safe`

**GREEN**: Create `DebugMcp/Tools/EvaluateSafeTool.cs`:
- Mirror `EvaluateTool.cs` structure
- Constructor DI: `IDebugSessionManager`, `ISafeExpressionAnalyzer`, `ILogger<EvaluateSafeTool>`
- `[McpServerTool(Name = "evaluate_safe", Title = "Evaluate Expression (Safe Mode)", ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = false)]`
- **Safety check FIRST** (before session check): call `_analyzer.Analyze(expression)`; if rejected → return structured rejection immediately
- If safe: delegate to existing `_sessionManager.EvaluateAsync` (same flow as `EvaluateTool`)
- Rejection response: `success=false`, `error.code="safe_eval_rejected"`, `error.details` includes `rejection_category`, `offending_expression`, `allowed_operations`
- Log: `_logger.ToolInvoked`, `_logger.ToolCompleted`/`_logger.ToolError`

Register in `Program.cs`:
- Add `--safe-eval-allowlist` CLI option (string?, default null)
- Construct `SafeEvalAllowlist` from defaults + parsed CLI value
- `builder.Services.AddSingleton(allowlist)`
- `builder.Services.AddSingleton<ISafeExpressionAnalyzer, SafeExpressionAnalyzer>()`

Run: `dotnet test --filter "FullyQualifiedName~Contract"` → GREEN
Run: `dotnet build` → 0 errors

**REFACTOR**: Extract `CreateRejectionResponse(SafeEvalRejection)` helper in `EvaluateSafeTool`.

---

### Step 5 — Verify all tests green

```bash
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"
```

Expected: 0 failures. If integration tests touched, verify they pass or are in known-flaky list.

---

### Step 6 — Quickstart smoke test

Follow [quickstart.md](quickstart.md) steps 1–8 manually (or via Python MCP client from previous session pattern). Confirm:
- Pure reads allowed ✓
- Method call blocked ✓
- Allowlisted method allowed ✓
- Safety-first: blocked expression returns `safe_eval_rejected` even without active session ✓
- CLI `--safe-eval-allowlist` extends the allowlist ✓
