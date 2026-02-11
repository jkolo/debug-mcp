# Implementation Plan: Async Stack Traces

**Branch**: `026-async-stack-traces` | **Date**: 2026-02-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/026-async-stack-traces/spec.md`

## Summary

Add logical async stack trace support to `stacktrace_get`. The current implementation shows raw physical frames including compiler-generated `MoveNext()` methods and thread pool internals. This feature: (1) detects async state machine frames via type name pattern matching, (2) resolves them to original async method names, (3) walks `Task.m_continuationObject` to discover suspended callers, (4) maps state machine fields to original variable names via PDB custom debug info, and (5) adds `frame_kind`/`is_awaiting` metadata to the response.

## Technical Context

**Language/Version**: C# / .NET 10.0 (global.json pins 10.0.102)
**Primary Dependencies**: ClrDebug 0.3.4 (ICorDebug wrappers), ModelContextProtocol 0.7.0-preview.1, System.Reflection.Metadata (PDB reading)
**Storage**: N/A
**Testing**: xUnit + FluentAssertions + Moq; unit + contract tests
**Target Platform**: Windows/macOS/Linux (x64/arm64) — cross-platform since feature 025
**Project Type**: Single .NET tool (PackAsTool)
**Performance Goals**: <500ms overhead for async stack trace vs synchronous (10-frame chain)
**Constraints**: Must not break existing `stacktrace_get` response format; new fields are additive only
**Scale/Scope**: ~5 files modified, ~2 new service files, ~1 new test file

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | Uses ICorDebug field inspection and metadata APIs directly — no external debugger |
| II. MCP Compliance | PASS | Enhances existing `stacktrace_get` tool with additive fields; no new tools needed |
| III. Test-First | PASS | Contract tests for frame detection + unit tests for continuation walking written before implementation |
| IV. Simplicity | PASS | New service (`AsyncStackTraceService`) encapsulates all async logic; existing tools delegate to it |
| V. Observability | PASS | Structured logging for frame detection, chain walking, and fallback paths |

No violations. No complexity justifications needed.

## Project Structure

### Documentation (this feature)

```text
specs/026-async-stack-traces/
├── spec.md
├── plan.md              # This file
├── research.md          # Phase 0: ICorDebug async APIs, continuation chain mechanics
├── quickstart.md        # Phase 1: Verification steps
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (files changed)

```text
DebugMcp/
├── Models/
│   └── Inspection/
│       └── StackFrame.cs                  # Add FrameKind, IsAwaiting, LogicalFunction
├── Services/
│   ├── ProcessDebugger.cs                 # CreateStackFrame: detect MoveNext, resolve names
│   ├── AsyncStackTraceService.cs          # NEW: continuation chain walking, frame synthesis
│   └── Breakpoints/
│       └── PdbSymbolReader.cs             # Add StateMachineHoistedLocalScopes support
└── Tools/
    └── StacktraceGetTool.cs               # Add include_raw param, emit frame_kind/is_awaiting

tests/DebugMcp.Tests/
├── Unit/
│   └── AsyncStackTraceServiceTests.cs     # NEW: unit tests for chain walking logic
└── Contract/
    └── AsyncStackTraceContractTests.cs    # NEW: verify response shape, frame_kind values
```

**Structure Decision**: Existing single-project layout unchanged. One new service file (`AsyncStackTraceService`) encapsulates all async stack trace logic. ProcessDebugger gains detection methods but delegates chain walking to the new service.

## Design Decisions

### D1: Detect Async Frames via Type Name Pattern (not attribute lookup)

**Decision**: Use regex `^<(.+?)>d__\d+$` on the declaring type name when method name is `MoveNext`. Do not use `AsyncStateMachineAttribute` lookup.

**Rationale**: The type name pattern is a compiler guarantee for C# async state machines and is available directly from `GetTypeDefProps` (already called in `GetMethodName`). Attribute lookup via `EnumCustomAttributes` requires finding the *original* method first (reverse mapping), which is the problem we're trying to solve. The type name gives us the original method name directly via capture group 1.

