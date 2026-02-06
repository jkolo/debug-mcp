# Feature Specification: Documentation & Site Update

**Feature Branch**: `023-docs-site-update`
**Created**: 2026-02-06
**Status**: Draft
**Input**: User description: "aktualizacja dokumentacji i strony"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Developer finds complete tool reference for new features (Priority: P1)

A developer visits the debug-mcp.net documentation site looking for how to use exception autopsy, tracepoints, process I/O, breakpoint notifications, or MCP resources. They find up-to-date reference pages covering all 34 tools, grouped by category, with parameter descriptions, example calls, and expected responses.

**Why this priority**: The documentation site currently covers only ~26 tools across 7 pages. Features added since the docs were last updated (exception autopsy, tracepoints, exception breakpoints enhancements, process I/O, MCP resources, symbol server) are undocumented on the site. This is the core gap.

**Independent Test**: Visit each tool category page and verify every tool from the codebase is documented with parameters, examples, and descriptions.

**Acceptance Scenarios**:

1. **Given** the docs site is deployed, **When** a developer navigates to the Tools section, **Then** all 34 MCP tools are documented across category pages
2. **Given** the exception autopsy tool page exists, **When** a developer reads it, **Then** they see the tool name, all parameters, a usage example with JSON response, and a description of the autopsy output structure
3. **Given** the breakpoints page, **When** a developer looks for exception breakpoints and tracepoints, **Then** both `breakpoint_set_exception` and `tracepoint_set` are documented with parameters and examples

---

### User Story 2 - Developer reads workflow guide for exception debugging (Priority: P2)

A developer wants to learn how to use exception breakpoints and autopsy in a real debugging scenario. They find a workflow page that walks through setting an exception breakpoint, triggering it, using `exception_get_context`, and interpreting the autopsy output.

**Why this priority**: Workflows make tools actionable. The existing workflow pages (debug-a-crash, inspect-memory, profile-modules, analyze-codebase) are effective but don't cover exception-based debugging — the key feature of v0.8.0.

**Independent Test**: Navigate to the new workflow page and follow it end-to-end as a tutorial.

**Acceptance Scenarios**:

1. **Given** the workflows section, **When** a developer looks for exception debugging, **Then** they find a dedicated page covering exception breakpoints → autopsy → resolution
2. **Given** the exception debugging workflow page, **When** a developer reads it, **Then** it includes example tool calls, JSON responses, and explanations of each step

---

### User Story 3 - Developer finds MCP Resources documentation (Priority: P2)

A developer wants to subscribe to live debugger state changes using MCP resources. They find documentation explaining the 4 available resources (`debugger://session`, `debugger://breakpoints`, `debugger://threads`, `debugger://source/{file}`), how to subscribe, and what data each resource returns.

**Why this priority**: MCP resources are a distinct feature category with no documentation on the site. They enable real-time state watching which is a different usage pattern from tool calls.

**Independent Test**: Navigate to a resources documentation page and verify all 4 resources are described.

**Acceptance Scenarios**:

1. **Given** the docs site, **When** a developer navigates to a resources section, **Then** they find documentation for all 4 MCP resources
2. **Given** a resource entry, **When** a developer reads it, **Then** they see the URI template, description, MIME type, and example response JSON

---

### User Story 4 - Eliminate duplicate documentation sources (Priority: P3)

The project currently maintains docs in both `/docs/` (root, 4 markdown files) and `/website/docs/` (Docusaurus source, 18 files). A contributor making a documentation change should have one clear place to edit. The root `/docs/` files should redirect to or be replaced by the website as the single source of truth.

**Why this priority**: Dual documentation sources cause confusion and drift. Lower priority because it doesn't affect end-user experience on the site itself.

**Independent Test**: After the change, verify there is a single clear source of truth for documentation and that the README points to it.

**Acceptance Scenarios**:

1. **Given** a contributor wants to update docs, **When** they look at the repo structure, **Then** there is one obvious location for documentation source files
2. **Given** the README, **When** a reader clicks documentation links, **Then** they reach the live documentation site (debug-mcp.net)

---

### Edge Cases

- What happens when tools are added in future features? The documentation structure should make it easy to add a new tool to an existing category page.
- What if the Docusaurus build fails due to broken links? The site build should validate internal links.
- What if asciinema recordings reference outdated tool names or parameters? Recordings should match current tool behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Documentation site MUST cover all 34 MCP tools with parameter descriptions and usage examples
- **FR-002**: Documentation site MUST include a page or section for `exception_get_context` tool with autopsy output structure explained
- **FR-003**: Documentation site MUST include a page or section for `tracepoint_set` tool with log message template syntax
- **FR-004**: Documentation site MUST include a page or section for `breakpoint_set_exception` with first-chance/second-chance explanation
- **FR-005**: Documentation site MUST include a page or section for `process_write_input` and `process_read_output` tools
- **FR-006**: Documentation site MUST include documentation for the 4 MCP resources with URI templates, descriptions, and example responses
- **FR-007**: Documentation site MUST include a workflow page for exception-based debugging (exception breakpoint → autopsy → resolution)
- **FR-008**: The sidebar navigation MUST include entries for all new content so users can discover it
- **FR-009**: The root `/docs/` directory MUST be consolidated — either removed (with README linking to site) or replaced with stubs pointing to the site
- **FR-010**: The README documentation links MUST point to the live site (debug-mcp.net) rather than local markdown files
- **FR-011**: The Docusaurus site MUST build without errors after all changes

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the 34 MCP tools are documented on the site (currently ~26)
- **SC-002**: All 4 MCP resources are documented on the site (currently 0)
- **SC-003**: At least 1 new workflow guide exists for exception debugging
- **SC-004**: The site builds successfully (`npm run build` exits 0)
- **SC-005**: Documentation source exists in exactly one location (no duplication between `/docs/` and `/website/docs/`)
- **SC-006**: README links to live site for all documentation references
