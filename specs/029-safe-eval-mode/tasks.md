# Tasks: Safe Evaluation Mode (029)

**Input**: Design documents from `/specs/029-safe-eval-mode/`
**Branch**: `029-safe-eval-mode`
**Tech stack**: C# 13 / .NET 10.0, xUnit + FluentAssertions, Microsoft.CodeAnalysis.CSharp.Workspaces (existing)

**Tests**: Included — Constitution §III mandates TDD (non-negotiable). Tests written BEFORE implementation (RED → GREEN).

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1/US2/US3 maps to user stories from spec.md

---

## Phase 1: Setup

**Purpose**: Directory scaffold — no logic, just structure.

- [x] T001 Create directory `DebugMcp/Services/SafeEval/` (placeholder — verified by first file write)
- [x] T002 Create directory `tests/DebugMcp.Tests/Unit/SafeEval/` (placeholder — verified by first test file write)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models and interface used by all three user stories. MUST complete before any story work.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T003 Create `DebugMcp/Services/SafeEval/SafeAnalysisResult.cs` — `RejectionCategory` enum (`MethodCall`, `ObjectCreation`, `Assignment`, `ParseError`), `SafeEvalRejection` positional record `(RejectionCategory Category, string OffendingExpression, string Message)`, `SafeAnalysisResult` record with static `Allowed()` and `Rejected(SafeEvalRejection)` factory methods
- [x] T004 Create `DebugMcp/Services/SafeEval/ISafeExpressionAnalyzer.cs` — interface with single method `SafeAnalysisResult Analyze(string expression)`
- [x] T005 Run `dotnet build` — confirm 0 errors before proceeding

**Checkpoint**: Core types defined — user story implementation can now begin.

---

## Phase 3: User Story 1 — Block Destructive Expressions (Priority: P1) 🎯 MVP

**Goal**: `evaluate_safe` MCP tool exists and rejects method calls / object construction / assignments before they reach the debugged process. Pure read expressions (member access, arithmetic, comparisons) pass through.

**Independent Test**: Call `evaluate_safe` with `File.Delete("x")` → `safe_eval_rejected`; call with `user.Name` → success value. No allowlist configuration needed.

### Tests for US1 (write FIRST — must fail before implementation)

- [x] T006 [P] [US1] Write `tests/DebugMcp.Tests/Unit/SafeEval/SafeExpressionAnalyzerTests.cs` — **allowed expressions**: `user.Name` (member read), `list.Count` (property), `a + b * 2` (arithmetic), `x > 0 ? y : z` (ternary), `arr[0]` (indexer), `user?.Name` (null-conditional), `42` (literal), `"hello"` (string literal), `x > 0 && y < 10` (logical AND), `!flag` (logical NOT) — all must return `IsAllowed = true`
- [x] T007 [US1] Extend `tests/DebugMcp.Tests/Unit/SafeEval/SafeExpressionAnalyzerTests.cs` — **blocked: method calls**: `File.Delete("x")` → `Category=MethodCall`, `db.Drop()` → `Category=MethodCall`, `list.Add(x)` → `Category=MethodCall`, `obj.GetList().Count` → `Category=MethodCall` (nested call blocks the whole expression)
- [x] T008 [US1] Extend `tests/DebugMcp.Tests/Unit/SafeEval/SafeExpressionAnalyzerTests.cs` — **blocked: object creation**: `new List<int>()` → `Category=ObjectCreation`; **blocked: assignment**: `x = 5` → `Category=Assignment`, `x += 1` → `Category=Assignment`; **parse error**: `"{ broken"` → `Category=ParseError`
- [x] T009 [US1] Run `dotnet test --filter "FullyQualifiedName~SafeExpressionAnalyzerTests"` — confirm ALL tests RED (fail with NotImplementedException or missing type)

### Implementation for US1

- [x] T010 [US1] Implement `DebugMcp/Services/SafeEval/SafeExpressionAnalyzer.cs`:
  - Parse with `CSharpSyntaxTree.ParseText($"_ = {expression};", CSharpParseOptions.Default.WithKind(SourceCodeKind.Script))`
  - If parse has Error diagnostics → `SafeAnalysisResult.Rejected(new SafeEvalRejection(ParseError, ..., ...))`
  - Walk with inner `CSharpSyntaxWalker`, fail-fast on first violation:
    - `VisitInvocationExpression` → reject as `MethodCall` (temporarily: block ALL invocations; allowlist added in US2)
    - `VisitObjectCreationExpression` → reject as `ObjectCreation`
    - `VisitAssignmentExpression` → reject as `Assignment`
  - If no violations → `SafeAnalysisResult.Allowed()`
