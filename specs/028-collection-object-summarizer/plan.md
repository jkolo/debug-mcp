# Implementation Plan: Collection & Object Summarizer

**Branch**: `028-collection-object-summarizer` | **Date**: 2026-02-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/028-collection-object-summarizer/spec.md`

## Summary

Add two new MCP tools — `collection_analyze` and `object_summarize` — that provide single-call summaries of collections and complex objects during debugging. These tools reduce the 5-50+ tool calls currently needed to understand a collection or wide object to exactly 1 call, preventing token blowup in AI agent workflows.

Implementation builds on existing `ProcessDebugger` infrastructure: `FormatValue` for value formatting, `CallFunctionAsync`/`ICorDebugEval` for calling `.Count` and indexers on collections, `GetElementAtPosition` for array access, and `EnumFields` for object field enumeration.

## Technical Context

**Language/Version**: C# / .NET 10.0 (pinned in global.json)
**Primary Dependencies**: ClrDebug 0.3.4 (ICorDebug), ModelContextProtocol 0.7.0-preview.1
**Storage**: In-memory only (no persistence, session-scoped)
**Testing**: xUnit + FluentAssertions + Moq (unit + contract tests)
**Target Platform**: Cross-platform (Windows, macOS, Linux — x64 + ARM64)
**Project Type**: Single .NET project (DebugMcp) + test project
**Performance Goals**: <2s for 10,000-element collection; <500 tokens output for 1,000-element collection
**Constraints**: Must not deadlock ICorDebug callbacks (lock ordering: `_lock` → `_stateLock`); `CallFunctionAsync` requires the debuggee thread to be paused
**Scale/Scope**: 2 new tools, 2 new services, ~6 new model records, ~15 test classes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Native First | PASS | Uses ICorDebug APIs directly — `CorDebugArrayValue.GetElementAtPosition`, `CallFunctionAsync` for property getters, `MetaDataImport.EnumFields` for field enumeration |
| II. MCP Compliance | PASS | Tools named `collection_analyze` and `object_summarize` (noun_verb). Structured JSON responses with `success` flag. Parameters use JSON Schema with descriptions. |
| III. Test-First | PASS | Will write unit tests before implementation. Contract tests for tool schema validation. |
| IV. Simplicity | PASS | 2 tools, 2 services, flat model records. No unnecessary abstractions. Each tool does one thing. Max 3 levels of indirection: Tool → Service → ProcessDebugger. |
| V. Observability | PASS | Tools log invocation parameters, duration, and outcome via ILogger<T>. |

## Project Structure

### Documentation (this feature)

```text
specs/028-collection-object-summarizer/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── collection-analyze.md
│   └── object-summarize.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
DebugMcp/
├── Models/Inspection/
│   ├── CollectionSummary.cs          # CollectionSummary, NumericStatistics, TypeDistribution records
│   ├── ObjectSummary.cs              # ObjectSummary, FieldSummary, InterestingField records
│   └── CollectionKind.cs             # Enum: Array, List, Dictionary, Set, Queue, Stack, Other
├── Services/
│   ├── CollectionAnalyzer.cs         # ICollectionAnalyzer + impl — core collection analysis
│   └── ObjectSummarizer.cs           # IObjectSummarizer + impl — core object summarization
└── Tools/
    ├── CollectionAnalyzeTool.cs      # MCP tool: collection_analyze
    └── ObjectSummarizeTool.cs        # MCP tool: object_summarize

tests/DebugMcp.Tests/
├── Unit/Inspection/
│   ├── CollectionAnalyzerTests.cs    # Unit tests for CollectionAnalyzer
│   └── ObjectSummarizerTests.cs      # Unit tests for ObjectSummarizer
└── Contract/
    └── ToolAnnotationTests.cs        # Existing — auto-covers new tools
```

**Structure Decision**: Follows existing project layout. Models go in `Models/Inspection/` alongside `Variable`, `EvaluationResult`, etc. Services go in `Services/` alongside `ProcessDebugger`. Tools go in `Tools/` with the existing 34 tools. Test files in `Unit/Inspection/` (new subdirectory for inspection-related tests).

## Design Decisions

### D1: Collection Element Access Strategy

**Decision**: Hybrid approach — direct array access for arrays, `_items` field + `_size` for `List<T>`, `CallFunctionAsync(get_Item)` for other collections.

**Rationale**: Arrays have O(1) direct access via `GetElementAtPosition`. `List<T>` stores elements in a backing `_items` array — reading the field directly avoids the overhead of ICorDebugEval function calls (each `CallFunctionAsync` resumes the debuggee briefly). For `Dictionary<K,V>` and other types, indexer calls via eval are the only reliable option.

**Alternatives rejected**:
- Pure eval-based (`get_Item(i)` for everything): Too slow — each call resumes the process, 10,000 elements × eval = unacceptable latency.
- Pure field-based (read internal `_items` on all types): Brittle — internal field names vary across .NET versions and collection types.

### D2: Collection Type Detection

**Decision**: Type name prefix matching first, fallback to `Count` property existence check.

**Rationale**: Known BCL collection types have stable fully-qualified names (e.g., `System.Collections.Generic.List`1`). Matching by prefix is fast and avoids eval. For unknown collections, checking for a `Count` property getter is a lightweight heuristic — if `get_Count` exists, treat as collection.

### D3: Sampling Strategy for Large Collections

**Decision**: Full enumeration up to 1,000 elements. Above 1,000, enumerate first `maxPreviewItems` + last `maxPreviewItems` for previews; compute statistics from first 1,000 elements; report as sampled.

**Rationale**: ICorDebugEval calls are expensive. Enumerating 1,000 elements is feasible within the 2-second budget. For statistics (min/max/avg), first-1,000 is a reasonable sample. For previews, first/last N is what agents typically need.

### D4: "Interesting" Field Detection in object_summarize

**Decision**: Flag fields with these heuristics:
- `null` reference → null list
- Empty string (`""`) → interesting
- `NaN` or `Infinity` for float/double → interesting
- `0` for non-nullable numeric types → not flagged (too noisy)
- `default(DateTime)`/`default(DateTimeOffset)` → interesting (indicates uninitialized)
- Collection fields → show count inline

**Rationale**: The goal is anomaly detection. Zero is normal for counters; empty strings and NaN are almost always bugs. Uninitialized DateTimes (`0001-01-01`) are a common .NET mistake.

### D5: Service Architecture

**Decision**: Two service interfaces (`ICollectionAnalyzer`, `IObjectSummarizer`) injected into tools, both depending on `IDebugSessionManager` for debugger access.

**Rationale**: Follows existing pattern (e.g., `ISnapshotService` for snapshot tools). Keeps tools thin (parameter validation + JSON serialization) and services testable with mocked `IDebugSessionManager`.

## Complexity Tracking

No constitution violations — no tracking needed.
