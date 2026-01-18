# Quickstart: Debug Session Management

**Feature**: 001-debug-session

This guide shows how to use DotnetMcp's debug session tools to connect to and
manage .NET debugging sessions.

---

## Prerequisites

1. DotnetMcp installed and configured with your AI assistant
2. A .NET application to debug (running or ready to launch)

---

## Basic Workflows

### Workflow 1: Attach to Running Process

When your application is already running and you need to debug it:

```
User: I have an app running on PID 12345, can you debug it?

Claude: I'll attach to that process.
        [Calls debug_attach with pid=12345]

        Successfully attached to process 12345 (MyApp.exe).
        Runtime: .NET 8.0.1
        State: running

        What would you like me to investigate?
```

**Tool invocation**:
```json
{
  "tool": "debug_attach",
  "arguments": {
    "pid": 12345
  }
}
```

### Workflow 2: Launch and Debug from Start

When you need to debug startup issues or catch problems from the beginning:

```
User: Debug /app/MyService.dll and stop at the entry point

Claude: I'll launch the application under debugger control.
        [Calls debug_launch with program="/app/MyService.dll", stopAtEntry=true]

        Process launched and paused at entry point.
        PID: 12346
        Runtime: .NET 8.0.1
        State: paused at entry

        Ready to set breakpoints or step through code.
```

**Tool invocation**:
```json
{
  "tool": "debug_launch",
  "arguments": {
    "program": "/app/MyService.dll",
    "stopAtEntry": true
  }
}
```

### Workflow 3: Check Session State

Query the current debugging state at any time:

```
User: What's the debugger status?

Claude: [Calls debug_state]

        Current state: paused
        Process: MyService (PID 12346)
        Paused at: /app/Services/UserService.cs line 42
        Reason: breakpoint
```

**Tool invocation**:
```json
{
  "tool": "debug_state",
  "arguments": {}
}
```

### Workflow 4: Clean Disconnect

When debugging is complete:

```
User: I'm done debugging, disconnect

Claude: [Calls debug_disconnect]

        Disconnected from process 12346.
        The process will continue running normally.
```

**Tool invocation**:
```json
{
  "tool": "debug_disconnect",
  "arguments": {
    "terminateProcess": false
  }
}
```

---

## Common Scenarios

### Debugging with Command-Line Arguments

```json
{
  "tool": "debug_launch",
  "arguments": {
    "program": "/app/MyApp.dll",
    "args": ["--config", "debug.json", "--verbose"],
    "cwd": "/app/data",
    "stopAtEntry": false
  }
}
```

### Debugging with Environment Variables

```json
{
  "tool": "debug_launch",
  "arguments": {
    "program": "/app/MyApp.dll",
    "env": {
      "ASPNETCORE_ENVIRONMENT": "Development",
      "LOG_LEVEL": "Debug"
    }
  }
}
```

### Terminating a Launched Process

When you launched a process and want to terminate it:

```json
{
  "tool": "debug_disconnect",
  "arguments": {
    "terminateProcess": true
  }
}
```

---

## Error Handling

### Process Not Found

```
Error: No process found with PID 99999
Action: Verify the PID is correct and the process is running
```

### Not a .NET Process

```
Error: Process 12345 is not a .NET application
Action: Ensure the target process is a .NET application
```

### Permission Denied

```
Error: Permission denied to debug process 12345
Action: Run with elevated privileges or debug a process owned by your user
```

### Session Already Active

```
Error: A debug session is already active (PID 12345)
Action: Disconnect from current session before attaching to another
```

---

## Tool Reference

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| `debug_attach` | Connect to running process | `pid` (required) |
| `debug_launch` | Start process under debugger | `program` (required) |
| `debug_disconnect` | End debug session | `terminateProcess` (optional) |
| `debug_state` | Query current state | None |

---

## Tips

1. **Always check state first**: Use `debug_state` to understand the current
   debugging context before taking actions.

2. **Use stopAtEntry for startup bugs**: When debugging initialization issues,
   launch with `stopAtEntry: true` to pause before any user code runs.

3. **Attach vs Launch**: Use `debug_attach` for running production/development
   apps; use `debug_launch` when you need control from the start.

4. **Clean up**: Always disconnect when done to release resources and allow the
   application to run normally.
