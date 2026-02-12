# Tasks: Collection & Object Summarizer

**Input**: Design documents from `/specs/028-collection-object-summarizer/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — constitution principle III (Test-First) is non-negotiable.

**Organization**: Tasks grouped by user story. US1 (collection_analyze) is the MVP. US2 (object_summarize) and US3 (nested collection fields) are independent increments.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Models)

**Purpose**: Create all positional record models needed by both tools. These are data-only — no logic, no dependencies.

- [x] T001 [P] Create CollectionKind enum in DebugMcp/Models/Inspection/CollectionKind.cs — values: Array, List, Dictionary, Set, Queue, Stack, Other
- [x] T002 [P] Create CollectionSummary and related records (NumericStatistics, TypeCount, ElementPreview, KeyValuePreview) in DebugMcp/Models/Inspection/CollectionSummary.cs per data-model.md
- [x] T003 [P] Create ObjectSummary and related records (FieldSummary, InterestingField) in DebugMcp/Models/Inspection/ObjectSummary.cs per data-model.md

---

## Phase 2: Foundational (Interfaces & DI)

**Purpose**: Define service contracts and wire up DI. MUST complete before user story implementation.

- [x] T004 [P] Define ICollectionAnalyzer interface with `Task<CollectionSummary> AnalyzeAsync(string expression, int maxPreviewItems, int? threadId, int frameIndex, int timeoutMs, CancellationToken ct)` in DebugMcp/Services/ICollectionAnalyzer.cs
- [x] T005 [P] Define IObjectSummarizer interface with `Task<ObjectSummary> SummarizeAsync(string expression, int maxPreviewItems, int? threadId, int frameIndex, int timeoutMs, CancellationToken ct)` in DebugMcp/Services/IObjectSummarizer.cs
- [x] T006 Register ICollectionAnalyzer and IObjectSummarizer as scoped services in DebugMcp/Program.cs (add `.AddScoped<ICollectionAnalyzer, CollectionAnalyzer>()` and `.AddScoped<IObjectSummarizer, ObjectSummarizer>()`)

**Checkpoint**: Models and interfaces compile. DI registration ready (implementations stubbed if needed for build).

---

## Phase 3: User Story 1 — Summarize a Large Collection (Priority: P1) — MVP

**Goal**: `collection_analyze` tool returns a single-call summary of any collection: count, element types, null count, first/last N previews, numeric stats, type distribution, sampling indicator.

**Independent Test**: Pause at breakpoint with a collection variable → call `collection_analyze` → verify complete summary in one response.

### Tests for User Story 1

> **Write these FIRST, ensure they FAIL before implementation**

- [x] T007 [P] [US1] Unit tests for collection type detection in tests/DebugMcp.Tests/Unit/Inspection/CollectionAnalyzerTests.cs — test ClassifyCollection returns correct CollectionKind for: int[], List\<string\>, Dictionary\<int,string\>, HashSet\<int\>, Queue\<int\>, Stack\<int\>, custom ICollection\<T\>, non-collection object. Mock IDebugSessionManager.
- [x] T008 [P] [US1] Unit tests for array/list analysis in tests/DebugMcp.Tests/Unit/Inspection/CollectionAnalyzerTests.cs — test AnalyzeAsync for: int[] with 10 elements (verify count, firstElements, lastElements), empty array (verify count=0, empty previews), List\<string\> with nulls (verify nullCount), List\<object\> with mixed types (verify typeDistribution).
- [x] T009 [P] [US1] Unit tests for numeric statistics and sampling in tests/DebugMcp.Tests/Unit/Inspection/CollectionAnalyzerTests.cs — test: int[] returns min/max/avg, double[] with NaN skips NaN in stats, collection >1000 elements returns isSampled=true, empty collection returns no numericStats.
- [x] T010 [P] [US1] Unit tests for dictionary analysis in tests/DebugMcp.Tests/Unit/Inspection/CollectionAnalyzerTests.cs — test: Dictionary\<string,int\> returns keyValuePairs with correct key/value types, empty dictionary returns count=0.
- [x] T011 [P] [US1] Unit tests for error cases in tests/DebugMcp.Tests/Unit/Inspection/CollectionAnalyzerTests.cs — test: non-collection expression returns error with code "not_collection", process not paused returns "not_paused", invalid expression returns "variable_unavailable".

### Implementation for User Story 1

- [x] T012 [US1] Implement collection type detection in DebugMcp/Services/CollectionAnalyzer.cs — static dictionary mapping type name prefixes to CollectionKind, fallback to FindPropertyGetter("Count") check. Method: `CollectionKind ClassifyCollection(string typeName, CorDebugValue value)`.
- [x] T013 [US1] Implement array and List\<T\> element enumeration in DebugMcp/Services/CollectionAnalyzer.cs — arrays via GetElementAtPosition(i), List\<T\> via reading _items field + _size. Extract first N and last N ElementPreview records using FormatValue.
- [x] T014 [US1] Implement Dictionary\<K,V\> key-value pair enumeration in DebugMcp/Services/CollectionAnalyzer.cs — read _entries array, iterate entries extracting key/value fields. Return KeyValuePreview records. Fallback to eval-based get_Item if internal layout differs.
- [x] T015 [US1] Implement other collection support (Set, Queue, Stack, Other) in DebugMcp/Services/CollectionAnalyzer.cs — get Count via eval (CallFunctionAsync for get_Count), element preview via eval-based enumeration (limited to maxPreviewItems).
- [x] T016 [US1] Implement numeric statistics computation in DebugMcp/Services/CollectionAnalyzer.cs — single-pass min/max/sum over enumerated elements using CorDebugGenericValue. Detect numeric element types (int, long, float, double, decimal). Skip NaN values. Report NumericStatistics only for numeric collections.
- [x] T017 [US1] Implement type distribution and null counting in DebugMcp/Services/CollectionAnalyzer.cs — during element enumeration, track runtime type → count map and null count. Return TypeDistribution when >1 distinct type found.
- [x] T018 [US1] Implement sampling for large collections in DebugMcp/Services/CollectionAnalyzer.cs — if count >1000, enumerate first 1000 for statistics, first maxPreviewItems + last maxPreviewItems for previews. Set isSampled=true.
- [x] T019 [US1] Implement CollectionAnalyzeTool MCP wrapper in DebugMcp/Tools/CollectionAnalyzeTool.cs — [McpServerToolType] class with [McpServerTool(Name="collection_analyze", Title="Analyze Collection", ReadOnly=true, Destructive=false, Idempotent=true, OpenWorld=false)]. Parameters: expression (required), max_preview_items (default 5, 1-50), thread_id, frame_index, timeout_ms. Inject ICollectionAnalyzer + ILogger. Serialize CollectionSummary to JSON per contract. Error handling per contract error codes.
- [x] T020 [US1] Verify all US1 tests pass — run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~CollectionAnalyzerTests"` and fix any failures.

