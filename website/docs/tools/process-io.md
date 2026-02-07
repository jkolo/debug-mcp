---
title: Process I/O
sidebar_position: 8
---

# Process I/O

Process I/O tools let you interact with the debugged process's standard input and output streams. This is essential for debugging interactive console applications, sending commands to CLI tools, and capturing program output.

## When to Use

Use process I/O tools when the debugged application reads from stdin or writes to stdout/stderr. Without these tools, console applications would hang waiting for input, and you'd miss their output entirely.

**Typical flow:** `debug_launch` → `process_read_output` *(see startup messages)* → `process_write_input` *(send commands)* → `process_read_output` *(see results)*

## Tools

### process_write_input

Write data to the debugged process's stdin.

**Requires:** Active session (running or paused)

**When to use:** The debugged application is waiting for user input — a CLI prompt, a menu selection, a line of text. Use `close_after` to send EOF when the application reads until end of stream.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `data` | string | Yes | Data to write to stdin |
| `close_after` | boolean | No | Close stdin after writing, sending EOF (default: false) |

**Example request:**
```json
{
  "data": "SELECT * FROM users WHERE id = 42\n"
}
```

**Example response:**
```json
{
  "success": true,
  "bytesWritten": 38,
  "stdinClosed": false
}
```

**Sending EOF after input:**
```json
{
  "data": "exit\n",
  "close_after": true
}
```

```json
{
  "success": true,
  "bytesWritten": 5,
  "stdinClosed": true
}
```

**Errors:**
- `NO_SESSION` — No process attached. Use `debug_launch` first.
- `STDIN_CLOSED` — Stdin is already closed (previously sent EOF).
- `IO_FAILED` — Failed to write input.

**Real-world use case:** An AI agent is debugging a REPL-style application that processes commands. It writes `"help\n"` to discover available commands, reads the output to understand the interface, then writes specific commands to trigger the code path being debugged.

---

### process_read_output

Read accumulated stdout and/or stderr output from the debugged process.

**Requires:** Active session (running or paused)

**When to use:** You want to see what the process has printed — startup messages, log output, error messages, or command responses. Output accumulates between reads; use `clear` to empty the buffer after reading.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `stream` | string | No | Which stream to read: `"stdout"`, `"stderr"`, or `"both"` (default: `"both"`) |
| `clear` | boolean | No | Clear the buffer after reading (default: false) |

**Example request:**
```json
{
  "stream": "both",
  "clear": true
}
```

**Example response:**
```json
{
  "success": true,
  "stdout": "Application started.\nListening on port 5000.\n",
  "stderr": "",
  "stdoutBytes": 42,
  "stderrBytes": 0
}
```

**Reading only stderr:**
```json
{
  "stream": "stderr"
}
```

```json
{
  "success": true,
  "stderr": "warn: Failed to load optional config file.\n",
  "stderrBytes": 43
}
```

**Errors:**
- `NO_SESSION` — No process attached. Use `debug_launch` first.
- `INVALID_PARAMETER` — Invalid stream value (must be `"stdout"`, `"stderr"`, or `"both"`).
- `IO_FAILED` — Failed to read output.

**Real-world use case:** An AI agent launches a web server and checks `process_read_output` to confirm it started successfully before sending HTTP requests. After the server crashes, it reads stderr to find the error message — without ever needing to open a log file.

---

## Tips

- **Buffer management:** Output accumulates between reads. Use `clear: true` to avoid seeing the same output twice.
- **Line endings:** Always include `\n` at the end of `data` when writing to stdin — most applications read line-by-line.
- **Combine with breakpoints:** Set a breakpoint, write input that triggers the code path, then inspect state when the breakpoint hits.
- **EOF behavior:** Once `close_after: true` is used, stdin cannot be reopened. Only use this when the application should stop reading input.
