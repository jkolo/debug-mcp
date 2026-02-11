# Feature Specification: State Snapshot & Diff

**Feature Branch**: `027-state-snapshot-diff`
**Created**: 2026-02-11
**Status**: Draft
**Input**: User description: "027 State Snapshot & Diff"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Capture Debug State at a Point in Time (Priority: P1)

An AI agent pauses at a breakpoint and wants to record the current state of all variables in scope. The agent calls a snapshot tool with an optional label (e.g., "before-retry"). The system captures local variables, arguments, and `this` for the current frame, assigns a unique snapshot ID, and returns confirmation. The agent can take multiple snapshots at different points during a debugging session.

**Why this priority**: Without the ability to capture snapshots, the diff feature has nothing to compare. This is the foundational building block — it delivers value on its own by giving agents a "bookmark" of state they can refer back to.

**Independent Test**: Pause at a breakpoint, call snapshot_create, verify a snapshot ID is returned with the captured variable names, types, and values.

**Acceptance Scenarios**:

1. **Given** the debugger is paused at a breakpoint, **When** the agent calls snapshot_create with label "before-fix", **Then** the system returns a snapshot ID, the label, a timestamp, the thread ID, frame index, and a count of captured variables.
2. **Given** the debugger is paused, **When** the agent calls snapshot_create without a label, **Then** the system returns a snapshot with an auto-generated label (e.g., "snapshot-1").
3. **Given** the debugger is running (not paused), **When** the agent calls snapshot_create, **Then** the system returns an error indicating the process must be paused.
4. **Given** the agent has already taken 3 snapshots, **When** the agent calls snapshot_create again, **Then** a 4th snapshot is created with a unique ID and sequential numbering.

---

### User Story 2 - Compare Two Snapshots to See What Changed (Priority: P1)

An AI agent has captured two snapshots (e.g., "before-retry" and "after-retry") and wants to understand what changed between them. The agent calls a diff tool with two snapshot IDs. The system compares all captured variables and returns a structured list of changes: added variables, removed variables, and modified variables (with before/after values). This eliminates the need for the agent to manually compare variable lists across multiple tool calls.

**Why this priority**: This is the core value proposition — seeing what changed between two points. Combined with US1, it enables the primary "snapshot, act, snapshot, diff" workflow that reduces round-trips from many variable reads to a single diff call.

**Independent Test**: Take two snapshots at different points, call snapshot_diff, verify the response contains accurate added/removed/modified variable lists with before and after values.

**Acceptance Scenarios**:

1. **Given** two snapshots exist where variable `retryCount` changed from 2 to 3, **When** the agent calls snapshot_diff with both IDs, **Then** the response includes `retryCount` in the "modified" list with old value "2" and new value "3".
2. **Given** two snapshots where a new variable `result` appeared in the second, **When** the agent calls snapshot_diff, **Then** `result` appears in the "added" list with its current value.
3. **Given** two snapshots where variable `tempBuffer` existed in the first but not the second (different scope), **When** the agent calls snapshot_diff, **Then** `tempBuffer` appears in the "removed" list.
4. **Given** two identical snapshots (nothing changed), **When** the agent calls snapshot_diff, **Then** the response indicates no changes with empty added/removed/modified lists.
5. **Given** one valid and one invalid snapshot ID, **When** the agent calls snapshot_diff, **Then** the system returns an error identifying the invalid snapshot.

---

### User Story 3 - List and Manage Snapshots (Priority: P2)

An AI agent has taken several snapshots during a debugging session and wants to review what snapshots exist, or clean up old ones to free memory. The agent can list all snapshots (seeing ID, label, timestamp, frame info) and delete specific ones or clear all.

**Why this priority**: Management features are secondary to the core capture-and-diff workflow, but become important in longer debugging sessions where many snapshots accumulate.

**Independent Test**: Create several snapshots, list them, verify all appear with correct metadata. Delete one, list again, verify it's gone.

**Acceptance Scenarios**:

1. **Given** 3 snapshots exist, **When** the agent calls snapshot_list, **Then** all 3 are returned with ID, label, timestamp, thread ID, frame function name, and variable count.
2. **Given** a snapshot with ID "snap-abc123" exists, **When** the agent calls snapshot_delete with that ID, **Then** the snapshot is removed and subsequent list/diff calls no longer reference it.
3. **Given** 5 snapshots exist, **When** the agent calls snapshot_clear, **Then** all snapshots are removed.
4. **Given** no snapshots exist, **When** the agent calls snapshot_list, **Then** the response indicates an empty list.

---

### User Story 4 - Capture Nested Object State (Priority: P2)

An AI agent is debugging a complex object graph and wants the snapshot to capture not just top-level variables but also nested fields up to a configurable depth. For example, snapshotting `user` should capture `user.Name`, `user.Address.City`, etc. This gives the diff meaningful granularity — "user.Address.City changed from Warsaw to Krakow" rather than just "user changed".