**Checkpoint**: `collection_analyze` tool works for arrays, List, Dictionary, HashSet, Queue, Stack, and unknown ICollection types. Numeric stats, type distribution, null counting, and sampling all functional. All US1 tests green.

---

## Phase 4: User Story 2 — Summarize a Complex Object (Priority: P2)

**Goal**: `object_summarize` tool returns a curated view of any object: categorized fields (valued, null, interesting), size, inaccessible count.

**Independent Test**: Pause at breakpoint with a complex object → call `object_summarize` → verify fields categorized with anomalies flagged.

### Tests for User Story 2

> **Write these FIRST, ensure they FAIL before implementation**

- [x] T021 [P] [US2] Unit tests for field enumeration and categorization in tests/DebugMcp.Tests/Unit/Inspection/ObjectSummarizerTests.cs — test: object with mixed fields returns correct Fields (non-null, non-default), NullFields list, TotalFieldCount. Mock IDebugSessionManager.
- [x] T022 [P] [US2] Unit tests for interesting field detection in tests/DebugMcp.Tests/Unit/Inspection/ObjectSummarizerTests.cs — test: empty string flagged as "empty_string", NaN flagged as "nan", Infinity flagged as "infinity", default DateTime flagged as "default_datetime", Guid.Empty flagged as "default_guid", normal zero NOT flagged.
- [x] T023 [P] [US2] Unit tests for null/simple/error cases in tests/DebugMcp.Tests/Unit/Inspection/ObjectSummarizerTests.cs — test: null variable returns isNull=true, simple 2-field object returns all fields with no interestingFields, inaccessible fields counted, process not paused returns error.

### Implementation for User Story 2

- [x] T024 [US2] Implement field enumeration and value classification in DebugMcp/Services/ObjectSummarizer.cs — enumerate fields via MetaDataImport.EnumFields, skip static fields, read values via GetFieldValue, classify into: valued (FieldSummary), null (NullFields list), interesting (InterestingField with reason). Track inaccessible fields.
- [x] T025 [US2] Implement interesting value heuristics in DebugMcp/Services/ObjectSummarizer.cs — detect: empty string (value == `"\"\""`), NaN (float/double NaN), Infinity, default DateTime/DateTimeOffset (year 0001), Guid.Empty (all zeros). Return InterestingField records with reason codes per research R4.
- [x] T026 [US2] Implement ObjectSummarizeTool MCP wrapper in DebugMcp/Tools/ObjectSummarizeTool.cs — [McpServerToolType] class with [McpServerTool(Name="object_summarize", Title="Summarize Object", ReadOnly=true, Destructive=false, Idempotent=true, OpenWorld=false)]. Parameters: expression (required), max_preview_items (default 5), thread_id, frame_index, timeout_ms. Inject IObjectSummarizer + ILogger. Serialize ObjectSummary to JSON per contract.
- [x] T027 [US2] Verify all US2 tests pass — run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ObjectSummarizerTests"` and fix any failures.

