# Asciinema Recording Scenarios

Recording instructions for debug-mcp documentation demos. Each scenario shows a real Claude Code session where the user asks the AI agent to debug the BuggyApp sample application.

## Prerequisites

- `asciinema` CLI installed (`pacman -S asciinema` / `brew install asciinema`)
- debug-mcp installed and configured as MCP server in Claude Code
- BuggyApp built: `cd website/samples/BuggyApp && dotnet build`

## General Recording Tips

- Use `asciinema rec <file>.cast --idle-time-limit 2 --title "<Title>"`
- Start `claude` inside the BuggyApp directory or provide full paths
- Let Claude Code respond naturally — the recording shows the real agent experience
- Ctrl+D to stop recording
- Preview with `asciinema play <file>.cast`

---

## Scenario 0: Adding debug-mcp to Claude Code

**File:** `setup-mcp.cast`
**Duration target:** ~30-45 seconds
**Demonstrates:** Installing debug-mcp and adding it as MCP server in Claude Code

### Script

```
# Start recording
$ asciinema rec setup-mcp.cast --idle-time-limit 2 --title "Adding debug-mcp to Claude Code"

# Step 1: Install debug-mcp
$ dnx debug-mcp --version
# Output: debug-mcp 0.1.1 (or current version)

# Step 2: Add debug-mcp as MCP server
$ claude mcp add dotnet-debugger dnx debug-mcp
# Output: Added MCP server dotnet-debugger with command: dnx debug-mcp

# Step 3: Verify it works — start Claude Code and check tools
$ claude

# User prompt:
> What debugging tools do you have available?

# Expected: Claude lists available tools from debug-mcp
# (debug_launch, debug_attach, breakpoint_set, variables_get, etc.)
```

### Key Moments to Capture

- The `dnx debug-mcp --version` output confirming installation
- Claude Code adding the MCP server configuration
- The list of available debugging tools after setup

---

## Scenario 1: Getting Started

**File:** `getting-started.cast`
**Duration target:** ~60-90 seconds
**Demonstrates:** First debugging session with Claude Code + debug-mcp

### Script

```
# Start recording
$ asciinema rec getting-started.cast --idle-time-limit 2 --title "Getting Started with debug-mcp"

# Start Claude Code
$ claude

# User prompt:
> Launch website/samples/BuggyApp (the built DLL) and stop at the entry point.
> Then set a breakpoint at line 16 in Program.cs, continue, and when it hits,
> show me the local variables.

# Expected Claude Code behavior:
# 1. Calls debug_launch with stop_at_entry: true
# 2. Sets breakpoint at Program.cs:16
# 3. Calls debug_continue
# 4. Calls breakpoint_wait
# 5. Calls variables_get — shows user=null
# 6. Claude explains: "The user variable is null because GetUser returned null for 'unknown-id'"

# User follow-up:
> Disconnect and stop the process.

# Claude calls debug_disconnect with terminate: true
```

### Key Moments to Capture

- Claude Code calling debug-mcp tools (visible in the tool call output)
- The variables output showing `user = null`
- Claude's explanation of the bug

---

## Scenario 2: Breakpoint Workflow

**File:** `breakpoint-workflow.cast`
**Duration target:** ~90-120 seconds
**Demonstrates:** Various breakpoint types via Claude Code conversation

### Script

```
$ claude

# User prompt:
> Launch BuggyApp under the debugger (stop at entry).
> Set up three breakpoints:
> 1. A line breakpoint at Calculator.cs line 20
> 2. An exception breakpoint for NullReferenceException
> 3. A conditional breakpoint at UserService.cs line 35 that only triggers when userId equals "unknown-id"
> Then list all breakpoints and show me what we have.

# Expected: Claude sets all 3 breakpoints, calls breakpoint_list, shows summary

# User:
> Now disable the Calculator breakpoint and continue execution.
> Wait for whatever hits first.

# Expected: Claude disables bp 1, continues, waits — NullReferenceException hits
# Claude shows the exception location and explains what happened

# User:
> Continue past this exception and wait for the Calculator breakpoint.
> Oh wait, we disabled it — re-enable it first.

# Expected: Claude re-enables bp, continues, waits for Calculator.cs:20 hit
# Shows the state at the breakpoint

# User:
> Clean up — disconnect and terminate.
```

