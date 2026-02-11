# Quickstart: State Snapshot & Diff

**Feature**: 027-state-snapshot-diff
**Date**: 2026-02-11

## Verification Steps

### 1. Build

```bash
dotnet build
```

Expected: 0 errors, 0 warnings.

### 2. Run Unit Tests

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit.Snapshots"
```

Expected: All snapshot-related unit tests pass.

### 3. Run Contract Tests

```bash
dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~SnapshotToolContract"
```

Expected: All 4 snapshot tools have valid MCP schema annotations.

### 4. Manual MCP Verification

Start the MCP server and connect with a client:

```bash
dotnet run --project DebugMcp
```

#### Step 4a: Create a Snapshot

1. Launch a test target: `debug_launch` with TestTargetApp
2. Set breakpoint: `breakpoint_set` on a method with variables
3. Trigger the breakpoint (send command to test app)
4. Call `snapshot_create` with label "before"
5. Verify response contains: `snap-*` ID, label, timestamp, variable count > 0

#### Step 4b: Diff Two Snapshots

1. Continue execution: `debug_continue`
2. Trigger the breakpoint again (or a different one)
3. Call `snapshot_create` with label "after"
4. Call `snapshot_diff` with both snapshot IDs
5. Verify response contains structured added/removed/modified lists

#### Step 4c: List and Delete

1. Call `snapshot_list` — verify both snapshots appear
2. Call `snapshot_delete` with one snapshot ID — verify it's removed
3. Call `snapshot_list` — verify only one remains
4. Call `snapshot_delete` without ID — verify all cleared

#### Step 4d: Session Cleanup

1. Call `debug_disconnect`
2. Call `snapshot_list` — verify empty (session-scoped cleanup)

### 5. Cross-Platform Build

```bash
dotnet build -c Release -r win-x64
dotnet build -c Release -r osx-arm64
dotnet build -c Release -r linux-x64
```

Expected: All 3 platforms build with 0 errors, 0 warnings.
