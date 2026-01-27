# Quickstart: Debug Launch

**Feature Branch**: `007-debug-launch`
**Date**: 2026-01-27

## Purpose

This guide helps developers verify that debug_launch works correctly - launching .NET applications under debugger control.

## Prerequisites

1. .NET 10.0 SDK installed
2. Project built: `dotnet build`
3. Test application available: `tests/TestTargetApp`

## Build and Run

```bash
# Build the project
cd /home/jurek/src/Own/DotnetMcp
dotnet build

# Build test application
dotnet build tests/TestTargetApp/TestTargetApp.csproj
```

## Verification Steps

### US1: Basic Launch (P1)

**Test Scenario**: Launch a .NET DLL under debugger control

```bash
# Using MCP tools (via Claude Code or MCP client)

# 1. Launch application
debug_launch(program: "/path/to/TestTargetApp.dll")
# Expected: success with session.state = "paused" (stopAtEntry default true)
# Expected: session.pauseReason = "entry"

# 2. Verify process is running
debug_state()
# Expected: state = "paused", launchMode = "launch"

# 3. Continue execution
debug_continue()
# Expected: state = "running"

# 4. Disconnect
debug_disconnect()
# Expected: success
```

**Pass Criteria**: Process launches and debugger attaches before user code runs.

---

### US2: Stop at Entry (P2)

**Test Scenario**: Verify stopAtEntry behavior

```bash
# Test with stopAtEntry = true (default)
debug_launch(program: "/path/to/TestTargetApp.dll", stopAtEntry: true)
# Expected: state = "paused", pauseReason = "entry"
# No console output from TestTargetApp should appear

debug_disconnect()

# Test with stopAtEntry = false
debug_launch(program: "/path/to/TestTargetApp.dll", stopAtEntry: false)
# Expected: state = "running"
# TestTargetApp should print "READY" immediately
```

**Pass Criteria**: stopAtEntry=true pauses before any user code; stopAtEntry=false runs immediately.

---

### US3: Command Line Arguments (P3)

**Test Scenario**: Pass arguments to launched process

```bash
debug_launch(
  program: "/path/to/TestTargetApp.dll",
  args: ["--verbose", "--config", "test.json"]
)

# Verify args are passed
# (TestTargetApp would need to echo args - modify or use dedicated test app)
```

**Pass Criteria**: Arguments accessible to launched process in correct order.

---

### US4: Working Directory (P4)

**Test Scenario**: Set working directory

```bash
debug_launch(
  program: "/path/to/TestTargetApp.dll",
  cwd: "/tmp/test-workdir"
)

# Verify working directory
# Use evaluate or inspect to check Environment.CurrentDirectory
```

**Pass Criteria**: Process starts with specified working directory.

---

### US5: Environment Variables (P5)

**Test Scenario**: Set environment variables

```bash
debug_launch(
  program: "/path/to/TestTargetApp.dll",
  env: "{\"MY_VAR\": \"test_value\", \"DEBUG_MODE\": \"true\"}"
)

# Verify environment
# Use evaluate to check Environment.GetEnvironmentVariable("MY_VAR")
```

**Pass Criteria**: Environment variables accessible to launched process.

---

### Error Cases

**File not found:**
```bash
debug_launch(program: "/nonexistent/app.dll")
# Expected: error.code = "INVALID_PATH"
```

**Already attached:**
```bash
# First attach to something
debug_attach(pid: 12345)

# Try to launch while attached
debug_launch(program: "/path/to/app.dll")
# Expected: error.code = "ALREADY_ATTACHED"
```

**Invalid working directory:**
```bash
debug_launch(
  program: "/path/to/app.dll",
  cwd: "/nonexistent/directory"
)
# Expected: error with clear message about directory not found
```

---

## Running Automated Tests

```bash
# Run all tests
dotnet test

# Run only launch tests
dotnet test --filter "FullyQualifiedName~Launch"

# Run specific integration tests
dotnet test --filter "FullyQualifiedName~LaunchIntegrationTests"
```

## Success Criteria Summary

| Scenario | Success Indicator |
|----------|-------------------|
| Basic launch | Process starts, debugger attached, state="paused" |
| stopAtEntry=true | No console output before explicit continue |
| stopAtEntry=false | Process running immediately, output visible |
| Arguments | Args accessible via Environment.GetCommandLineArgs() |
| Working directory | Environment.CurrentDirectory matches cwd |
| Environment vars | Environment.GetEnvironmentVariable() returns values |
| Error: not found | INVALID_PATH error code |
| Error: already attached | ALREADY_ATTACHED error code |

## Troubleshooting

### "Program not found"
- Verify the full path to the DLL
- Ensure the application is built

### "Launch failed: could not find dbgshim"
- Ensure .NET SDK is installed
- Verify dbgshim NuGet package is restored

### "Permission denied"
- Check file permissions
- On Linux, check ptrace_scope: `cat /proc/sys/kernel/yama/ptrace_scope`
- If value is 1, run: `echo 0 | sudo tee /proc/sys/kernel/yama/ptrace_scope`

### "Timeout during launch"
- Increase timeout parameter
- Check if process is starting (ps aux)
- Verify .NET runtime is available

### Process exits immediately
- Check if application requires console input
- Verify dependencies are available
- Check application logs
