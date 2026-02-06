# Quickstart: Documentation & Site Update

## What Changed

3 new documentation pages, 2 modified pages, docs consolidation.

## Verification

```bash
# Build the site (strict mode catches broken links)
cd website && npm run build

# Preview locally
cd website && npm run start
# Open http://localhost:3000

# Verify new pages exist
curl -s http://localhost:3000/docs/tools/process-io | head -1
curl -s http://localhost:3000/docs/resources | head -1
curl -s http://localhost:3000/docs/workflows/debug-exceptions | head -1
```

## New Pages

| Page | URL Path | Content |
|------|----------|---------|
| Process I/O | `/docs/tools/process-io` | `process_write_input`, `process_read_output` |
| Resources | `/docs/resources` | 4 MCP resources with examples |
| Debug Exceptions | `/docs/workflows/debug-exceptions` | Exception breakpoint + autopsy workflow |

## Modified Pages

| Page | Change |
|------|--------|
| `tools/breakpoints.md` | Added `exception_get_context` section |
| `sidebars.ts` | Added Process I/O, Resources, Debug Exceptions entries |
| `README.md` | Documentation links now point to debug-mcp.net |
| `docs/` | Replaced with stub README redirecting to site |
