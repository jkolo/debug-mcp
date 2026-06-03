# Research: Safe Evaluation Mode (029)

## 1. Expression Parsing Strategy

**Decision**: Use `CSharpSyntaxTree.ParseText` with `SourceCodeKind.Script` — no compilation or semantic model needed.

**Rationale**: Syntactic analysis of `InvocationExpressionSyntax` and `ObjectCreationExpressionSyntax` nodes is sufficient to detect method calls before execution. Adding a semantic model (requiring compilation against loaded assemblies) would add hundreds of milliseconds and considerable complexity — unnecessary for a safety gate whose job is to say "this LOOKS like a method call."

**Alternative rejected**: Semantic analysis (SemanticModel + SymbolInfo) would let us resolve fully qualified type names, making allowlist matching exact. Rejected because: (1) no loaded compilation to compile against at tool-invocation time, (2) >100ms overhead violates SC-003 (<50ms), (3) spec explicitly says the feature is a static analysis gate, not a semantic sandbox.

**Wrap expression for valid parse**: `CSharpSyntaxTree.ParseText($"_ = {expression};", CSharpParseOptions.Default.WithKind(SourceCodeKind.Script))` — wrapping as assignment ensures the expression parses without errors from the parser expecting a statement.

## 2. Allowlist Matching Without Semantic Model

**Decision**: Unqualified suffix matching — allowlist entries are normalized to `SimpleTypeName.MethodName` (or `SimpleTypeName.*`). The receiver in an invocation is extracted as the last identifier segment.

**Example**: `System.Math.*` → normalized to `Math.*`. Invocation `Math.Abs(x)` → receiver `Math`, method `Abs` → matches `Math.*`. ✓

**Known limitation**: Cannot distinguish `Math` in namespace A from `Math` in namespace B — accepted per spec (Assumptions: "purely syntactic"). The user is responsible for scoping allowlist entries appropriately.

**Any-receiver entries**: The default built-in set includes `ToString`, `Equals`, `GetHashCode` stored with `TypeSimpleName = null` — these match any receiver. This means `user.ToString()` (AST receiver = `"user"`) correctly resolves to allowed, because the entry does not require a specific type name. CLI-supplied patterns always produce type-qualified entries.

**Alternative rejected**: Full namespace resolution via semantic model (see §1 above).

## 3. What Gets Blocked vs. Allowed by Default

**Allowed** (require no allowlist entry):
- `IdentifierNameSyntax` standalone reads — `x`, `user`
- `MemberAccessExpressionSyntax` without invocation — `user.Name`, `list.Count` (property)
- `ElementAccessExpressionSyntax` — `arr[0]`, `dict["key"]`
- Binary/unary/ternary operators — `a + b`, `x > 0 ? y : z`
- Literal expressions — `42`, `"hello"`, `true`, `null`
- Conditional access — `user?.Name`, `list?[0]`

**Blocked** (require allowlist):
- `InvocationExpressionSyntax` — `method()`, `obj.Method()`, `Type.Method()`
- `ObjectCreationExpressionSyntax` — `new Foo()`, `new List<int>()`
- Assignment expressions (`=`, `+=`, ...) — `x = 5` is a write, always blocked (not in the safe-expression set regardless of allowlist)

**Note on assignments**: Assignment nodes (`AssignmentExpressionSyntax`) are blocked unconditionally — the safe eval tool is read-only by design and no allowlist entry can unlock writes.

## 4. Default Allowlist Size (FR-005 compliance)

Verified 20+ entries will be in default set:
```
String.Format, String.Concat, String.IsNullOrEmpty, String.IsNullOrWhiteSpace,
String.Join, String.Compare, String.Equals,
Math.Abs, Math.Ceiling, Math.Floor, Math.Round, Math.Max, Math.Min, Math.Pow, Math.Sqrt, Math.Log,
Enumerable.Count, Enumerable.Any, Enumerable.First, Enumerable.FirstOrDefault,
Enumerable.Last, Enumerable.LastOrDefault, Enumerable.ToList, Enumerable.ToArray,
Object.ToString, Object.Equals, Object.GetHashCode
```
That's 27 entries — exceeds FR-005's minimum of 20. Aliases like `Math.*` cover all `Math.*` members with one entry.

## 5. MCP Tool Name

**Decision**: `evaluate_safe` — follows `noun_verb` convention from constitution (`evaluate` noun, `safe` qualifier acting as modifier).

**Alternative considered**: `evaluate_pure` — name in the roadmap. Rejected for naming: `pure` is a functional-programming term not universally known; `safe` is more self-explanatory in context.

## 6. CLI Configuration Format

**Decision**: `--safe-eval-allowlist "Math.*,String.Format,MyLib.Helper.*"` — comma-separated patterns, appended to the default set (not replacing it).

**Rationale**: Consistent with `--symbol-servers` (semicolon) and similar flags. Comma is more intuitive for short method patterns.

**Alternative rejected**: JSON config file — added complexity; no other feature uses a config file.

## 7. Service Architecture

**Decision**: New `ISafeExpressionAnalyzer` + `SafeExpressionAnalyzer` service, injected into `EvaluateSafeTool`.

**Rationale**: Keeps the tool thin (delegates analysis to service, mirrors existing `EvaluateTool` → `IDebugSessionManager` pattern). Also makes the analyzer unit-testable without MCP infrastructure.

**Alternative rejected**: Inline analysis logic in the tool class — violates Simplicity principle (each tool does one thing; analysis is a non-trivial concern).

## 8. Existing Infrastructure Reuse

- `Microsoft.CodeAnalysis.CSharp.Workspaces` already referenced — no new NuGet packages needed.
- `CSharpSyntaxWalker` from `Microsoft.CodeAnalysis.CSharp` — already in transitive deps.
- `EvaluateTool` pattern for response shape, error codes, logging — copy-and-adapt.
- `Program.cs` Option + DI registration pattern — consistent with `--no-roslyn`, `--symbol-servers`.