- [x] T011 [US1] Run `dotnet test --filter "FullyQualifiedName~SafeExpressionAnalyzerTests"` — confirm GREEN (all allowed/blocked tests pass; allowlisted-method tests added in US2 will still fail — acceptable)
- [x] T012 [US1] Create `DebugMcp/Tools/EvaluateSafeTool.cs` — mirror `EvaluateTool.cs` structure:
  - Constructor DI: `IDebugSessionManager`, `ISafeExpressionAnalyzer`, `ILogger<EvaluateSafeTool>`
  - `[McpServerTool(Name = "evaluate_safe", Title = "Evaluate Expression (Safe Mode)", ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = false)]`
  - **Safety check first** (before session/pause check): call `_analyzer.Analyze(expression)` → if rejected, return `safe_eval_rejected` immediately
  - If safe: delegate to `_sessionManager.EvaluateAsync` (same flow as `EvaluateTool`)
  - Log `ToolInvoked` / `ToolCompleted` / `ToolError` via existing `_logger` helpers
- [x] T013 [US1] Register in `DebugMcp/Program.cs`:
  - Add temporary `builder.Services.AddSingleton<ISafeExpressionAnalyzer, SafeExpressionAnalyzer>()`
  - (Allowlist DI added in US2 once `SafeEvalAllowlist` exists)
- [x] T014 [US1] Run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — confirm 0 failures (contract test auto-covers `evaluate_safe` annotation via reflection)
- [x] T015 [US1] Run `dotnet build` — confirm 0 errors, 0 warnings

**Checkpoint**: `evaluate_safe` tool exists, rejects method calls/object creation/assignment, allows pure reads. US1 independently testable.

---

## Phase 4: User Story 2 — Allowlist Known-Pure Methods (Priority: P2)

**Goal**: Default allowlist of ≥20 pure methods (Math.\*, String.Format, Enumerable.Count, ToString, etc.) is built-in. `--safe-eval-allowlist` CLI option extends it. `Math.Abs(delta)` and `String.Format(...)` pass through `evaluate_safe`.

**Independent Test**: Call `evaluate_safe` with `Math.Abs(delta)` while paused → success value (not rejected). Start server with `--safe-eval-allowlist "Console.*"`, call with `Console.WriteLine("x")` → passes safety check.

### Tests for US2 (write FIRST — must fail before implementation)

- [x] T016 [P] [US2] Write `tests/DebugMcp.Tests/Unit/SafeEval/SafeEvalAllowlistTests.cs`:
  - `Parse_SimpleMethod_Matches` — `"String.Format"` → `IsAllowed("String", "Format") = true`
  - `Parse_Wildcard_MatchesAnyMethod` — `"Math.*"` → `IsAllowed("Math", "Abs") = true`, `IsAllowed("Math", "Round") = true`
  - `Parse_QualifiedName_StripsNamespace` — `"System.Math.*"` → `IsAllowed("Math", "Abs") = true`
  - `Parse_UnknownMethod_NotAllowed` — `"File.Delete"` NOT in default set → `IsAllowed("File", "Delete") = false`
  - `Default_ContainsAtLeast20Entries` — default allowlist has ≥20 entries
  - `Default_ContainsToString` — `IsAllowed("User", "ToString") = true` and `IsAllowed("Order", "ToString") = true` (any receiver matches — stored as `AllowlistEntry(null, "ToString")`)
  - `Default_ContainsMathAbs` — `IsAllowed("Math", "Abs") = true` from defaults
  - `IsAllowed_CaseSensitive` — `IsAllowed("math", "abs") = false`
- [x] T017 [P] [US2] Extend `tests/DebugMcp.Tests/Unit/SafeEval/SafeExpressionAnalyzerTests.cs` — **allowlisted methods now allowed**: `Math.Abs(delta)` → `IsAllowed = true`, `String.Format("{0}", x)` → `IsAllowed = true`, `Enumerable.Count(list)` → `IsAllowed = true`; **non-allowlisted still blocked**: `Console.WriteLine("x")` → `Category = MethodCall`; **allowlisted with unsafe argument** (edge case from spec): `Math.Abs(list.Add(x))` → `IsAllowed = false`, `Category = MethodCall` (the argument `list.Add(x)` is itself a non-allowlisted call)
- [x] T018 [US2] Run `dotnet test --filter "FullyQualifiedName~SafeEvalAllowlistTests|FullyQualifiedName~SafeExpressionAnalyzerTests"` — confirm new tests RED

### Implementation for US2

