# Feature Specification: Documentation Improvement

**Feature Branch**: `012-docs-improvement`
**Created**: 2026-01-29
**Status**: Draft
**Input**: User description: "Improve documentation with richer feature descriptions, asciinema recordings showing real usage examples, and rendered diagrams replacing ASCII art"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rich Feature Descriptions (Priority: P1)

A developer evaluating debug-mcp.net visits the documentation site and finds comprehensive descriptions of each MCP tool — not just parameter tables, but explanations of when and why to use each tool, common workflows, and real-world use cases. Each tool page includes context about what debugging scenario it addresses.

**Why this priority**: Feature descriptions are the most visited part of any tool's docs. Without clear, detailed descriptions, users cannot evaluate or effectively use the tool. This is the foundation all other documentation builds on.

**Independent Test**: Can be tested by reviewing each tool's documentation page and verifying it contains a description, use-case explanation, parameter reference, and at least one example workflow.

**Acceptance Scenarios**:

1. **Given** a developer visits the MCP Tools Reference page, **When** they read any tool's section, **Then** they find a description of the tool's purpose, a "when to use" explanation, full parameter reference, example request/response, and at least one real-world use case.
2. **Given** a developer is looking for a debugging workflow (e.g. "how to debug a memory leak"), **When** they browse the docs, **Then** they find a workflow guide that chains multiple tools together with step-by-step instructions.
3. **Given** a new user with no prior experience, **When** they read the Getting Started section, **Then** they can set up and perform their first debugging session following the documented steps.

---

### User Story 2 - Asciinema Recordings (Priority: P2)

A developer wants to see debug-mcp.net in action before installing it. They visit the docs site and find embedded terminal recordings (asciinema) showing real debugging sessions — attaching to a process, setting breakpoints, inspecting variables, stepping through code. The recordings are interactive (can be paused, copied from) and demonstrate the tool's actual capabilities.

**Why this priority**: Video-like demonstrations dramatically reduce the barrier to understanding. Seeing a real session is worth more than pages of text. This is the strongest "sell" for the tool and helps users learn by watching.

**Independent Test**: Can be tested by visiting docs pages and verifying asciinema embeds load, play, and show realistic debugging sessions.

**Acceptance Scenarios**:

1. **Given** a developer visits the docs landing page or a tool reference page, **When** the page loads, **Then** they see embedded asciinema players showing real debugging sessions.
2. **Given** a developer plays an asciinema recording, **When** they watch it, **Then** they see a complete, realistic debugging workflow (not a toy example) with visible commands and output.
3. **Given** a developer wants to copy a command from a recording, **When** they interact with the asciinema player, **Then** they can select and copy text from the recording.
4. **Given** a developer on a slow connection visits the docs, **When** the page loads, **Then** the asciinema player shows a preview frame before the recording loads, and the rest of the page is not blocked.

---

### User Story 3 - Rendered Diagrams (Priority: P3)

A developer reading the Architecture page sees professionally rendered diagrams (not ASCII art) showing the system's layered architecture, data flow, and component relationships. The diagrams are readable on all screen sizes, support both light and dark themes, and can be maintained as code (Mermaid).

**Why this priority**: Diagrams communicate architecture faster than text. Rendered diagrams look professional and are easier to read than ASCII art, especially on mobile or narrow screens. Using Mermaid means diagrams are maintainable as code alongside the docs.

**Independent Test**: Can be tested by viewing the Architecture page and verifying all diagrams render as graphical elements (not monospace text), are readable at different viewport widths, and display correctly in both light and dark mode.

**Acceptance Scenarios**:

1. **Given** a developer visits the Architecture page, **When** the page renders, **Then** all diagrams appear as rendered graphics (SVG or similar), not ASCII art.
2. **Given** a developer views the Architecture page on a mobile device (< 768px width), **When** they scroll to a diagram, **Then** the diagram is readable without horizontal scrolling.
3. **Given** a developer views the docs in dark mode, **When** they look at a diagram, **Then** the diagram colors adapt to the dark theme and remain legible.
4. **Given** a contributor wants to update a diagram, **When** they edit the Mermaid source in the markdown file, **Then** the diagram updates automatically on the next build.

