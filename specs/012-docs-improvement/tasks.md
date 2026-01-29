# Tasks: Documentation Improvement

**Input**: Design documents from `/specs/012-docs-improvement/`
**Prerequisites**: plan.md, spec.md, research.md, quickstart.md

**Tests**: Not applicable — documentation feature. Validation via `npm run build`.

**Organization**: Tasks grouped by user story for independent implementation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1, US2, US3)
- Exact file paths included

---

## Phase 1: Setup

**Purpose**: Configure Docusaurus plugins and shared infrastructure

- [x] T001 Install `@docusaurus/theme-mermaid` dependency in `website/package.json`
- [x] T002 Install `asciinema-player` dependency in `website/package.json`
- [x] T003 Enable Mermaid in `website/docusaurus.config.ts`: add `markdown: { mermaid: true }`, add `'@docusaurus/theme-mermaid'` to top-level `themes` array, configure `themeConfig.mermaid.theme` with light/dark variants
- [x] T004 Create `website/src/components/AsciinemaPlayer.tsx` — React wrapper using `BrowserOnly`, `asciinema-player` create() API, CSS import, props: src, rows, cols, idleTimeLimit, speed, autoPlay, theme, fit, poster
- [x] T005 Create `website/static/casts/` directory with `.gitkeep`
- [x] T006 Update `website/sidebars.ts` — add `tools/` category (Session, Breakpoints, Execution, Inspection, Memory, Modules), `workflows/` category, Getting Started as first item
- [x] T007 Verify setup: `cd website && npm run build` succeeds with Mermaid plugin active

**Checkpoint**: Infrastructure ready — all three user stories can now proceed.

---

## Phase 2: User Story 1 — Rich Feature Descriptions (Priority: P1) 🎯 MVP

**Goal**: Every MCP tool has comprehensive documentation split into 6 category pages, plus 3 workflow guides and a Getting Started guide.

**Independent Test**: Visit each tool category page and verify: purpose description, "when to use", parameter table, example request/response, real-world use case. Visit workflow pages and verify multi-tool step-by-step guides. Visit Getting Started and follow it end-to-end.

### Tool Category Pages

- [x] T008 [P] [US1] Create `website/docs/tools/session.md` — document debug_launch, debug_attach, debug_disconnect, debug_state (4 tools: purpose, when to use, params table, example req/resp, use case)
- [x] T009 [P] [US1] Create `website/docs/tools/breakpoints.md` — document breakpoint_set, breakpoint_remove, breakpoint_list, breakpoint_enable, breakpoint_set_exception, breakpoint_wait (6 tools)
- [x] T010 [P] [US1] Create `website/docs/tools/execution.md` — document debug_continue, debug_pause, debug_step (3 tools)
- [x] T011 [P] [US1] Create `website/docs/tools/inspection.md` — document stacktrace_get, threads_list, variables_get, evaluate, object_inspect (5 tools)
- [x] T012 [P] [US1] Create `website/docs/tools/memory.md` — document memory_read, layout_get, references_get (3 tools)
- [x] T013 [P] [US1] Create `website/docs/tools/modules.md` — document modules_list, modules_search, types_get, members_get (4 tools)

### Workflow Guides

- [x] T014 [P] [US1] Create `website/docs/workflows/debug-a-crash.md` — multi-tool guide: launch → breakpoint_set_exception → continue → stacktrace_get → variables_get → evaluate. Target audience: developer configuring debug-mcp for AI agent.
- [x] T015 [P] [US1] Create `website/docs/workflows/inspect-memory-layout.md` — multi-tool guide: attach → breakpoint_set → continue → object_inspect → layout_get → memory_read → references_get.
- [x] T016 [P] [US1] Create `website/docs/workflows/profile-module-loading.md` — multi-tool guide: launch → modules_list → modules_search → types_get → members_get.

### Getting Started

- [x] T017 [US1] Create `website/docs/getting-started.md` — end-to-end guide: install via dnx/dotnet tool, configure in Claude Desktop/Claude Code, first debugging session (launch → set breakpoint → continue → inspect variables → disconnect). sidebar_position: 1.

### Cleanup

- [x] T018 [US1] Remove old `website/docs/mcp-tools.md` (replaced by tools/ category pages) and update any internal links
- [x] T019 [US1] Verify build: `cd website && npm run build` — all new pages render, no broken links

**Checkpoint**: US1 complete — all 25 tools documented across 6 pages, 3 workflow guides, Getting Started guide. Site builds clean.

---

## Phase 3: User Story 2 — Asciinema Recordings (Priority: P2)

**Goal**: At least 4 embedded asciinema recordings showing real debugging sessions via CLI wrapper.

**Independent Test**: Visit pages with recordings, verify players load, play, show realistic sessions, text is copyable.

### Prepare Sample App

- [x] T020 [US2] Create sample .NET app for recordings in `website/samples/BuggyApp/` — multi-class console app with: a NullReferenceException bug in a service class, a logic error in a calculator method, multiple types with fields for inspection. Must build and run with `dotnet run`.

### Write Recording Scenarios

- [x] T021 [P] [US2] Write recording scenario `website/static/casts/README.md#getting-started` — step-by-step script: commands to type, expected output, timing notes. Covers: install debug-mcp, configure, launch BuggyApp, set breakpoint, hit it, inspect variable, disconnect.
- [x] T022 [P] [US2] Write recording scenario `website/static/casts/README.md#breakpoint-workflow` — script: set breakpoint by file/line, set exception breakpoint, list breakpoints, enable/disable, wait for hit, inspect state.
- [x] T023 [P] [US2] Write recording scenario `website/static/casts/README.md#variable-inspection` — script: hit breakpoint, get stacktrace, list threads, inspect variables at different scopes, evaluate expressions, inspect object.
- [x] T024 [P] [US2] Write recording scenario `website/static/casts/README.md#full-debug-session` — script: launch BuggyApp, set breakpoints, step through code, inspect memory layout, browse modules, disconnect.