- [x] T019 [US2] Create `DebugMcp/Services/SafeEval/SafeEvalAllowlist.cs`:
  - `AllowlistEntry` record: `(string? TypeSimpleName, string MethodName)` — `TypeSimpleName == null` means "any receiver"; `IsWildcard = MethodName == "*"`
  - `ParseEntry(string pattern)` — strips namespace prefixes (last `.`-segment for type name); CLI patterns always produce non-null `TypeSimpleName`
  - `IsAllowed(string receiverSimpleName, string methodName)` — entry matches when `(TypeSimpleName == null || TypeSimpleName == receiverSimpleName) && (IsWildcard || MethodName == methodName)`
  - Default entries (27 minimum): `String.Format, String.Concat, String.IsNullOrEmpty, String.IsNullOrWhiteSpace, String.Join, String.Compare, String.Equals, Math.* (as Math wildcard), Enumerable.Count, Enumerable.Any, Enumerable.First, Enumerable.FirstOrDefault, Enumerable.Last, Enumerable.LastOrDefault, Enumerable.ToList, Enumerable.ToArray, Convert.ToString, Convert.ToInt32, Convert.ToDouble, Convert.ToBoolean, DateTime.ToString, TimeSpan.ToString, Guid.ToString`; plus any-receiver entries: `AllowlistEntry(null, "ToString"), AllowlistEntry(null, "Equals"), AllowlistEntry(null, "GetHashCode")`
  - Constructor: `SafeEvalAllowlist(IEnumerable<string>? additionalPatterns = null)` — merges defaults with extras
- [x] T020 [US2] Run `dotnet test --filter "FullyQualifiedName~SafeEvalAllowlistTests"` — confirm GREEN
- [x] T021 [US2] Inject `SafeEvalAllowlist` into `SafeExpressionAnalyzer`:
  - Add constructor parameter `SafeEvalAllowlist allowlist`
  - In `VisitInvocationExpression`: extract receiver simple name + method name from `InvocationExpressionSyntax`; call `allowlist.IsAllowed(receiver, method)` — if allowed, do NOT reject
  - Helper `ExtractReceiverName(InvocationExpressionSyntax)` → returns last identifier segment of the expression before `(`
- [x] T022 [US2] Update `DebugMcp/Program.cs`:
  - Add `--safe-eval-allowlist` `Option<string?>` — `"Comma-separated method patterns to add to the safe-eval allowlist (e.g. 'Math.*,String.Format')"`
  - Parse value, split by `,`, construct `SafeEvalAllowlist(extraPatterns)`
  - `builder.Services.AddSingleton(allowlist)`
  - Update `SafeExpressionAnalyzer` DI registration to receive it
- [x] T023 [US2] Run `dotnet test --filter "FullyQualifiedName~SafeEvalAllowlistTests|FullyQualifiedName~SafeExpressionAnalyzerTests"` — confirm GREEN
- [x] T024 [US2] Run `dotnet build` — confirm 0 errors

**Checkpoint**: `Math.Abs(delta)` and other allowlisted calls pass through `evaluate_safe`. CLI option extends allowlist. US2 independently testable.

---

## Phase 5: User Story 3 — Descriptive Rejection Feedback (Priority: P3)

**Goal**: When `evaluate_safe` rejects an expression, the response pinpoints the offending sub-expression, names the rejection category, and lists what operations are permitted — so an agent can self-correct without additional tool calls.

**Independent Test**: Submit `repo.Save(entity)` to `evaluate_safe` → response contains `rejection_category: "MethodCall"`, `offending_expression` contains `"Save"`, `allowed_operations` field present.

### Tests for US3 (write FIRST — must fail)

- [x] T025 [US3] Write `tests/DebugMcp.Tests/Unit/SafeEval/EvaluateSafeToolRejectionTests.cs` — mock `ISafeExpressionAnalyzer` to return a `MethodCall` rejection for `repo.Save(entity)`, call the tool handler directly, parse response JSON:
  - `error.code == "safe_eval_rejected"` ✓
  - `error.details.rejection_category == "MethodCall"` ✓
  - `error.details.offending_expression` contains `"Save"` ✓
  - `error.details.allowed_operations` is non-empty string ✓
- [x] T026 [US3] Extend `T025` test file — verify same shape for `ObjectCreation` rejection and `Assignment` rejection categories
- [x] T027 [US3] Run `dotnet test --filter "FullyQualifiedName~EvaluateSafeToolRejectionTests"` — confirm RED

### Implementation for US3

