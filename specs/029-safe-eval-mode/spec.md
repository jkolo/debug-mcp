# Feature Specification: Safe Evaluation Mode

**Feature Branch**: `029-safe-eval-mode`
**Created**: 2026-06-03
**Status**: Draft

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Block Destructive Expressions (Priority: P1)

An AI agent autonomously debugging a production-like process invokes the safe evaluation tool with an expression. The tool accepts member reads, arithmetic, and comparisons — and rejects any call that could mutate state or trigger side effects in the debugged process (e.g., writing to a file, dropping a database table, sending a network request).

**Why this priority**: Core safety contract. Without this, autonomous agents cannot be trusted to evaluate expressions on sensitive processes. This is the MVP that must work before anything else.

**Independent Test**: Can be fully tested by submitting expressions of each category (pure vs. side-effecting) and verifying correct allow/reject decisions. Delivers a fully usable safe-eval guardrail with no other stories required.

**Acceptance Scenarios**:

1. **Given** a debugged process paused at a breakpoint, **When** an agent evaluates `user.Name` (member read), **Then** the value is returned successfully.
2. **Given** a debugged process paused, **When** an agent evaluates `list.Count * 2` (property access + arithmetic), **Then** the result is returned successfully.
3. **Given** a debugged process paused, **When** an agent evaluates `File.Delete("data.db")` (method call with side effects), **Then** the evaluation is rejected with a clear error explaining why.
4. **Given** a debugged process paused, **When** an agent evaluates `dbContext.Database.EnsureDeleted()` (method call), **Then** the evaluation is rejected before execution in the target process.
5. **Given** a debugged process paused, **When** an agent evaluates `a + b > 0` (arithmetic + comparison), **Then** the result is returned successfully.

---

### User Story 2 - Allowlist Known-Pure Methods (Priority: P2)

An operator configuring the MCP server (or an agent with access to configuration) extends the safe evaluation rules by marking specific methods as known-pure (e.g., `string.Format`, `Math.Abs`, `Enumerable.Count`, `ToString` overrides). Once allowlisted, those methods are permitted in safe-eval expressions.

**Why this priority**: Without an allowlist, safe mode is too restrictive for real-world use — many pure utility methods are indistinguishable from side-effecting ones by signature alone. Allowlist unlocks practical usefulness while preserving the safety contract.

**Independent Test**: Can be tested by configuring an allowlist entry for a specific method, then evaluating an expression that uses it — verifying it passes where it previously would have been rejected.

**Acceptance Scenarios**:

1. **Given** `ToString()` is on the allowlist, **When** an agent evaluates `order.Status.ToString()`, **Then** the result is returned successfully.
2. **Given** `Math.Abs` is on the allowlist, **When** an agent evaluates `Math.Abs(delta)`, **Then** the result is returned.
3. **Given** a method is NOT on the allowlist and is not recognized as intrinsically pure, **When** an agent evaluates an expression calling it, **Then** it is rejected even if the method happens to be side-effect-free in practice.
4. **Given** `Console.WriteLine` is NOT on the allowlist, **When** an agent evaluates `Console.WriteLine("x")`, **Then** it is rejected (the allowlist does not permit every method by default).

---

### User Story 3 - Descriptive Rejection Feedback (Priority: P3)

When an expression is rejected by the safe evaluation tool, the agent receives a structured error that identifies the specific operation that caused the rejection and explains what categories of operations are permitted.

**Why this priority**: Without clear rejection messages, agents cannot self-correct — they will either retry blindly or escalate unnecessarily. Good feedback closes the autonomous debugging loop.

**Independent Test**: Can be tested by submitting a known-bad expression and verifying the response includes the offending sub-expression and the rejection reason.

**Acceptance Scenarios**:

1. **Given** an agent submits `repo.Save(entity)`, **When** the expression is rejected, **Then** the response names `Save` as the problematic call and states that method invocations are not permitted in safe mode.
2. **Given** an agent submits a valid expression, **When** it is accepted, **Then** no rejection metadata is included in the response.
3. **Given** an expression contains multiple violations, **When** rejected, **Then** the first violation encountered is reported (fail-fast).

---

### Edge Cases

