# Phase 0 Research: MCP Surface Modernization

**Feature**: 069-mcp-surface-modernization | **Date**: 2026-08-25

Every finding below was verified against a primary source — the MCP specification, the SDK
assemblies on disk, or the repository itself. Nothing here is recalled from memory.

---

## R1. Mechanism for deferred results

**Decision**: MCP Tasks extension, via the `ModelContextProtocol.Extensions.Tasks` package,
version **2.2.0** (matching the pinned SDK).

**Rationale**: This is the specification's own mechanism for long-running operations
([Tasks](https://modelcontextprotocol.io/specification/latest/basic/utilities/tasks)). The
server returns a durable handle, the client polls `tasks/get`, and `tasks/cancel` is
cooperative. Crucially the spec makes adoption safe: *"Before returning a `CreateTaskResult`,
verify that the client included the extension in its per-request capabilities. Never return a
task to a client that did not declare support."* A client that never declares support cannot be
broken by this feature existing.

**Verified**: package versions `2.0.0-preview.3` through `2.2.0` published
(`api.nuget.org/v3-flatcontainer/modelcontextprotocol.extensions.tasks/index.json`). The
assembly at `lib/net10.0/ModelContextProtocol.Extensions.Tasks.dll` exports `IMcpTaskStore`,
`InMemoryMcpTaskStore`, a `WithTasks` builder extension, `CreateTaskAsync`,
`GetMetaWithTaskCapability`, and `CreateMissingTasksCapabilityException`. The package restores
cleanly into this project under CPM (verified by a `dotnet add package` that was then reverted —
the repository is unmodified).

**Resolved at implementation time (T031, via reflection over the installed 2.2.0 assemblies —
`McpServerToolCreateOptions` has no `Execution`/`TaskSupport` member at all; that member never
existed in this SDK version and the plan's original assumption was wrong)**: wiring is
`builder.WithTasks(IMcpTaskStore store, Action<McpTasksOptions> configure)` on `IMcpServerBuilder`
— there is no `AddMcpServer(options => options.TaskStore = ...)` path. Task eligibility is
**not** a per-tool static property; it is one server-wide delegate,
`McpTasksOptions.ExecutionModeSelector: Func<RequestContext<CallToolRequestParams>,
McpTaskExecutionMode>`, evaluated per request. `McpTaskExecutionMode` has three values —
`Synchronous`, `Optional`, `Required` — and the SDK's documented default *"treats every tool as
task-capable"*, so the selector **must** be supplied explicitly or all 39 tools become
task-eligible, contradicting FR-013 exactly as the wrong assumption predicted, just via a
different mechanism. The XML doc for `WithTasks` states *"Tasks are implemented as an
alternate-result call-tool filter"* — the filter, not tool code, decides synchronous-vs-task and
returns a `ResultOrCreatedTask<TResult>`. **Consequence for the plan**: FR-013's five-tool
qualifying set is enforced by **one policy class** (`Services/Tasks/TaskExecutionPolicy.cs`,
mirroring `TimeoutPolicy`'s shape) consulted by the selector, not by editing 34 individual tool
files to "pin `Forbidden`" — there is no such per-tool setting to pin. `RequestContext.Params.Name`
identifies the tool being called; `RequestContext.MatchedPrimitive.Id` is the doc-recommended
alternative that reads tool identity without a Tasks→Core reference. Client opt-in gating (never
returning a task to a client that did not declare the extension) is internal to the SDK's filter,
consistent with the spec quote above — no server-side code re-implements that check.

**Alternatives considered**:
- *Blocking with a longer client timeout* — does not remove the failure, only postpones it, and
  the timeout is the client's to set, not ours.
- *A bespoke job-handle protocol built on ordinary tool calls* — the specification's own
  "Stateful Tools" section describes this pattern, but it would duplicate a standard mechanism,
  require the client to learn our conventions, and forfeit `tasks/cancel`.

---

## R2. Mechanism for progress reporting

**Decision**: `IProgress<ProgressNotificationValue>` as a tool method parameter. Core SDK, no
extra package.

**Rationale**: The SDK binds such a parameter automatically and excludes it from the tool's input
schema, so it is invisible to callers. If the client supplied a `ProgressToken` on the request,
reports propagate as `notifications/progress`; if it did not, the reports are discarded. That is
exactly the silent degradation FR-005 requires — no capability check, no branching in tool code.

**Works over stdio**: yes. Progress notifications are ordinary JSON-RPC notifications on the same
connection; nothing about them is HTTP-specific.

**Alternatives considered**:
- *MCP Logging* (`notifications/message`) as a progress channel — rejected: deprecated by the
  same SEP-2577 that killed Sampling, and this project already carries `MCP9005` debt for it.
- *Progress only inside task status messages* — would tie progress to Story 2 and deny it to
  clients that support progress but not tasks. Keeping them independent is what makes Story 1
  shippable on its own.

---

## R3. Structured results and the text block

**Decision**: `UseStructuredContent = true` with a typed result record per tool, **and** retain
the serialized JSON as a text content block.

**Rationale**: The specification is explicit — *"For backwards compatibility, a tool that returns
structured content SHOULD also return the serialized JSON in a TextContent block."* FR-017 is
therefore aligned with the spec rather than a local invention. On output schemas the spec is
stronger: *"If an output schema is provided: Servers MUST provide structured results that conform
to this schema."* That makes FR-020's validation check a conformance obligation, not a nicety.

**Verified**: `ModelContextProtocol.Core.dll` 2.2.0 exports `UseStructuredContent`,
`OutputSchema`, `OutputSchemaType`, `StructuredContent`, `CreateOutputSchema`,
`IsValidToolOutputSchema`, `SupportsNaturalOutputSchemas` and `TransformOutputSchemaForLegacyWire`
— the last confirming the SDK handles older wire formats itself.

**Note on the NuGet cache location**: this machine's global packages folder is
`/home/jurek/.cache/NuGetPackages`, not `~/.nuget/packages`. Use `dotnet nuget locals
global-packages --list` rather than assuming.

---

## R4. Error reporting shape

**Decision**: keep the existing `ErrorCodes` set and the existing `success: false` payload field,
and additionally set the protocol-level `isError: true` on failed tool results.

**Rationale**: The spec distinguishes *protocol errors* (JSON-RPC `error`, for unknown tool or
malformed request) from *tool execution errors*, which *"are reported in tool results with
`isError: true`"* and which clients **SHOULD** feed back to the model for self-correction. Today
debug-mcp signals failure only through a `success: false` field inside the text payload, so a
client cannot tell success from failure without parsing. Setting `isError` fixes that at the
protocol level; keeping `success` satisfies FR-021's promise that existing fields survive.

**Verified in repo**: `ErrorCodes` is a single `public static class` in
`DebugMcp/Models/ErrorResponse.cs` holding **50** string constants. FR-018/FR-019 build on it;
no new registry is introduced.

---

## R5. Task store durability

**Decision**: `InMemoryMcpTaskStore`, scoped to the server process.

**Rationale**: the transport is stdio with one client per process. There is no reconnect path for
a client to resume against, and every debug-session-bound result references ICorDebug state that
dies with the process. Persisting handles would advertise a resumption the architecture cannot
honour. This confirms the assumption already recorded in the spec.

**Alternatives considered**: a disk-backed store — rejected above. Redis or a database, which the
SDK docs suggest for production HTTP deployments — not applicable to a single-user stdio tool.

---

## R6. Testing strategy for notifications and tasks

**Decision**: follow the project's existing precedent — wrap the un-mockable SDK surface in a
first-party interface and supply a test double.

**Rationale**: `IMcpServer.SendNotificationAsync` is an extension method and cannot be mocked with
Moq. The repository already solved this once: `IBreakpointNotifier` with `NullBreakpointNotifier`
in `tests/DebugMcp.Tests/Support/`, plus `McpResourceNotifier`. Progress and task-status
notifications should be reached through the same shape, so unit tests assert against a recording
double rather than against the SDK.

**End-to-end coverage** still needs a real client, because the opt-in negotiation and the polling
loop are wire-level behaviour that a double cannot exercise. The verification method proven during
the SDK 2.2.0 upgrade applies: drive the server over stdio with JSON-RPC, filtering responses by
`id` because the server interleaves notification lines. That is a `quickstart.md` scenario, not a
unit test.

**Existing anchor for FR-020**: `tests/DebugMcp.Tests/Contract/ToolAnnotationTests.cs` already
enumerates every tool by name and asserts its annotations. The schema-presence and
documentation-coverage checks belong beside it.

---

## R7. Converting the synchronous tools without breaking the lock invariant

**Decision**: change the eight tools' signatures to asynchronous and thread `CancellationToken`
through, but do **not** introduce new concurrency at the ICorDebug boundary.

**Rationale**: The eight synchronous tools (`variables_get`, `stacktrace_get`, `timeline_query`,
`snapshot_create`, `snapshot_delete`, `snapshot_diff`, `process_read_output`,
`process_write_input`) are synchronous in signature; their underlying work is either in-memory or
a blocking runtime call. Making the signature asynchronous changes *who waits*, not *what runs*.
The invariant that must survive is the existing one: `_lock` → `_stateLock` is permitted, the
reverse is forbidden, and ICorDebug callbacks never take `_lock`. FR-006 states this as a
requirement; the risk is that `await` inside a lock-held region introduces interleaving that
today is impossible.

**Constraint for implementation**: no `await` may span a region holding `_lock` or `_stateLock`.
Where a runtime call must be awaited, it is completed before the lock is taken or after it is
released. This is a review checkpoint for every one of the eight conversions, not a general
guideline.

---

## R8. Rejected: model-backed enrichment

**Decision**: rejected on evidence, not deferred.

**Rationale**: [SEP-2577](https://modelcontextprotocol.io/seps/2577-deprecate-roots-sampling-and-logging)
(status **Final**) deprecates Sampling — the only native mechanism for an MCP server to reach a
model — citing low client adoption and complexity, and naming it *"the most security-sensitive of
the three… attack surface for prompt injection and data exfiltration"*. The SEP's stated
alternative is direct provider integration, which was weighed and declined: it requires a
credential, costs per call, produces non-deterministic output that cannot be asserted in tests,
and adds network latency to a debugger.

**What replaces it**: deterministic ranking computed from state the server already holds.
Reproducible, free, and assertable — which is what makes SC-007 and SC-008 possible at all.

---

## R9. Deliberately not adopted

Reviewed against this feature's scope and excluded:

- **Caching hints** (`ttlMs`, `cacheScope` on `tools/list`) — real and applicable, but it is
  ROADMAP #067 and orthogonal to result *shape*.
- **Pagination** (`nextCursor` on results) — ROADMAP #036. This feature bounds results with
  explicit truncation markers only.
- **MRTR / `InputRequiredResult`** — the task lifecycle includes an `input_required` status, but
  no operation in this feature needs mid-flight user input. Not implemented; the status is simply
  never produced.
- **`subscriptions/listen` for `notifications/tasks`** — the spec makes task notifications
  optional and requires clients to poll regardless. Polling alone satisfies Story 2; adding a
  subscription stream is complexity without a requirement behind it.
