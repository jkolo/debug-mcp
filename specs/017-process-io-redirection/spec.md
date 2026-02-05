# Feature 017: Process I/O Redirection

## Problem

When `debug_launch` uses DbgShim's `CreateProcessForLaunch`, the debugged process inherits MCP server's stdio handles. This causes:
1. `Console.ReadLine()` in debugged process blocks MCP's stdin (JSON-RPC input)
2. `Console.WriteLine()` in debugged process corrupts MCP's stdout (JSON-RPC output)
3. MCP tool calls hang when debugged process is running

## Solution

Redirect debugged process stdin/stdout/stderr to internal buffers and provide MCP tools to interact with them.

## User Stories

### US1: Launch with I/O Redirection
As a debugger user, I want the debugged process's I/O to be isolated from MCP, so that MCP communication works correctly.

**Acceptance Criteria:**
- `debug_launch` redirects stdin/stdout/stderr automatically
- MCP JSON-RPC communication continues working while process runs
- Process can block on stdin without affecting MCP

### US2: Read Process Output
As a debugger user, I want to read the debugged process's stdout/stderr output, so I can see what the program prints.

**Acceptance Criteria:**
- New `process_read_output` tool returns buffered stdout/stderr
- Can read stdout only, stderr only, or both
- Output is accumulated (multiple reads don't lose data)
- Can optionally clear buffer after reading

### US3: Write Process Input
As a debugger user, I want to write to the debugged process's stdin, so I can provide input to interactive programs.

**Acceptance Criteria:**
- New `process_write_input` tool writes to stdin buffer
- Written data is immediately available to process
- Can write multiple times
- Can send EOF to close stdin

## Technical Approach

### Implementation Strategy

Use `System.Diagnostics.Process.Start()` with redirected streams instead of raw DbgShim launch:

1. **Launch**:
   - Create `Process` with `RedirectStandardInput/Output/Error = true`
   - Start process (suspended or not)
   - Use DbgShim `RegisterForRuntimeStartup` to wait for CLR
   - Attach debugger when CLR ready

2. **I/O Management**:
   - Create `ProcessIoManager` service
   - Async tasks read stdout/stderr into `StringBuilder` buffers
   - Stdin exposed as `StreamWriter`

3. **New Tools**:
   - `process_read_output`: Read from stdout/stderr buffers
   - `process_write_input`: Write to stdin stream

### Data Model

```csharp
public class ProcessIoManager : IDisposable
{
    private Process? _process;
    private StringBuilder _stdoutBuffer = new();
    private StringBuilder _stderrBuffer = new();
    private readonly Lock _lock = new();

    // Read accumulated output
    public (string Stdout, string Stderr) ReadOutput(bool clear = false);

    // Write to stdin
    public void WriteInput(string data);
    public void CloseInput(); // Send EOF
}
```

### Tool Contracts

#### process_read_output
```json
{
  "stream": "both|stdout|stderr",  // default: "both"
  "clear": false                   // clear buffer after read
}
```

Response:
```json
{
  "stdout": "Hello, World!\n",
  "stderr": "",
  "stdoutBytes": 14,
  "stderrBytes": 0
}
```

#### process_write_input
```json
{
  "data": "user input here\n",
  "closeAfter": false              // send EOF after write
}
```

Response:
```json
{
  "success": true,
  "bytesWritten": 16
}
```

## Non-Goals

- Real-time streaming of output (polling is fine for MCP)
- PTY/terminal emulation
- ANSI color code handling

## Dependencies

- Requires changes to `debug_launch` implementation
- Must work with existing `debug_attach` flow for already-running processes
