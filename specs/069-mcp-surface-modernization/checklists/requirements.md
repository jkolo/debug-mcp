# Specification Quality Checklist: MCP Surface Modernization

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-25
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Validation Notes

**Authoring decisions taken to satisfy the checklist** (applied while writing, not as a later
revision pass — the spec was written once and validated against the criteria below):

1. *Keeping SDK surface out of the requirements* — the source material for this spec named
   concrete SDK types and wire method names. These were deliberately excluded from the
   functional requirements in favour of capability language: "deferred-result handle", "enquire
   about a handle", "publish a result schema". The concrete API surface is already researched
   and verified, and belongs in `plan.md`.

2. *Numeric thresholds instead of adjectives* — success criteria state SC-001 (first update
   within 5s, further at least every 30s), SC-006 (≥50% token reduction), SC-007 (≥8 of 10
   recorded scenarios rank the fault site first) and SC-008 (10 replays byte-identical), so that
   each can be failed by a test rather than argued about.

3. *Closing the qualifying set* — "long operation" would otherwise be left to implementer
   judgement. FR-013 states the threshold (5 seconds worst case) **and** enumerates the five
   qualifying tools, making the set closed and testable.

4. *Backward compatibility stated as requirements, not assumed* — FR-017 (text rendering
   retained), FR-021 (field names and semantics preserved) and SC-009 (a client supporting none
   of the new capabilities sees no change).

**Deliberate scope exclusions**, recorded so they are not re-litigated during planning:

- Model-backed enrichment is **rejected on evidence**, not deferred — MCP Sampling is deprecated
  by SEP-2577 (Final) and flagged there as the most security-sensitive of the deprecated
  features. Reopening requires a new decision.
- Pagination and bulk APIs stay with ROADMAP #036.
- The 4 prompts and 7 resources are untouched.

**Note on scope size**: this specification spans five independently shippable slices (P1–P5; it was four until the third pass brought timeouts into scope).
The user explicitly directed that the result-contract migration (Story 3, 39 tools) be included
here rather than split into a follow-up spec. Planning should preserve the slice boundaries so
each can ship on its own.

## Planning-Readiness Audit (2026-08-25, second pass)

A separate pass was run against the repository to check that every requirement is actually
implementable and every success criterion actually evaluable. It found three gaps; all three are
now fixed in the spec.

| # | Gap found | Resolution |
|---|---|---|
| 1 | **FR-020 assumed a machine-readable tool catalogue that does not exist.** Tool documentation lives in `website/docs/tools/*.md` — 10 thematic prose files, not one entry per tool — and no doc-sync test exists today. As originally worded, the check would have required deriving result shapes from prose. | FR-020 rewritten to enumerate three concrete divergences and to verify documentation coverage **by tool name only**. Recorded as an assumption. |
| 2 | **The constitution requires an optional timeout on all blocking operations; only ~13 of 40 tool files have one.** A pre-existing violation the plan's Constitution Check gate would hit, with nothing in the spec saying whether this feature closes it. | *Superseded — see the third pass below.* Initially declared out of scope as a knowingly carried deviation; that resolution did not survive `/speckit-analyze`. |
| 3 | **SC-006, SC-007 and SC-008 all depend on a corpus of recorded faulting scenarios that does not exist.** Three success criteria were unevaluable and the work to make them evaluable was invisible. | New **FR-030** requires building the corpus (≥10 scenarios) and names the five fault classes it must cover. |

**Verified as already in place** (planning may rely on these):

- `ErrorCodes` exists as a single static set (50 constants at the time of writing; US5 may add a timeout code) in `DebugMcp/Models/ErrorResponse.cs`.
  FR-018/FR-019 build on it rather than introducing a new registry.
- The project has an established pattern for testing MCP notifications despite
  `IMcpServer.SendNotificationAsync` being an un-mockable extension method: wrap it in a
  first-party interface and supply a test double (`IBreakpointNotifier` /
  `NullBreakpointNotifier`, plus `McpResourceNotifier`). Progress and deferred-result
  notifications should follow this precedent; planning need not re-derive it.
- `ToolAnnotationTests` already enumerates every tool by name and asserts its annotations, giving
  FR-020's coverage check an existing place to live.

## Cross-Artifact Analysis (2026-08-25, third pass)

`/speckit-analyze` ran against spec, plan and tasks together and produced 10 findings — one
CRITICAL, two HIGH, five MEDIUM, two LOW. All were resolved.

**The CRITICAL one reversed an earlier decision.** The second pass had accepted the timeout gap as
a documented, knowingly carried deviation. The analysis rules do not permit that: a constitution
MUST can be satisfied or amended through a separate constitution change, but not deferred by a
plan. The user chose to bring it into scope, and it is now **User Story 5 (P5)** with FR-031–FR-034
and SC-011/SC-012. The original input-versus-output reasoning survives — but only as the reason
US5 is a separate, last slice, not as a reason to leave the requirement unmet.

| Finding | Severity | Resolution |
|---|---|---|
| Timeout requirement deferred rather than satisfied | CRITICAL | Brought into scope as US5; plan's Constitution Check now records no carried Principle II deviation |
| FR-022 (no model, no credential) had **zero** task coverage | HIGH | New **T088** contract test; new **SC-013**. This was the feature's defining decision with nothing to detect its violation — negative requirements have no natural implementation task, which is exactly how they slip |
| FR-017 had a test (T042) but no implementation owner | HIGH | **T053** now owns guaranteeing the text block, rather than relying on unverified SDK default behaviour |
| "Practical message size" undefined | MEDIUM | New **FR-035**: 256 KB default budget, configurable, with the 14 affected tools enumerated |
| FR-009 handle defaults (`ttlMs`, `pollIntervalMs`) unassigned | MEDIUM | **T031** now sets and justifies them |
| SC-003 unverifiable as written | MEDIUM | Restated as a measurement — longest single exchange per qualifying operation — and recorded during quickstart Scenario 2 |
| Principle IV indirection deviation | MEDIUM | Confirmed as properly handled through Complexity Tracking, the constitution's own sanctioned mechanism |
| One concept, three names (receipt / handle / task) | MEDIUM | Unified on **handle** across all artifacts; `taskId` retained only as the protocol's wire field name |
| `Services/Tasks/`, `Services/Timeouts/`, `docs/enrichment-heuristics.md` absent from the plan's structure tree | LOW | Added |
| Four tasks without file paths; migration series inconsistent | LOW | Paths added; T045–T052 aligned with T044 |

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`
- All items pass as of 2026-08-25; no blocking clarifications outstanding
- **35 functional requirements, 13 success criteria, 5 independently shippable stories, 90 tasks**
