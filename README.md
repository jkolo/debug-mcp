# debug-mcp.net

[![CI](https://github.com/jkolo/debug-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/jkolo/debug-mcp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/debug-mcp)](https://www.nuget.org/packages/debug-mcp)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Compatible-blue)](https://modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/License-AGPL--3.0-blue)](LICENSE)

**MCP server for .NET debugging** — enable AI agents to debug .NET applications interactively.

## What is debug-mcp?

debug-mcp is a [Model Context Protocol](https://modelcontextprotocol.io/) server that exposes .NET debugging capabilities as structured API tools. It allows AI assistants like Claude, GPT, or Copilot to:

- Launch or attach to .NET processes
- Set breakpoints, exception breakpoints, and tracepoints
- Step through code line by line
- Inspect variables, evaluate expressions, and read memory
- Analyze stack traces and threads
- Get full exception context with one call (exception autopsy)
- Navigate code with go-to-definition, find usages, and diagnostics
- Automatically resolve symbols from public symbol servers

Unlike similar tools that use external debuggers via DAP protocol, debug-mcp interfaces **directly with the .NET runtime** using ICorDebug APIs — the same approach used by JetBrains Rider.

## Quick Start

### Run

```bash
# No installation needed (.NET 10+)
dnx debug-mcp

# Or one-shot execution
dotnet tool exec debug-mcp
```

### Install (optional)

```bash
# Global tool
dotnet tool install -g debug-mcp

# Local tool (per-project)
dotnet new tool-manifest   # if not already present
dotnet tool install debug-mcp
```

### Requirements

- .NET 10 SDK or later
- Linux (Windows and macOS support planned)

### Configure with Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "dotnet-debugger": {
      "command": "dnx",
      "args": ["debug-mcp"]
    }
  }
}
```

### Configure with Claude Code

Add to your `.mcp.json`:

```json
{
  "mcpServers": {
    "dotnet-debugger": {
      "command": "dnx",
      "args": ["debug-mcp"]
    }
  }
}
```

### Example Conversation

```
You: Debug my ASP.NET app and find why GetUser returns null

Claude: I'll launch your application and investigate.
        [Calls debug_launch with program path]
        [Calls breakpoint_set at UserService.cs:42]
        [Calls debug_continue]
        [Calls breakpoint_wait with 30s timeout]

        The breakpoint was hit. Let me check the variables.
        [Calls variables_get for current frame]

        I found the issue: the `userId` parameter is an empty string.
        The bug is in the calling code at line 28 where...
```

```
You: My app throws NullReferenceException somewhere in checkout. Find it.

Claude: I'll set an exception breakpoint and trigger the flow.
        [Calls debug_launch with program path]
        [Calls breakpoint_set_exception for System.NullReferenceException]
        [Calls debug_continue]
        [Calls breakpoint_wait with include_autopsy: true]

        Caught it! The exception was thrown in OrderService.cs at line 87.
        Here's the full context from the autopsy:
        - cart.Items was null because LoadCart() returned an empty cart
        - The null check at line 85 only checked cart, not cart.Items
```

## Tools (34)

| Category | Tools | Description |
|----------|-------|-------------|
| **Session** | `debug_launch`, `debug_attach`, `debug_disconnect`, `debug_state` | Start, stop, and monitor debug sessions |
| **Execution** | `debug_continue`, `debug_pause`, `debug_step` | Control program flow |
| **Breakpoints** | `breakpoint_set`, `breakpoint_remove`, `breakpoint_list`, `breakpoint_enable`, `breakpoint_wait` | Set and manage source breakpoints |
| **Exception Breakpoints** | `breakpoint_set_exception` | Break on specific exception types (first/second chance) |
| **Tracepoints** | `tracepoint_set` | Non-blocking breakpoints that log messages without pausing |
| **Exception Autopsy** | `exception_get_context` | Full exception analysis: type, message, inner exceptions, stack frames with source, and local variables |
| **Inspection** | `threads_list`, `stacktrace_get`, `variables_get`, `evaluate` | Examine program state |
| **Memory** | `object_inspect`, `memory_read`, `layout_get`, `references_get`, `members_get` | Deep object and memory analysis |
| **Modules** | `modules_list`, `modules_search`, `types_get` | Explore loaded assemblies and types |
| **Code Analysis** | `code_load`, `code_goto_definition`, `code_find_usages`, `code_find_assignments`, `code_get_diagnostics` | Roslyn-powered code navigation and diagnostics |
| **Process I/O** | `process_write_input`, `process_read_output` | Interact with debuggee stdin/stdout |

## Resources (4)

| URI | Description |
|-----|-------------|
| `debugger://session` | Current debug session state |
| `debugger://breakpoints` | All active breakpoints |
| `debugger://threads` | Thread list with states |
| `debugger://source/{file}` | Source file contents |

## Documentation

- [Architecture](https://debug-mcp.net/docs/architecture) — System design and components
- [How Debugging Works](https://debug-mcp.net/docs/debugger) — ICorDebug internals explained
- [MCP Tools Reference](https://debug-mcp.net/docs/tools/session) — Complete API documentation
- [MCP Resources](https://debug-mcp.net/docs/resources) — Subscribable state views
- [Development Guide](https://debug-mcp.net/docs/development) — Building, testing, contributing

## Similar Projects

| Project | Language | Approach | .NET Support |
|---------|----------|----------|--------------|
| [mcp-debugger](https://github.com/debugmcp/mcp-debugger) | TypeScript | DAP | Via external debugger |
| [dap-mcp](https://github.com/KashunCheng/dap_mcp) | Python | DAP | Via external debugger |
| [LLDB MCP](https://lldb.llvm.org/use/mcp.html) | C++ | Native | No |
| **debug-mcp** | C# | ICorDebug | Native, direct |

## License

AGPL-3.0 — see [LICENSE](LICENSE) for details.
