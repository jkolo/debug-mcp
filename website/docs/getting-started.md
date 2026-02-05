---
title: Getting Started
sidebar_position: 0
---

# Getting Started

Get debug-mcp running and perform your first debugging session in minutes.

import AsciinemaPlayer from '@site/src/components/AsciinemaPlayer';

## See It in Action

<AsciinemaPlayer src="/casts/getting-started.cast" rows={24} cols={120} idleTimeLimit={2} speed={1.5} />

## Install

The recommended way to install debug-mcp is via [`dnx`](https://github.com/dn-vm/dnx):

```bash
dnx debug-mcp
```

Alternatively, install as a .NET global tool:

```bash
dotnet tool install -g debug-mcp
```

## Configure Your AI Agent

### Claude Code

Add to your Claude Code MCP settings:

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

### Claude Desktop

Add to `claude_desktop_config.json`:

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

### Other MCP Clients

debug-mcp communicates via MCP over stdio. Any MCP-compatible client can use it:

```bash
# Run directly
dnx debug-mcp

# Or via dotnet tool
debug-mcp
```

## Your First Debugging Session

Let's debug a simple .NET application step by step.

### 1. Launch the application

Ask your AI agent:

> "Launch `/path/to/MyApp.dll` and stop at the entry point"

The agent calls `debug_launch`:
```json
{
  "program": "/path/to/MyApp.dll",
  "stop_at_entry": true
}
```

### 2. Set a breakpoint

> "Set a breakpoint at line 25 in Program.cs"

```json
{
  "file": "Program.cs",
  "line": 25
}
```

### 3. Continue and wait

> "Continue execution and wait for the breakpoint"

The agent calls `debug_continue`, then `breakpoint_wait`.

### 4. Inspect variables

> "Show me the local variables"

```json
// variables_get
{
  "scope": "all"
}
```

### 5. Evaluate an expression

> "What is the value of `items.Count`?"

```json
// evaluate
{
  "expression": "items.Count"
}
```

### 6. Disconnect

> "Stop debugging"

```json
// debug_disconnect
{
  "terminate": true
}
```

## Static Code Analysis

debug-mcp also provides Roslyn-based code analysis tools that work without running the debugger.

### Load a solution

> "Load the solution at /path/to/MyApp.sln for code analysis"

```json
// code_load
{
  "path": "/path/to/MyApp.sln"
}
```

### Find all usages

> "Find all usages of the UserService class"

```json
// code_find_usages
{
  "name": "MyApp.Services.UserService",
  "symbolKind": "Type"
}
```

### Check for errors

> "Are there any compilation errors?"

```json
// code_get_diagnostics
{
  "minSeverity": "Error"
}
```

## What's Next?

- **[Tools Reference](/docs/tools/session)** — Full documentation for all MCP tools
- **[Code Analysis](/docs/tools/code-analysis)** — Static analysis: find usages, assignments, and diagnostics
- **[Analyze a Codebase](/docs/workflows/analyze-codebase)** — Navigate and understand unfamiliar code
- **[Debug a Crash](/docs/workflows/debug-a-crash)** — Step-by-step guide for finding crash root causes
- **[Inspect Memory Layout](/docs/workflows/inspect-memory-layout)** — Analyze object layout and memory usage
- **[Profile Module Loading](/docs/workflows/profile-module-loading)** — Explore loaded assemblies and types
- **[Architecture](/docs/architecture)** — Understand how debug-mcp works internally