**Trade-off**: Pattern matching is fragile if the compiler changes conventions. However, this pattern has been stable since C# 5.0 (2012) and is relied upon by Visual Studio, Rider, and dotnet-dump.

### D2: Continuation Chain Walking via TryGetFieldValue

**Decision**: Walk `Task.m_continuationObject` using the existing `TryGetFieldValue` infrastructure in ProcessDebugger. Create a new `AsyncStackTraceService` that receives a `CorDebugValue` (the Task) and recursively reads fields.

**Rationale**: `TryGetFieldValue` (ProcessDebugger.cs:3805-3898) already handles reference dereferencing, boxing, and type hierarchy traversal. The continuation object can be: null (no continuation), `Action` delegate, `TaskContinuation`, `List<object>` (multiple continuations), or `ITaskCompletionAction`. For each, we extract the `_target` field of delegates to find the state machine instance, then read `<>1__state` to determine the await position.

**Depth limit**: 50 frames maximum to prevent pathological infinite chains.

### D3: Additive Response Format (no breaking changes)

**Decision**: Add `frame_kind`, `is_awaiting`, and `logical_function` fields to the JSON response. Existing fields remain unchanged. Add `include_raw` parameter (default: false) that includes `raw_frames` array alongside the logical `frames`.

**Rationale**: SC-005 requires all 34 tools continue working unchanged. Additive JSON fields are non-breaking for consumers. The default behavior changes subtly — `MoveNext` frames are presented with their logical names — but the `function` field still contains the full name, and `frame_kind` indicates it's async.

### D4: State Machine Variable Name Mapping via PDB Custom Debug Info

**Decision**: Use `MetadataReader.GetCustomDebugInformation()` with `StateMachineHoistedLocalScopes` kind to map compiler-generated field names (like `<result>5__2`) to original slot indices, then look up the original name via local scope information.

**Rationale**: PdbSymbolReader already uses `System.Reflection.Metadata` and reads local variable scopes. The `StateMachineHoistedLocalScopes` custom debug info blob is available in portable PDBs for Debug builds. This is the same mechanism used by Visual Studio and Rider.

**Fallback**: When PDB info is unavailable (Release builds, missing PDBs), display the compiler-generated name with `<>` brackets stripped for readability (e.g., `<result>5__2` → `result`).

### D5: ValueTask Handling — Best-Effort via Task Extraction

**Decision**: For `ValueTask<T>`, read the `_obj` field. If it's a `Task`, walk its continuation chain. If it's an `IValueTaskSource`, mark the chain as unresolvable at that point.

**Rationale**: `ValueTask` wraps either a `Task` or an `IValueTaskSource`. The Task path is the common case for async methods (the compiler uses `AsyncValueTaskMethodBuilder` which creates a Task internally). `IValueTaskSource` implementations vary and their continuation chains are not standardized — marking as unresolvable is the safe choice per FR-009.

## File Change Summary

| File | Change Type | Description |
|------|------------|-------------|
| `DebugMcp/Models/Inspection/StackFrame.cs` | Modify | Add `FrameKind`, `IsAwaiting`, `LogicalFunction` optional fields |
| `DebugMcp/Services/ProcessDebugger.cs` | Modify | `CreateStackFrame`: detect MoveNext pattern, resolve logical name; expose `TryGetFieldValue` for service use |
| `DebugMcp/Services/AsyncStackTraceService.cs` | New | Continuation chain walking, logical frame synthesis |
| `DebugMcp/Services/Breakpoints/PdbSymbolReader.cs` | Modify | Add `GetStateMachineLocalNamesAsync` for hoisted local mapping |
| `DebugMcp/Tools/StacktraceGetTool.cs` | Modify | Add `include_raw` param, emit `frame_kind`/`is_awaiting`/`logical_function` in response |
| `tests/DebugMcp.Tests/Unit/AsyncStackTraceServiceTests.cs` | New | Mock-based tests for chain walking, frame detection |
| `tests/DebugMcp.Tests/Contract/AsyncStackTraceContractTests.cs` | New | Response shape, frame_kind enum values, backward compatibility |
