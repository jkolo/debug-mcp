# Implementation Plan: Documentation & Site Update

**Branch**: `023-docs-site-update` | **Date**: 2026-02-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/023-docs-site-update/spec.md`

## Summary

Update the Docusaurus documentation site (debug-mcp.net) to cover all 34 MCP tools (3 missing), add MCP resources documentation, add an exception debugging workflow, and consolidate the dual docs sources (`/docs/` vs `/website/docs/`).

## Technical Context

**Language/Version**: TypeScript (Docusaurus 3.9.2), Markdown/MDX
**Primary Dependencies**: Docusaurus 3.9.2, React 19, asciinema-player 3.14.0, @docusaurus/theme-mermaid
**Storage**: Static files (Markdown, JSON examples)
**Testing**: `npm run build` (Docusaurus strict mode with `onBrokenLinks: 'throw'`)
**Target Platform**: GitHub Pages at debug-mcp.net
**Project Type**: Documentation site (static)
**Constraints**: Must match existing doc page style and structure

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | N/A | Documentation-only feature, no runtime code |
| II. MCP Compliance | PASS | Documenting existing MCP tools and resources |
| III. Test-First | N/A | No testable code; validation via `npm run build` |
| IV. Simplicity | PASS | Adding pages to existing structure, no new abstractions |
| V. Observability | N/A | Documentation-only feature |

All gates pass. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/023-docs-site-update/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (page inventory)
├── quickstart.md        # Phase 1 output
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (files to modify/create)

```text
website/docs/
├── tools/
│   ├── breakpoints.md          # MODIFY — add exception_get_context section
│   ├── inspection.md           # MODIFY — move object_inspect to memory.md if needed
│   └── process-io.md           # CREATE — process_write_input, process_read_output
├── resources.md                # CREATE — 4 MCP resources documentation
└── workflows/
    └── debug-exceptions.md     # CREATE — exception debugging workflow

website/sidebars.ts             # MODIFY — add new pages to navigation

docs/                           # REMOVE — replace with stub README pointing to site
README.md                       # MODIFY — update documentation links to debug-mcp.net
```

**Structure Decision**: Extend existing Docusaurus site structure. New tool category page for Process I/O. Exception autopsy fits naturally in the existing breakpoints/inspection pages. Resources get a dedicated top-level page.
