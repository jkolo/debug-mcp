# Implementation Plan: Documentation Improvement

**Branch**: `012-docs-improvement` | **Date**: 2026-01-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/012-docs-improvement/spec.md`

## Summary

Improve the debug-mcp.net documentation website with richer feature descriptions for all MCP tools (split into category pages), embedded asciinema terminal recordings showing real debugging sessions via CLI wrapper, and Mermaid-rendered diagrams replacing ASCII art. Target audience: developers configuring debug-mcp as an MCP server for AI agents.

## Technical Context

**Language/Version**: TypeScript (Docusaurus 3), Markdown/MDX
**Primary Dependencies**: Docusaurus 3, @docusaurus/theme-mermaid, asciinema-player (npm), asciinema CLI (for recording)
**Storage**: Static files (cast files, markdown) in repository
**Testing**: `npm run build` (Docusaurus build validates all pages, Mermaid syntax, broken links)
**Target Platform**: Static website hosted on GitHub Pages at debug-mcp.net
**Project Type**: Documentation website (Docusaurus)
**Performance Goals**: N/A (static site)
**Constraints**: Cast files must be self-hosted, all diagrams via Mermaid, dark/light theme support
**Scale/Scope**: 20+ tool pages across 6 category pages, 4+ asciinema recordings, 3+ workflow guides, 1 Getting Started guide

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
| --------- | ------ | ----- |
| I. Native First | N/A | Documentation feature, no runtime code |
| II. MCP Compliance | PASS | Documentation will reflect MCP tool naming/parameter conventions |
| III. Test-First | N/A | No implementation code; validation via `npm run build` |
| IV. Simplicity | PASS | Using Docusaurus built-in Mermaid plugin, minimal custom components |
| V. Observability | N/A | Documentation feature, no runtime code |

**Gate result**: PASS — this is a documentation-only feature; constitution principles apply to content accuracy (documenting tools per MCP standards) not to code implementation.

## Project Structure

### Documentation (this feature)

```text
specs/012-docs-improvement/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
website/
├── docs/
│   ├── getting-started.md           # New: Getting Started guide
│   ├── architecture.md              # Updated: Mermaid diagrams
│   ├── tools/                       # New: tool category pages
│   │   ├── session.md               # debug_launch, debug_attach, debug_disconnect, debug_state
│   │   ├── breakpoints.md           # breakpoint_set, breakpoint_remove, breakpoint_list, breakpoint_enable, breakpoint_set_exception, breakpoint_wait
│   │   ├── execution.md             # debug_continue, debug_pause, debug_step
│   │   ├── inspection.md            # stacktrace_get, threads_list, variables_get, evaluate, object_inspect
│   │   ├── memory.md                # memory_read, layout_get, references_get
│   │   └── modules.md               # modules_list, modules_search, types_get, members_get
│   ├── workflows/                   # New: multi-tool workflow guides
│   │   ├── debug-a-crash.md
│   │   ├── inspect-memory-layout.md
│   │   └── profile-module-loading.md
│   ├── debugger.md                  # Updated: Mermaid diagrams
│   └── development.md              # Existing, minor updates
├── src/
│   ├── components/
│   │   └── AsciinemaPlayer.tsx      # New: React wrapper for asciinema-player
│   └── ...
├── static/
│   └── casts/                       # New: self-hosted .cast files
│       ├── getting-started.cast
│       ├── breakpoint-workflow.cast
│       ├── variable-inspection.cast
│       └── full-debug-session.cast
├── sidebars.ts                      # Updated: new sidebar structure
├── docusaurus.config.ts             # Updated: Mermaid plugin
└── package.json                     # Updated: asciinema-player dependency
```

**Structure Decision**: Extend existing Docusaurus website with new `tools/` and `workflows/` subdirectories under `docs/`. Tool reference split from single page into 6 category pages. Asciinema casts stored in `static/casts/`. Custom React component wraps asciinema-player for MDX embedding.