**Checkpoint**: `object_summarize` tool categorizes fields into valued/null/interesting, detects anomalies, handles null variables and simple objects. All US2 tests green.

---

## Phase 5: User Story 3 — Nested Collection Fields (Priority: P3)

**Goal**: When `object_summarize` encounters a collection-typed field, it shows the element count and type inline instead of raw `{System.Collections.Generic.List}`.

**Independent Test**: Call `object_summarize` on an object with List\<T\> and Dictionary\<K,V\> fields → verify collectionCount and collectionElementType populated.

**Depends on**: US1 (collection type detection) and US2 (object summarization)

### Tests for User Story 3

- [x] T028 [P] [US3] Unit tests for inline collection field detection in tests/DebugMcp.Tests/Unit/Inspection/ObjectSummarizerTests.cs — test: field of type List\<Order\> with 12 items returns collectionCount=12 and collectionElementType="Order", null collection field appears in nullFields, non-collection field has collectionCount=null.

### Implementation for User Story 3

- [x] T029 [US3] Enhance ObjectSummarizer to detect collection-typed fields and populate collectionCount/collectionElementType in DebugMcp/Services/ObjectSummarizer.cs — reuse ClassifyCollection from CollectionAnalyzer to identify collection fields, call get_Count via eval to get element count, parse element type from generic type name. Add to FieldSummary.
- [x] T030 [US3] Verify all US3 tests pass — run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ObjectSummarizerTests"` and ensure collection field tests pass alongside existing US2 tests.

**Checkpoint**: `object_summarize` shows inline collection counts for collection-typed fields. All US2+US3 tests green.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verify everything works together, no regressions.

- [x] T031 Build and verify zero errors zero warnings — run `dotnet build` from repo root
- [x] T032 Run contract tests to verify new tools are discovered — run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~ToolAnnotationTests"`, verify tool count increased to 40
- [x] T033 Run full unit + contract test suite — run `dotnet test tests/DebugMcp.Tests --no-build --filter "FullyQualifiedName~Unit|FullyQualifiedName~Contract"`, verify zero failures
- [ ] T034 Run quickstart.md smoke test validation per specs/028-collection-object-summarizer/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 models
- **US1 (Phase 3)**: Depends on Phase 2 — can start tests immediately (mocked)
- **US2 (Phase 4)**: Depends on Phase 2 — can start tests immediately (mocked), independent of US1
- **US3 (Phase 5)**: Depends on US1 (type detection) + US2 (object summarizer)
- **Polish (Phase 6)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: Independent — can implement after Phase 2
- **US2 (P2)**: Independent — can implement after Phase 2, in parallel with US1
- **US3 (P3)**: Depends on US1 (ClassifyCollection) and US2 (ObjectSummarizer) — implement last

### Within Each User Story

1. Tests MUST be written and FAIL before implementation (Constitution III)
2. Models already exist from Phase 1
3. Service implementation in dependency order
4. Tool wrapper last (depends on service)
5. Verify tests pass before moving on

### Parallel Opportunities

- **Phase 1**: T001, T002, T003 — all parallel (different files)
- **Phase 2**: T004, T005 — parallel (different files); T006 after both
- **Phase 3 tests**: T007, T008, T009, T010, T011 — all parallel (same file but independent test classes/methods)
- **Phase 4 tests**: T021, T022, T023 — all parallel
- **US1 + US2**: Can run in parallel after Phase 2 (completely independent services/tools)

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel (different test methods, same file):
T007: "Unit tests for collection type detection"
T008: "Unit tests for array/list analysis"
T009: "Unit tests for numeric statistics and sampling"
T010: "Unit tests for dictionary analysis"
T011: "Unit tests for error cases"

# Then implement sequentially (same file, dependent logic):
T012 → T013 → T014 → T015 → T016 → T017 → T018 → T019 → T020
```

## Parallel Example: US1 + US2 Simultaneous

```bash
# After Phase 2 completes, both stories can proceed in parallel:
# Agent A: US1 (collection_analyze)
T007-T011 (tests) → T012-T020 (implementation)

# Agent B: US2 (object_summarize)
T021-T023 (tests) → T024-T027 (implementation)

# Then US3 after both complete:
T028-T030
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T006)
3. Complete Phase 3: User Story 1 (T007-T020)
4. **STOP and VALIDATE**: `collection_analyze` works for all collection types
5. Deploy — agents can already save 5-50+ tool calls per collection

### Incremental Delivery

1. Setup + Foundational → Models and interfaces ready
2. Add US1 (`collection_analyze`) → Test → Deploy (MVP!)
3. Add US2 (`object_summarize`) → Test → Deploy
4. Add US3 (inline collection counts) → Test → Deploy
5. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Constitution III requires TDD — write tests first, verify they fail, then implement
- All test tasks mock IDebugSessionManager — no live debugger needed for unit tests
- Contract tests (ToolAnnotationTests) auto-discover new tools — no new test file needed
- Commit after each phase or logical task group
- Stop at any checkpoint to validate independently