- [x] T028 [US3] Add `CreateRejectionResponse(SafeEvalRejection rejection)` private helper in `DebugMcp/Tools/EvaluateSafeTool.cs`:
  - `success = false`
  - `error.code = "safe_eval_rejected"`
  - `error.message` — human-readable summary
  - `error.details.rejection_category` — `rejection.Category.ToString()` (MethodCall / ObjectCreation / Assignment)
  - `error.details.offending_expression` — `rejection.OffendingExpression`
  - `error.details.allowed_operations` — constant string: `"member reads, property access, arithmetic (+,-,*,/,%), comparisons (==,!=,<,>), logical (&&,||,!), ternary (?:), indexers, null-conditional (?.,?[]), and methods on the safe-eval allowlist"`
- [x] T029 [US3] Update `EvaluateSafeTool.EvaluateAsync` to call `CreateRejectionResponse` when safety check fails (replaces any inline rejection code from T012)
- [x] T030 [US3] Run `dotnet test --filter "FullyQualifiedName~EvaluateSafeToolRejectionTests"` — confirm GREEN
- [x] T031 [US3] Run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"` — confirm 0 failures across all unit + contract tests

**Checkpoint**: All three user stories complete. Rejection response identifies offending expression, category, and permitted operations.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T032 Run release build: `dotnet build -c Release` — confirm 0 errors, 0 warnings in Release configuration
- [x] T033 [P] Run quickstart.md steps 1–8 (manual smoke test): build passes, pure reads allowed, method call blocked, allowlisted method passes, safety-first behavior, CLI option works, annotation verified
- [x] T034 [P] Update `ROADMAP.md` — move feature 029 from Proposed to Completed table with version TBD
- [x] T035 [P] Add performance assertion in `tests/DebugMcp.Tests/Unit/SafeEval/SafeExpressionAnalyzerTests.cs` — `SC003_RejectedExpression_AnalyzedUnder50ms`: warm up once, measure 10 iterations of `Analyze("File.Delete(\"x\")")` via `Stopwatch`, assert average <50ms (validates SC-003: static analysis only, no process interaction)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Foundational only — first MVP deliverable
- **US2 (Phase 4)**: Depends on US1 (SafeExpressionAnalyzer exists to inject SafeEvalAllowlist into)
- **US3 (Phase 5)**: Depends on US1 (EvaluateSafeTool exists to add CreateRejectionResponse to)
- **Polish (Phase 6)**: Depends on all stories complete

### Within Each User Story

Tests MUST be written and FAIL before implementation (RED → GREEN → REFACTOR per constitution).  
Models before services, services before tool, tool before DI registration.

### Parallel Opportunities

- T006 is independent; T007 and T008 MUST follow T006 sequentially (they extend the same file)
- T016, T017 can run in parallel (different test classes)
- T032, T033, T034, T035 can run in parallel (different targets, no blocking deps)

---

## Parallel Example: User Story 1

```bash
# T006 first (creates the file), then T007 and T008 sequentially (extend the same file):
# Sequential: T006 → T007 → T008
# Reason: all three modify SafeExpressionAnalyzerTests.cs — only T006 starts fresh
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational (T003–T005) — CRITICAL
3. Complete Phase 3: User Story 1 (T006–T015)
4. **STOP and VALIDATE**: `evaluate_safe` tool blocks destructive expressions
5. Ship if needed — US2/US3 are enhancements

### Incremental Delivery

1. Setup + Foundational → core types defined
2. US1 → `evaluate_safe` blocks ALL method calls (overly strict but safe)
3. US2 → default allowlist + CLI extension unlocks known-pure methods
4. US3 → rejection responses pinpoint the offending sub-expression

### Total Tasks

| Phase | Tasks | Notes |
|-------|-------|-------|
| Setup | 2 | Directory scaffolding |
| Foundational | 3 | Core models + interface |
| US1 (P1) | 10 | Tests (4) + impl (6) |
| US2 (P2) | 9 | Tests (3) + impl (6) |
| US3 (P3) | 7 | Tests (3) + impl (4) |
| Polish | 4 | Release build + smoke + roadmap + perf assertion |
| **Total** | **35** | |

---

## Notes

- [P] tasks = different files, no blocking dependencies between them
- TDD is mandatory (constitution §III) — never skip the RED phase
- `CSharpSyntaxTree` is already available via `Microsoft.CodeAnalysis.CSharp.Workspaces` — no new packages
- `SafeExpressionAnalyzer` is stateless — constructed once, thread-safe, no locks needed
- `SafeEvalAllowlist` is immutable after construction — singleton in DI is safe
- Commit after each GREEN phase checkpoint
