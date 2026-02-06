# Data Model: Documentation Page Inventory

## Pages to Create

### 1. `website/docs/tools/process-io.md`

**Sidebar position**: After "Code Analysis" in Tools category
**Content**:

| Tool | Parameters | Key Details |
|------|-----------|-------------|
| `process_write_input` | `data` (string, required), `close_after` (bool, default false) | Write to debuggee stdin; `close_after` sends EOF |
| `process_read_output` | `stream` (stdout/stderr/both, default both), `clear` (bool, default false) | Read accumulated output; `clear` empties buffer after read |

### 2. `website/docs/resources.md`

**Sidebar position**: After "Tools" category, before "Workflows"
**Content**:

| Resource URI | Name | MIME Type | Description |
|-------------|------|-----------|-------------|
| `debugger://session` | Debug Session | application/json | Current session state, process info, pause reason |
| `debugger://breakpoints` | Breakpoints | application/json | All active breakpoints with status and hit counts |
| `debugger://threads` | Threads | application/json | Thread list with states and current locations |
| `debugger://source/{+file}` | Source File | text/plain | Source file contents for a given path |

Sections: Overview of MCP resources vs tools, subscribe/unsubscribe pattern, each resource with URI, description, example response JSON.

### 3. `website/docs/workflows/debug-exceptions.md`

**Sidebar position**: After "Analyze Codebase" in Workflows category
**Content structure**:

1. Introduction — when to use exception debugging
2. Set exception breakpoint (`breakpoint_set_exception`)
3. Trigger the exception (`debug_continue` + `process_write_input` or wait)
4. Wait for hit (`breakpoint_wait` with `include_autopsy: true`)
5. Deep dive (`exception_get_context` with parameters)
6. Inspect variables in throwing frame
7. Navigate to root cause

## Pages to Modify

### 4. `website/docs/tools/breakpoints.md`

**Add section**: `## exception_get_context` after the existing `breakpoint_wait` section.

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `max_frames` | integer | No | 10 | Maximum stack frames to return (1-100) |
| `include_variables_for_frames` | integer | No | 1 | Number of top frames to include local variables for (0-10) |
| `max_inner_exceptions` | integer | No | 5 | Maximum inner exception chain depth (0-20) |

Response structure: `threadId`, `exception` (type, message, isFirstChance), `innerExceptions[]`, `frames[]` (function, module, location, variables), `totalFrames`, `throwingFrameIndex`.

### 5. `website/sidebars.ts`

Add entries:
- `tools/process-io` in Tools category
- `resources` as top-level item after Tools
- `workflows/debug-exceptions` in Workflows category

### 6. `/docs/` directory

Remove all 4 files (ARCHITECTURE.md, DEBUGGER.md, DEVELOPMENT.md, MCP_TOOLS.md). Replace with single `docs/README.md` pointing to https://debug-mcp.net.

### 7. `README.md`

Update Documentation section links from local files to:
- `https://debug-mcp.net/docs/architecture`
- `https://debug-mcp.net/docs/debugger`
- `https://debug-mcp.net/docs/tools/session` (etc.)
- `https://debug-mcp.net/docs/development`
