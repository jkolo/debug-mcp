# Research: Documentation & Site Update

## R1: Where to place exception_get_context documentation

**Decision**: Add to `tools/breakpoints.md` alongside `breakpoint_set_exception` and `breakpoint_wait` (with `include_autopsy`).

**Rationale**: Exception autopsy is triggered from exception breakpoints. The existing breakpoints page already documents `breakpoint_set_exception` and `breakpoint_wait`. Adding `exception_get_context` here creates a complete "exception debugging" section within the breakpoints page. Users searching for exception-related tools will find everything in one place.

**Alternatives considered**:
- Separate `tools/exception-autopsy.md` page — rejected, only 1 tool doesn't warrant its own page
- Add to `tools/inspection.md` — rejected, exception autopsy is triggered from breakpoint flow, not general inspection

## R2: How to document MCP resources

**Decision**: Create a new top-level page `resources.md` at the same level as tools category.

**Rationale**: Resources are a distinct MCP concept from tools. They represent subscribable state (read-only, push-based) vs tools (request-response). A dedicated page makes this distinction clear and provides space for subscribe/unsubscribe patterns.

**Alternatives considered**:
- Embed in architecture.md — rejected, too buried for practical use
- One page per resource — rejected, only 4 resources, all short

## R3: How to consolidate /docs/ and /website/docs/

**Decision**: Remove `/docs/` directory entirely. Replace with a single `docs/README.md` stub that redirects to the live site. Update README.md links to point to `https://debug-mcp.net/docs/...`.

**Rationale**: The website is the canonical documentation. The root `/docs/` files are older copies that have drifted from the website content. Keeping both causes confusion about which to edit.

**Alternatives considered**:
- Symlinks from /docs/ to /website/docs/ — rejected, doesn't work well with GitHub rendering
- Keep /docs/ as source and generate website from it — rejected, requires restructuring the entire Docusaurus setup

## R4: Existing page style conventions

**Decision**: Follow the pattern established in existing tool pages.

Based on analysis of `tools/session.md`, `tools/breakpoints.md`, etc.:
- Each tool section starts with `## tool_name` heading
- Followed by a description paragraph
- **Parameters** table with Name, Type, Required, Default, Description columns
- **Example** subsection with JSON request/response in fenced code blocks
- Tips or notes in blockquotes where relevant

## R5: Process I/O page structure

**Decision**: Create new `tools/process-io.md` page with both `process_write_input` and `process_read_output`.

**Rationale**: These 2 tools form a logical pair (stdin/stdout interaction). They don't fit into any existing category page. A dedicated page keeps the tool categories clean and discoverable.