**Why this priority**: Enhances the usefulness of diffs significantly but adds complexity. The basic flat-variable snapshot (US1) delivers value on its own; depth expansion is an enhancement.

**Independent Test**: Pause where a complex object is in scope, create a snapshot with depth=2, verify nested fields appear. Change a nested field, snapshot again, diff shows the nested change.

**Acceptance Scenarios**:

1. **Given** a variable `order` with fields `order.Total` and `order.Customer.Name`, **When** snapshot_create is called with depth=2, **Then** the snapshot captures `order.Total`, `order.Customer`, and `order.Customer.Name`.
2. **Given** two snapshots where `order.Customer.Name` changed, **When** snapshot_diff is called, **Then** the modified list includes `order.Customer.Name` with old and new values.
3. **Given** snapshot_create is called with depth=0 (default), **When** the snapshot is captured, **Then** only top-level variable names, types, and values are stored (no expansion).

---

### Edge Cases

- What happens when a snapshot is taken in a frame with no local variables? System returns a valid snapshot with zero variables.
- How does the system handle snapshots across different threads? Each snapshot records its thread ID; diffing snapshots from different threads is allowed but the response includes a thread mismatch warning.
- What happens when the debug session disconnects? All snapshots are cleared — they are session-scoped.
- What happens when a variable's type changes between snapshots (e.g., due to different scope)? The diff marks it as both "removed" (old type) and "added" (new type).
- How are large collections handled in snapshots? Collections capture summary info (type, count) at the top level; element-level capture only when depth > 0, limited by depth parameter.
- What is the maximum number of snapshots allowed? 100 per session. Beyond this, the system warns but does not reject — the agent is expected to manage cleanup.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a tool to capture the current debug state (variables, arguments, this) as a named snapshot with a unique ID.
- **FR-002**: System MUST provide a tool to compare two snapshots and return structured differences (added, removed, modified variables with before/after values).
- **FR-003**: System MUST provide a tool to list all snapshots in the current session with their metadata (ID, label, timestamp, thread ID, frame function, variable count).
- **FR-004**: System MUST provide a tool to delete a specific snapshot by ID.
- **FR-005**: Snapshots MUST capture variable name, type, string value, and scope (local/argument/this) for the specified frame.
- **FR-006**: Snapshots MUST support configurable expansion depth (default: 0 = top-level only) to capture nested object fields.
- **FR-007**: Snapshot diff MUST categorize changes into three groups: added variables, removed variables, and modified variables.
- **FR-008**: Modified variables in a diff MUST include both the old value and new value.
- **FR-009**: Snapshots MUST be scoped to the current debug session — all snapshots are cleared when the session disconnects.
- **FR-010**: System MUST return an error when snapshot operations are attempted while the process is not paused.
- **FR-011**: System MUST return an error when diff is requested with an invalid or deleted snapshot ID.
- **FR-012**: Snapshot diff MUST work across snapshots taken on different threads, with a warning flag in the response when thread IDs differ.
- **FR-013**: Each snapshot MUST record: snapshot ID, label, creation timestamp, thread ID, frame index, frame function name.

### Key Entities

- **Snapshot**: A point-in-time capture of debug state. Contains an ID (snap-{guid}), label, timestamp, thread ID, frame index, frame function name, and a list of captured variables with their values.
- **SnapshotVariable**: A single captured variable within a snapshot. Contains name, type, string value, scope, and optionally expanded child variables (when depth > 0).
- **SnapshotDiff**: The result of comparing two snapshots. Contains the two snapshot IDs, lists of added/removed/modified variables, and metadata (thread match flag, timestamp delta).
- **DiffEntry**: A single change within a diff. For modified variables: name, type, old value, new value. For added/removed: name, type, value.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An agent can capture a snapshot and diff two snapshots in a single debugging pause — completing the "what changed?" workflow in 2 tool calls instead of the current N variable reads plus manual comparison.
- **SC-002**: Snapshot capture completes within 2 seconds for frames with up to 50 variables at depth 0.
- **SC-003**: Snapshot diff returns results within 1 second for snapshots with up to 200 variables each.
- **SC-004**: All existing debugging tools continue to work unchanged — snapshot feature is purely additive with no regressions.
- **SC-005**: Snapshot and diff responses follow the same structured format as existing tools, requiring no special handling by agents.
- **SC-006**: The feature reduces the number of tool calls needed for a "track state evolution" workflow by at least 60% compared to manually reading variables at each point.

## Assumptions

- Snapshots store string representations of values (same format as variables_get), not raw memory. This keeps the feature simple and consistent with existing inspection tools.
- Snapshot IDs use the `snap-{guid}` prefix convention, consistent with existing ID patterns (bp-, tp-, ebp-).
- The default expansion depth of 0 means only top-level variables are captured. This matches the behavior of variables_get without the expand parameter.
- Snapshots are in-memory only — no persistence across sessions. This matches the ephemeral nature of debug sessions.
- The 100-snapshot limit is a soft limit with a warning, not a hard cap that rejects new snapshots.