### Record Sessions (MANUAL — user records following scenarios)

- [x] T025 ⚠️ MANUAL [US2] Record `website/static/casts/getting-started.cast` following scenario from T021. Use `asciinema rec --idle-time-limit 2`.
- [x] T026 ⚠️ MANUAL [US2] Record `website/static/casts/breakpoint-workflow.cast` following scenario from T022.
- [ ] T027 ⚠️ MANUAL [US2] Record `website/static/casts/variable-inspection.cast` following scenario from T023. *(blocked — requires debug-mcp bug fixes)*
- [ ] T028 ⚠️ MANUAL [US2] Record `website/static/casts/full-debug-session.cast` following scenario from T024. *(blocked — requires debug-mcp bug fixes)*

### Embed Recordings

- [x] T029 [US2] Embed asciinema player in `website/docs/getting-started.md` — show getting-started.cast recording
- [x] T030 [P] [US2] Embed asciinema player in `website/docs/tools/breakpoints.md` — show breakpoint-workflow.cast
- [ ] T031 [P] [US2] Embed asciinema player in `website/docs/tools/inspection.md` — show variable-inspection.cast *(blocked — T027 not recorded)*
- [x] T032 [US2] Embed asciinema player on landing page `website/src/pages/index.tsx` — show setup-mcp.cast (replaced terminal widget)
- [x] T033 [US2] Verify build and player behavior: `cd website && npm run build` — all players embedded, cast files served. Verify `poster` prop renders preview frame before playback, player loads lazily (non-blocking)

**Checkpoint**: US2 complete — 4 recordings embedded across Getting Started, tool pages, and landing page.

---

## Phase 4: User Story 3 — Rendered Diagrams (Priority: P3)

**Goal**: All ASCII art diagrams replaced with Mermaid, rendering in both themes.

**Independent Test**: View Architecture and Debugger pages — diagrams are SVG graphics, adapt to dark/light mode, readable on mobile.

- [x] T034 [US3] Replace ASCII art architecture diagram in `website/docs/architecture.md` with Mermaid `graph TD` diagram showing: LLM Agent → MCP Protocol → DebugMcp Server (MCP Layer with tool groups, Debugger Core with components) → ICorDebug → .NET Runtime
- [x] T035 [US3] Replace any remaining ASCII art diagrams in `website/docs/architecture.md` with Mermaid equivalents (debug event flow, session lifecycle)
- [x] T036 [US3] Replace ASCII art diagrams in `website/docs/debugger.md` with Mermaid equivalents (state machine, event handling flow)
- [x] T037 [US3] Verify dark/light mode: `npm start` → toggle theme → all diagrams legible in both modes
- [x] T038 [US3] Verify mobile: resize browser to < 768px → all diagrams readable without horizontal scroll
- [x] T039 [US3] Verify build: `cd website && npm run build` — all Mermaid blocks render, no syntax errors

**Checkpoint**: US3 complete — zero ASCII art blocks remain, all diagrams rendered as graphics.

---

## Phase 5: Polish & Cross-Cutting

**Purpose**: Final validation across all stories

- [x] T040 [P] Review all pages for consistent terminology (debug-mcp, not DebugMcp/NetInspect.Mcp/DotnetMcp in user-facing text)
- [x] T041 [P] Verify sidebar navigation: Getting Started → Tools (6 pages) → Workflows (3 pages) → Architecture → Debugger → Development
- [x] T042 Final build and link validation: `cd website && npm run build` — zero warnings, zero broken links. Verify responsive layout at 320px, 768px, 1440px, 2560px viewports
- [x] T043 Run quickstart.md validation checklist

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **US1 (Phase 2)**: Depends on Setup (T001-T007)
- **US2 (Phase 3)**: Depends on Setup (T004 AsciinemaPlayer component) + US1 (pages to embed into)
- **US3 (Phase 4)**: Depends on Setup (T003 Mermaid plugin) only — can run in parallel with US1/US2
- **Polish (Phase 5)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (Rich Descriptions)**: Independent after Setup
- **US2 (Asciinema)**: Needs US1 pages to exist (embed recordings into them)
- **US3 (Mermaid Diagrams)**: Independent after Setup — can run parallel with US1

### Parallel Opportunities

- T008–T013: All 6 tool category pages can be written in parallel
- T014–T016: All 3 workflow guides can be written in parallel
- T020–T023: All 4 recordings can be recorded in parallel
- T025–T026: Embedding in tool pages can be parallel
- T029–T031: Mermaid conversion per page can be parallel
- US1 and US3 can proceed in parallel (different files)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T007)
2. Complete Phase 2: US1 — Rich Descriptions (T008–T019)
3. **STOP and VALIDATE**: All 25 tools documented, 3 workflows, Getting Started
4. Deploy — site is already dramatically improved

### Incremental Delivery

1. Setup → US1 (descriptions) → Deploy (MVP)
2. Add US3 (diagrams) → Deploy (visual upgrade)
3. Add US2 (recordings) → Deploy (full experience)
4. Polish → Final deploy

### Recommended Order

US3 (diagrams) before US2 (recordings) because:
- Diagrams are faster to implement (text-only, no CLI recording sessions)
- Recordings reference final page layout (better to embed after content is stable)

---

## Notes

- All 25 tools: 4 Session + 6 Breakpoints + 3 Execution + 5 Inspection + 3 Memory + 4 Module = 25
- Cast files typically 10-50KB — safe for git
- Mermaid renders at build time — no client-side JS overhead
- Asciinema player loads lazily via BrowserOnly — no SSR issues