### Key Moments to Capture

- Three different breakpoint types being set
- breakpoint_list showing all breakpoints
- Enable/disable toggling
- Exception breakpoint catching NullReferenceException

---

## Scenario 3: Variable Inspection

**File:** `variable-inspection.cast`
**Duration target:** ~90-120 seconds
**Demonstrates:** Deep inspection of variables, stack traces, expression evaluation

### Script

```
$ claude

# User prompt:
> Launch BuggyApp, set a breakpoint at line 20 in Calculator.cs
> (inside CalculateTotal), and continue until it hits.

# Expected: Claude launches, sets bp, continues, waits — hits Calculator.cs:20

# User:
> Show me the full stack trace, list all threads, and then show me
> all the variables in the current frame.

# Expected: Claude calls stacktrace_get, threads_list, variables_get
# Shows: order object with UnitPrice=100, Quantity=3, subtotal=300

# User:
> The discount calculation looks wrong. Evaluate these two expressions:
> 1. subtotal - 30 (what the code does)
> 2. subtotal * 0.70m (what it should do — 30% discount)

# Expected: Claude evaluates both expressions
# Shows: 270 vs 210 — the bug is clear

# User:
> Inspect the order object in detail — I want to see the memory layout.

# Expected: Claude calls object_inspect on order
# Shows field values, types, sizes

# User:
> So the bug is that it subtracts 30 as a flat amount instead of
> applying 30% discount. Got it. Disconnect.
```

### Key Moments to Capture

- Stack trace showing the call chain
- Variables listing with concrete values
- Side-by-side expression evaluation revealing the bug (270 vs 210)
- Object inspection with field details

---

## Scenario 4: Full Debug Session

**File:** `full-debug-session.cast`
**Duration target:** ~2-3 minutes
**Demonstrates:** Complete debugging workflow — finding two bugs, browsing modules

### Script

```
$ claude

# User prompt:
> I have a buggy .NET app at website/samples/BuggyApp. It has at least
> two bugs. Help me find them. Launch it under the debugger with an
> exception breakpoint for all exceptions.

# Expected: Claude launches with exception breakpoint on System.Exception
# Continues execution, waits

# Bug 1: NullReferenceException
# Expected: Exception breakpoint fires at Program.cs:16
# Claude gets stacktrace, inspects variables, finds user=null
# Explains: GetUser returns null for unknown IDs, caller doesn't check

# User:
> OK, that's bug #1. Continue to find the next issue.

# Expected: Claude continues, process runs to the Calculator section
# If there's another exception or Claude sets a breakpoint at the Calculator

# User (if needed):
> Set a breakpoint at Calculator.cs line 22 and continue.

# Bug 2: Logic error in discount
# Expected: Claude inspects variables at Calculator breakpoint
# Evaluates expressions to show 270 vs 210 discrepancy
# Explains: discount=30 is subtracted as flat amount, should be 30% multiplier

# User:
> What modules does this app have loaded? Show me the types in the BuggyApp module.

# Expected: Claude calls modules_list (filtered), then types_get for BuggyApp
# Shows: Models (User, Order), Services (UserService, Calculator, OrderService)

# User:
> Show me the members of the Calculator class.

# Expected: Claude calls members_get for BuggyApp.Services.Calculator
# Shows: CalculateTotal method signature

# User:
> Great, we found 2 bugs. Disconnect and terminate.
```

### Key Moments to Capture

- Claude autonomously investigating the first crash
- The "aha" moment when user=null is discovered
- Expression evaluation comparing wrong vs correct discount
- Module browsing showing app structure
- Claude's natural-language explanations throughout

---

## Post-Recording

After recording all 4 `.cast` files, place them in `website/static/casts/` and notify so they can be embedded in the documentation pages.