---

### Edge Cases

- What happens when asciinema.org is unreachable? Recordings should be self-hosted (cast files in the docs repo) so they work independently.
- What happens when a browser doesn't support the asciinema player JavaScript? A static fallback image or text summary should be shown.
- What happens when diagrams contain too many nodes for small screens? Diagrams should be designed with a maximum complexity level and can be scrollable if needed.
- What happens when Mermaid rendering fails during build? The build should still succeed, showing raw Mermaid code as a fallback rather than failing the entire deployment.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each MCP tool documentation MUST include: purpose description, "when to use" guidance, full parameter table, example request, example response, and at least one real-world use case. Tools MUST be organized into one page per category (Session, Breakpoints, Execution, Inspection, Memory, Module).
- **FR-002**: Documentation MUST include at least 3 workflow guides showing multi-tool debugging scenarios (e.g., "Debug a crash", "Inspect memory layout", "Profile module loading"). Guides MUST target developers configuring debug-mcp as an MCP server for their AI agent.
- **FR-003**: Asciinema cast files MUST be self-hosted in the repository (not dependent on asciinema.org availability).
- **FR-004**: Documentation MUST include at least 4 embedded asciinema recordings demonstrating real debugging sessions, covering: first session setup, breakpoint workflow, variable inspection, and a full debugging scenario. Recordings MUST show a multi-class .NET application with at least one exception or logic bug.
- **FR-005**: The asciinema player MUST show a preview frame (poster) before loading and MUST NOT block page rendering.
- **FR-006**: All ASCII art diagrams MUST be replaced with Mermaid diagram source that renders to graphical diagrams.
- **FR-007**: Mermaid diagrams MUST render correctly in both light and dark Docusaurus themes.
- **FR-008**: Documentation MUST include a "Getting Started" guide that takes a new user from installation to their first debugging session.
- **FR-009**: All documentation pages MUST be navigable and readable on viewports from 320px to 2560px width.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every MCP tool (all 20+) has a complete documentation page with purpose, parameters, example, and use case.
- **SC-002**: At least 4 asciinema recordings are embedded in the documentation, each showing a complete debugging workflow.
- **SC-003**: All ASCII art diagrams in the documentation are replaced with rendered Mermaid diagrams (zero ASCII art blocks remain).
- **SC-004**: The documentation site builds successfully with all Mermaid diagrams rendered and all asciinema recordings embedded.
- **SC-005**: At least 3 multi-tool workflow guides are published, each covering a distinct debugging scenario.
- **SC-006**: A new user can follow the Getting Started guide and complete their first debugging session without external help.

## Clarifications

### Session 2026-01-29

- Q: Should tool reference remain a single page or be split? → A: Split into one page per tool category (Session, Breakpoints, Execution, Inspection, Memory, Module).
- Q: How should asciinema recordings be created? → A: Manual recordings using a CLI wrapper that invokes tools with human-friendly commands.
- Q: Who are the workflow guides for? → A: Developers configuring debug-mcp as an MCP server for their AI agent (Claude, etc.).

## Assumptions

- Docusaurus has native Mermaid support via `@docusaurus/theme-mermaid` plugin — this is a well-supported official plugin.
- Asciinema recordings will use the asciinema-player JavaScript library which can be embedded in Docusaurus via a custom React component or MDX.
- Cast files (.cast) are small enough to be stored in the repository (typically 10-50KB per recording).
- Recordings will be created manually using a CLI wrapper with human-friendly commands, against a sample .NET application included in the repo or referenced from existing test targets.
- The existing dark theme in the docs site will be compatible with Mermaid's theme configuration.

## Scope Boundaries

**In scope:**
- Enriching existing tool reference pages with detailed descriptions and examples
- Creating workflow guide pages
- Recording and embedding asciinema sessions
- Replacing ASCII art with Mermaid diagrams
- Adding a Getting Started guide
- Configuring Docusaurus Mermaid plugin

**Out of scope:**
- API documentation auto-generation from code
- Internationalization / translations
- Search functionality improvements
- Blog or changelog pages
- Video tutorials (non-terminal, e.g. screen recordings with voice)
