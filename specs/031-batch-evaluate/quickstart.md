# Quickstart: Batch Evaluate & Hypothesis Runner

**Feature**: 031-batch-evaluate

## Prerequisites

- `DebugTestApp` built: `dotnet build tests/DebugTestApp`
- MCP server running: `dotnet run --project DebugMcp`

## Scenario 1 — Three Hypotheses, One Call (P1)

**Goal**: Verify that 3 experiments targeting different locations return results in one batch call.

```jsonc
// 1. Launch DebugTestApp
{ "tool": "debug_launch", "arguments": { "program": "tests/DebugTestApp/bin/Debug/net10.0/DebugTestApp" } }

// 2. Submit batch with 3 non-blocking experiments
{
  "tool": "batch_evaluate",
  "arguments": {
    "experiments": [
      { "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 10 }, "capture": ["counter"] },
      { "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 20 }, "capture": ["name", "value"] },
      { "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 30 }, "capture": ["result"] }
    ],
    "timeout_seconds": 10
  }
}
```

**Expected**:
- `success: true`
- `completion_reason: "all_triggered"` (if all 3 locations execute)
- Each experiment in `experiments` array has `status: "triggered"` and at least one hit with variable values

## Scenario 2 — Non-Blocking Observation in Loop (P2)

**Goal**: Observe a variable across multiple loop iterations without pausing.

```jsonc
{
  "tool": "batch_evaluate",
  "arguments": {
    "experiments": [
      {
        "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 15 },
        "mode": "non_blocking",
        "capture": ["i"],
        "max_hits": 5
      }
    ],
    "timeout_seconds": 10
  }
}
```

**Expected**:
- Program runs to completion without pausing
- Experiment has `hit_count: 5` (or however many loop iterations)
- `hits` array contains 5 entries with different `i` values

## Scenario 3 — Partial Results on Timeout (P3)

**Goal**: Verify partial results are returned when not all experiments trigger.

```jsonc
{
  "tool": "batch_evaluate",
  "arguments": {
    "experiments": [
      { "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 10 }, "capture": ["counter"] },
      { "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 9999 }, "capture": ["x"] }
    ],
    "timeout_seconds": 3
  }
}
```

**Expected**:
- Returns after ~3 seconds
- `completion_reason: "timeout"` (second experiment never triggers)
- First experiment: `status: "triggered"`, has hits
- Second experiment: `status: "not_triggered"`, `hits: []`

## Scenario 4 — Pre-Existing Breakpoint Freeze/Restore

**Goal**: Verify that a breakpoint set before the batch is disabled during the batch and re-enabled after.

```jsonc
// 1. Set a pre-existing breakpoint
{ "tool": "breakpoint_set", "arguments": { "file": "tests/DebugTestApp/Program.cs", "line": 25 } }
// Note the returned breakpoint ID (e.g., "bp-abc123")

// 2. Run a batch
{
  "tool": "batch_evaluate",
  "arguments": {
    "experiments": [
      { "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 10 }, "capture": ["counter"] }
    ],
    "timeout_seconds": 5
  }
}
// During the batch: the pre-existing breakpoint at line 25 should NOT pause execution

// 3. After batch completes: verify breakpoint is still in the session via debugger://breakpoints resource
// The bp-abc123 breakpoint should be present and enabled again
```

## Scenario 5 — Concurrent Same-Location Experiments

**Goal**: Two experiments at the same location both receive independent results.

```jsonc
{
  "tool": "batch_evaluate",
  "arguments": {
    "experiments": [
      {
        "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 15 },
        "capture": ["i"],
        "condition": "i > 3"
      },
      {
        "trigger": { "file": "tests/DebugTestApp/Program.cs", "line": 15 },
        "capture": ["i"],
        "condition": "i == 1"
      }
    ],
    "timeout_seconds": 10
  }
}
```

**Expected**:
- Both experiments report hits (at different values of `i`)
- `experiments[0].hits[0].values.i` > "3"
- `experiments[1].hits[0].values.i` == "1"

## Automated Tests

```bash
# All batch unit tests
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~BatchRunner"

# Contract: tool annotation
dotnet test tests/DebugMcp.Tests --no-build \
  --filter "FullyQualifiedName~ToolAnnotationTests.Tool_batch_evaluate"
```