- What happens when an expression contains nested method calls (e.g., `obj.GetList().Count`)? `GetList()` is a method call — the whole expression must be rejected because the call cannot be skipped even if `.Count` is safe.
- What happens when an allowlisted method is called with arguments that are themselves method calls? The argument sub-expressions must also pass the safety check.
- How does the system handle expressions that reference variables not in scope? Same error as the existing `evaluate` tool — out-of-scope variables are a separate concern from safety.
- What if the allowlist contains a method pattern that does not match any loaded type? The entry is silently unused; no error at startup or evaluation time.
- What happens when safe-eval is invoked when no debugger session is active? Returns the same "no active session" error as the existing `evaluate` tool.
- What if the expression is syntactically invalid? Returns a parse error before safety analysis runs.
- What if an expression only contains literals (no variable references)? Permitted — literals carry no side effects.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a dedicated safe-evaluation tool (distinct from the existing general `evaluate` tool) that accepts an expression string and returns a value or a structured rejection.
- **FR-002**: The safe-evaluation tool MUST permit expressions composed only of: local variable reads, field and property reads, indexer reads, arithmetic operators (+, -, *, /, %), comparison operators (==, !=, <, >, <=, >=), logical operators (&&, ||, !), ternary operator (?:), numeric and string literals, and null-conditional access (?., ?[]). All other expression constructs — including object construction (`new T(...)`) and assignment operators — are rejected by the safety check.
- **FR-003**: The safe-evaluation tool MUST reject any expression that contains a method invocation unless the method matches an entry in the configured allowlist.
- **FR-004**: Rejection MUST occur before the expression is executed in the debugged process — via static analysis of the expression tree, not runtime interception.
- **FR-005**: The allowlist MUST ship with a built-in default set of widely-used pure methods including at minimum: `ToString`, `GetHashCode`, `Equals`, `String.Format`, `String.Concat`, `String.IsNullOrEmpty`, `String.IsNullOrWhiteSpace`, all members of `System.Math`, `Enumerable.Count`, `Enumerable.Any`, `Enumerable.First`, `Enumerable.FirstOrDefault`, `Enumerable.Last`, `Enumerable.LastOrDefault`.
- **FR-006**: The allowlist MUST be extensible at MCP server startup via a CLI argument without code changes.
- **FR-007**: The allowlist MUST support wildcard matching at the type level (e.g., `System.Math.*` permits all members of `Math`).
- **FR-008**: When an expression is rejected, the response MUST include: the rejection reason category, the specific sub-expression that triggered rejection, and a description of what operations are permitted in safe mode.
- **FR-009**: The safe-evaluation tool MUST support the same expression syntax and variable scope as the existing `evaluate` tool for expressions that pass the safety check.
- **FR-010**: The safe-evaluation tool MUST be annotated as read-only and non-destructive in its MCP tool metadata.

### Key Entities

- **Safe Expression**: An expression string that, after static analysis, contains no non-allowlisted method invocations. Represents the set of expressions permitted by the safety contract.
- **Allowlist Entry**: A type-qualified method or member pattern (e.g., `System.Math.*`, `System.String.Format`) that is explicitly permitted in safe expressions alongside the built-in pure set.
- **Rejection Result**: A structured response returned when an expression fails the safety check, containing the offending sub-expression and the rejection reason category.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of method invocations not on the allowlist are blocked before execution in the debugged process — zero false negatives in the safety check.
- **SC-002**: 100% of purely read-access expressions (member reads, arithmetic, comparisons) are permitted — zero false positives blocking valid safe expressions.
- **SC-003**: Rejected expressions receive a response in under 50ms (static analysis only — no interaction with the debugged process required for rejected expressions).
- **SC-004**: The default allowlist covers at least 20 commonly used pure methods so that agents can perform meaningful introspection without any configuration.
- **SC-005**: Agents can identify the specific problematic sub-expression and understand what to change from the rejection response alone, without additional tool calls.

## Assumptions

- The primary consumer is an AI agent operating autonomously, not a human developer typing expressions interactively.
- The existing `evaluate` tool remains unchanged and available for use cases where side effects are intentional and authorized by the user.
- The safety check is static (expression AST analysis), not dynamic (sandboxed execution) — faster and simpler, but means an allowlisted method with actual side effects is the operator's responsibility to vet.
- Expression parsing reuses the Roslyn-based infrastructure already present in the project.
- The allowlist applies globally per server session, not per debug session or per individual tool call — per-call allowlist overrides are out of scope for this feature.
- Property getters are treated as safe by default; the spec assumes that property getters in user code are not generally side-effecting (a standard C# convention). This assumption may be wrong for poorly designed code, which is an accepted limitation.
