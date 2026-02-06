# Tasks: Documentation & Site Update

**Input**: Design documents from `/specs/023-docs-site-update/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

**Tests**: Not applicable — documentation-only feature. Validation via `npm run build`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Verify existing site builds and understand current structure

- [ ] T001 Verify Docusaurus site builds cleanly by running `cd website && npm ci && npm run build`
- [ ] T002 Read existing tool pages to understand style conventions: `website/docs/tools/session.md`, `website/docs/tools/breakpoints.md`, `website/docs/tools/inspection.md`

---

## Phase 2: User Story 1 — Complete tool reference for new features (Priority: P1) 🎯 MVP

**Goal**: Document all 34 MCP tools on the site. Currently 3 are missing: `exception_get_context`, `process_write_input`, `process_read_output`.

**Independent Test**: Run `npm run build` successfully. Open each tool page and verify all 34 tools are present with parameters, examples, and descriptions.

### Implementation for User Story 1

- [ ] T003 [P] [US1] Add `exception_get_context` section to `website/docs/tools/breakpoints.md` — include parameters table (max_frames, include_variables_for_frames, max_inner_exceptions), example JSON request/response showing autopsy output with exception details, inner exceptions, stack frames, and variables
- [ ] T004 [P] [US1] Create `website/docs/tools/process-io.md` — new page documenting `process_write_input` (parameters: data, close_after) and `process_read_output` (parameters: stream, clear) with example JSON request/response for each tool
- [ ] T005 [US1] Add Process I/O entry to `website/sidebars.ts` — insert `tools/process-io` item in the Tools category after Code Analysis
- [ ] T006 [US1] Verify site builds with `cd website && npm run build`

**Checkpoint**: All 34 tools documented. Site builds. MVP complete.

---

## Phase 3: User Story 2 — Exception debugging workflow (Priority: P2)

**Goal**: Add a workflow guide that walks through exception breakpoints + autopsy end-to-end.

**Independent Test**: Navigate to the new workflow page and verify it contains a complete step-by-step flow with example tool calls and responses.

### Implementation for User Story 2

- [ ] T007 [US2] Create `website/docs/workflows/debug-exceptions.md` — step-by-step workflow: (1) launch app with `debug_launch`, (2) set exception breakpoint with `breakpoint_set_exception`, (3) continue execution, (4) wait for hit with `breakpoint_wait` + `include_autopsy: true`, (5) deep dive with `exception_get_context`, (6) inspect variables in throwing frame, (7) navigate to root cause. Include example JSON for each step.
- [ ] T008 [US2] Add Debug Exceptions entry to `website/sidebars.ts` — insert `workflows/debug-exceptions` in the Workflows category after Analyze Codebase
- [ ] T009 [US2] Verify site builds with `cd website && npm run build`

**Checkpoint**: Exception debugging workflow available. Site builds.

---

## Phase 4: User Story 3 — MCP Resources documentation (Priority: P2)

**Goal**: Document all 4 MCP resources with URI templates, descriptions, and example responses.

**Independent Test**: Navigate to the resources page and verify all 4 resources are documented with example JSON responses.

### Implementation for User Story 3

- [ ] T010 [US3] Create `website/docs/resources.md` — document MCP resources concept (subscribable state vs request-response tools), then document each resource: `debugger://session` (session state, process info), `debugger://breakpoints` (active breakpoints with hit counts), `debugger://threads` (thread list with states), `debugger://source/{+file}` (source file contents). Include example JSON response for each resource.
- [ ] T011 [US3] Add Resources entry to `website/sidebars.ts` — insert `resources` as top-level item after the Tools category and before Workflows
- [ ] T012 [US3] Verify site builds with `cd website && npm run build`

**Checkpoint**: All 4 MCP resources documented. Site builds.

---

## Phase 5: User Story 4 — Consolidate documentation sources (Priority: P3)

**Goal**: Single source of truth for documentation. Remove `/docs/` duplication, update README links to live site.

**Independent Test**: Verify `/docs/` contains only a redirect stub. Verify README documentation links point to `https://debug-mcp.net/docs/...`. Verify site builds.

### Implementation for User Story 4

- [ ] T013 [P] [US4] Remove legacy docs files: `docs/ARCHITECTURE.md`, `docs/DEBUGGER.md`, `docs/DEVELOPMENT.md`, `docs/MCP_TOOLS.md`. Create `docs/README.md` with message redirecting to https://debug-mcp.net
- [ ] T014 [P] [US4] Update `README.md` documentation links — replace local file references (`docs/ARCHITECTURE.md`, etc.) with live site URLs (`https://debug-mcp.net/docs/architecture`, `https://debug-mcp.net/docs/debugger`, `https://debug-mcp.net/docs/tools/session`, `https://debug-mcp.net/docs/development`)
- [ ] T015 [US4] Verify site builds with `cd website && npm run build` and verify no broken links in README

**Checkpoint**: Single documentation source. README links to live site. Site builds.

---

## Phase 6: Polish & Validation

**Purpose**: Final validation and cross-cutting concerns

- [ ] T016 Full site build and link validation with `cd website && npm run build`
- [ ] T017 Run quickstart.md verification steps
- [ ] T018 Preview site locally with `cd website && npm run start` and spot-check all new/modified pages

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **User Story 1 (Phase 2)**: Depends on Setup — MVP deliverable
- **User Story 2 (Phase 3)**: No dependency on US1 — can run in parallel
- **User Story 3 (Phase 4)**: No dependency on US1/US2 — can run in parallel
- **User Story 4 (Phase 5)**: No dependency on US1/US2/US3 — can run in parallel
- **Polish (Phase 6)**: Depends on all user stories complete

### Parallel Opportunities

- T003 and T004 can run in parallel (different files)
- T013 and T014 can run in parallel (different files)
- User Stories 1–4 are fully independent and can all run in parallel after Setup
- Within US1: T003 and T004 are parallel; T005 depends on T004 (sidebar needs page to exist)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: User Story 1 (T003–T006)
3. **STOP and VALIDATE**: All 34 tools documented, site builds
4. Deploy if ready — immediate value

### Incremental Delivery

1. US1 → All tools documented → Deploy (MVP!)
2. US2 → Exception workflow added → Deploy
3. US3 → Resources documented → Deploy
4. US4 → Docs consolidated → Deploy
5. Each story adds value without breaking previous stories

---

## Notes

- All pages must follow existing style: `## tool_name` heading → description → Parameters table → Example with JSON
- Parameters table columns: Name, Type, Required, Default, Description
- Site uses strict mode (`onBrokenLinks: 'throw'`) — broken links will fail the build
- Commit after each user story checkpoint
