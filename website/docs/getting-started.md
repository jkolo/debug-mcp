---
title: Getting Started
sidebar_position: 0
---

# Getting Started

Get debug-mcp running and perform your first debugging session in minutes.

import AsciinemaPlayer from '@site/src/components/AsciinemaPlayer';

## See It in Action

<AsciinemaPlayer src="/casts/getting-started.cast" rows={24} cols={120} idleTimeLimit={2} speed={1.5} />

## Prerequisites

- [.NET 10 SDK](https://dot.net) or later

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

### Claude Code / Claude Desktop

Add to your MCP configuration (`settings.json` or `claude_desktop_config.json`):

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

## Verify It Works

Ask your AI agent:

> "What debug-mcp tools are available?"

You should see a list of 34 tools including `debug_launch`, `breakpoint_set`, and `evaluate`.

## Your First Debugging Session

Let's debug a simple .NET application step by step.

### 1. Launch the application

> "Launch `/path/to/MyApp.dll` and stop at the entry point"

The process starts and pauses immediately at `Main()`.

<details>
<summary>Tool call details</summary>

**Request** (`debug_launch`):
```json
{
  "program": "/path/to/MyApp.dll",
  "stop_at_entry": true
}
```

</details>

### 2. Set a breakpoint

> "Set a breakpoint at line 25 in Program.cs"

The agent maps the file and line to an IL offset and creates the breakpoint.

<details>
<summary>Tool call details</summary>

**Request** (`breakpoint_set`):
```json
{
  "file": "Program.cs",
  "line": 25
}
```

</details>

### 3. Continue and wait

> "Continue execution and wait for the breakpoint"

The agent calls `debug_continue`, then `breakpoint_wait`. Execution resumes until line 25 is hit.

### 4. Inspect variables

> "Show me the local variables"

The agent retrieves all local variables, arguments, and `this` for the current frame.

<details>
<summary>Tool call details</summary>

**Request** (`variables_get`):
```json
{
  "scope": "all"
}
```

</details>

### 5. Evaluate an expression

> "What is the value of `items.Count`?"

The agent evaluates the expression in the context of the stopped thread and returns the result.

<details>
<summary>Tool call details</summary>

**Request** (`evaluate`):
```json
{
  "expression": "items.Count"
}
```

</details>

### 6. Disconnect

> "Stop debugging"

The agent terminates the process and ends the session.

<details>
<summary>Tool call details</summary>

**Request** (`debug_disconnect`):
```json
{
  "terminate": true
}
```

</details>

## Static Code Analysis

debug-mcp also provides Roslyn-based code analysis tools that work without running the debugger.

### Load a solution

> "Load the solution at /path/to/MyApp.sln for code analysis"

<details>
<summary>Tool call details</summary>

**Request** (`code_load`):
```json
{
  "path": "/path/to/MyApp.sln"
}
```

</details>

### Find all usages

> "Find all usages of the UserService class"

<details>
<summary>Tool call details</summary>

**Request** (`code_find_usages`):
```json
{
  "name": "MyApp.Services.UserService",
  "symbolKind": "Type"
}
```

</details>

### Check for errors

> "Are there any compilation errors?"

<details>
<summary>Tool call details</summary>

**Request** (`code_get_diagnostics`):
```json
{
  "minSeverity": "Error"
}
```

</details>

## What's Next?

- **[Tools Overview](/docs/tools)** — All 34 tools at a glance
- **[Debug with Breakpoints](/docs/workflows/debug-with-breakpoints)** — The most common debugging workflow
- **[Debug an Exception](/docs/workflows/debug-an-exception)** — Find crash root causes with exception autopsy
- **[Analyze a Codebase](/docs/workflows/analyze-codebase)** — Navigate and understand unfamiliar code
- **[Inspect Memory Layout](/docs/workflows/inspect-memory-layout)** — Analyze object layout and memory usage
- **[Explore Application Structure](/docs/workflows/explore-application-structure)** — Browse loaded assemblies and types
- **[Architecture](/docs/architecture)** — Understand how debug-mcp works internally
