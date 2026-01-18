# Research: Debug Session Management

**Feature**: 001-debug-session
**Date**: 2026-01-17

## Executive Summary

This research covers three key areas for implementing debug session management:
1. ICorDebug APIs for attach/launch
2. MCP server implementation in C#
3. Process launch vs. attach workflows

All technical decisions align with the Constitution's Native First principle.

---

## 1. ICorDebug APIs for Attach and Launch

### Decision: Use ClrDebug Library for ICorDebug Wrappers

**Rationale**: ClrDebug provides complete managed C# wrappers for all ICorDebug
interfaces, eliminating COM interop boilerplate while maintaining direct API access.

**Alternatives considered**:
- Raw COM interop: Too verbose, error-prone
- ClrMD: Read-only inspection, not interactive debugging
- DAP-based approach: Violates Native First principle

### Key APIs

| Operation | API | Notes |
|-----------|-----|-------|
| Attach | `ICorDebug.DebugActiveProcess(pid)` | Connects to running process |
| Launch | `ICorDebug.CreateProcess(path, args)` | Starts process under debugger |
| Detach | `ICorDebugProcess.Detach()` | Cleanly disconnects |
| State | `ICorDebugProcess` properties | Query execution state |

### Infrastructure: dbgshim

The dbgshim native library is required to get `ICorDebug` instances:

```csharp
var corDebug = DbgShim.CreateDebuggingInterfaceFromVersion(
    runtimeVersion,
    runtimePath
);
corDebug.Initialize();
corDebug.SetManagedHandler(callbackHandler);
```

**Platform packages**:
- `Microsoft.Diagnostics.DbgShim.win-x64`
- `Microsoft.Diagnostics.DbgShim.linux-x64`
- `Microsoft.Diagnostics.DbgShim.osx-x64`

### Critical: AppDomain Attachment

Every `CreateAppDomain` callback MUST call `appDomain.Attach()` before
`Continue()`, otherwise events are missed:

```csharp
void CreateAppDomain(ICorDebugProcess process, ICorDebugAppDomain appDomain)
{
    appDomain.Attach();  // CRITICAL
    process.Continue(false);
}
```

### .NET Process Detection

Before attaching, verify the target is a .NET process:

```csharp
// Windows: Check for coreclr.dll or clr.dll in process modules
// Linux: Check /proc/{pid}/maps for libcoreclr.so
// macOS: Check for libcoreclr.dylib
```

---

## 2. MCP Server Implementation

### Decision: Use Official ModelContextProtocol C# SDK

**Rationale**: Official SDK with Microsoft collaboration, attribute-based tool
definition, automatic JSON Schema generation, clean DI integration.

**Package**: `ModelContextProtocol` (NuGet, prerelease)

### Tool Definition Pattern

```csharp
[McpServerToolType]
public class DebugTools
{
    [McpServerTool(Name = "debug_attach"),
     Description("Attach debugger to running process")]
    public ToolCallResult DebugAttach(
        [Description("Process ID")] int pid,
        [Description("Timeout ms")] int timeout = 30000
    )
    {
        // Implementation
    }
}
```

### Server Setup

```csharp
var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();
```

### Error Handling Pattern

Return errors via `IsError` flag, not exceptions:

```csharp
return new ToolCallResult
{
    Content = new[] { new TextContent { Text = errorMessage } },
    IsError = true
};
```

---

## 3. Launch vs. Attach Workflows

### Decision: Support Both Workflows with Clear Distinction

**Attach** (`debug_attach`):
- For running processes
- User provides PID
- Cannot guarantee pre-execution attachment

**Launch** (`debug_launch`):
- Creates process under debugger control
- Debugger attached before any user code
- Supports `stopAtEntry` for immediate pause

### Launch Implementation

```csharp
var process = corDebug.CreateProcess(
    null,                                    // lpApplicationName
    "dotnet /path/to/app.dll arg1",         // Command line
    null, null, false,                       // Security/inherit
    CreateProcessFlags.CREATE_NEW_CONSOLE,   // Flags
    null,                                    // Environment
    workingDirectory,                        // CWD
    null, null,                              // Startup/process info
    CorDebugCreateProcessFlags.DEBUG_NO_SPECIAL_OPTIONS,
    out ICorDebugProcess outProcess
);
```

### Stop at Entry Implementation

Two approaches:
1. **Synthetic breakpoint**: Set breakpoint at IL offset 0 of Main method
2. **Module load wait**: Pause on primary module `LoadModule` event

Recommend approach #2 for simplicity.

---

## 4. Cross-Platform Considerations

### Architecture Matching

- Debugger and debuggee must match bitness (32/64-bit)
- Use runtime-specific dbgshim NuGet packages

### Symbol Files

- Use Portable PDB format (default in modern .NET)
- Enables source mapping on all platforms

### Permissions

- Linux: May require `CAP_SYS_PTRACE` or same-user debugging
- Windows: Requires debug privileges (usually automatic for same user)
- macOS: May require developer mode or code signing

---

## 5. Dependencies Summary

| Package | Purpose | Version |
|---------|---------|---------|
| `ModelContextProtocol` | MCP server SDK | 0.6.0+ |
| `ClrDebug` | ICorDebug wrappers | Latest |
| `Microsoft.Diagnostics.DbgShim.{platform}` | Native debugger shim | Latest |
| `Microsoft.Extensions.Hosting` | DI and hosting | 10.0 |

---

## Sources

- [ICorDebug Interface - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/debugging/icordebug-interface)
- [ClrDebug - GitHub](https://github.com/lordmilko/ClrDebug)
- [MCP C# SDK Documentation](https://modelcontextprotocol.github.io/csharp-sdk/)
- [Build a Model Context Protocol Server in C# - .NET Blog](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [Writing a .NET Debugger - Low Level Design](https://lowleveldesign.org/2010/10/11/writing-a-net-debugger-part-1-starting-the-debugging-session/)
