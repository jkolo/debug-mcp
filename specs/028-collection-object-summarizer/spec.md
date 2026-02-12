# Feature Specification: Collection & Object Summarizer

**Feature Branch**: `028-collection-object-summarizer`
**Created**: 2026-02-11
**Status**: Draft
**Input**: User description: "Smart inspection for large objects: `collection_analyze` returns count, min, max, average for primitives; common types, null count, first/last N items for objects. `object_summarize` returns key fields, sizes, interesting flags (nulls, defaults, NaNs, empty strings). Prevents token blowup on large object graphs."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Summarize a Large Collection (Priority: P1)

An AI debugging agent is paused at a breakpoint where a local variable `orders` holds a `List<Order>` with 10,000 items. Today, the agent must call `variables_get` (sees `{System.Collections.Generic.List}` with `has_children: true`), then expand or evaluate individual elements — burning dozens of tool calls and thousands of tokens just to understand "what's in this list?" With `collection_analyze`, the agent makes one call and gets back: count, element type distribution, null count, first and last N elements as previews, and for numeric fields — min, max, and average. The agent immediately knows the shape of the data and can focus on the relevant slice.

**Why this priority**: This is the core value proposition. Collections are the most common source of token blowup — a single `List<T>` with 1,000 items can generate 50+ tool calls to understand. This story alone justifies the feature.

**Independent Test**: Can be fully tested by pausing at a breakpoint with a collection variable, calling `collection_analyze`, and verifying the summary is returned in a single response. Delivers immediate value without `object_summarize`.

**Acceptance Scenarios**:

1. **Given** the debugger is paused and a local variable is a `List<int>` with 100 elements, **When** the agent calls `collection_analyze("myList")`, **Then** the response includes: count (100), element type ("System.Int32"), min/max/average of the values, and the first and last 5 elements.
2. **Given** the debugger is paused and a local variable is a `Dictionary<string, Order>` with 500 entries, **When** the agent calls `collection_analyze("orders")`, **Then** the response includes: count (500), key type, value type, and a sample of the first 5 key-value pairs.
3. **Given** the debugger is paused and a local variable is an array of objects where 15 out of 200 are null, **When** the agent calls `collection_analyze("items")`, **Then** the response includes: count (200), null count (15), element type(s), and the first/last 5 non-null element previews.
4. **Given** the debugger is paused and a local variable is an empty `HashSet<string>`, **When** the agent calls `collection_analyze("tags")`, **Then** the response includes: count (0) and element type, with no element previews.

---

### User Story 2 - Summarize a Complex Object (Priority: P2)

An AI debugging agent is inspecting a large domain object (`CustomerProfile`) with 30+ fields, nested sub-objects, and several null references. Today, `object_inspect` returns all fields with raw values — the agent must parse through all of them to find what's interesting. With `object_summarize`, the agent gets a curated view: key fields (non-default, non-null values), fields that are null, fields with "interesting" values (empty strings, NaN, zero where non-zero is expected, extreme values), and a size estimate. The agent can immediately spot anomalies.

**Why this priority**: Complements collection analysis by handling the "wide object" problem. Less common than collections but equally wasteful when encountered — a 30-field object requires manual scanning of all fields to find the 3 that matter.

**Independent Test**: Can be fully tested by pausing at a breakpoint with a complex object, calling `object_summarize`, and verifying the curated summary highlights interesting fields. Works without `collection_analyze`.

**Acceptance Scenarios**:

1. **Given** the debugger is paused and a local variable is a `Customer` object with 20 fields where 5 are null, 2 are empty strings, and 1 is NaN, **When** the agent calls `object_summarize("customer")`, **Then** the response lists all non-default fields with values, a separate list of null fields, and flags for empty strings and NaN.
2. **Given** the debugger is paused and a local variable is a simple `Point` record with 2 fields, **When** the agent calls `object_summarize("point")`, **Then** the response includes all fields with values and the object size — no "interesting flags" section since nothing is anomalous.
3. **Given** the debugger is paused and a local variable is null, **When** the agent calls `object_summarize("obj")`, **Then** the response indicates the variable is null with its declared type.

---

### User Story 3 - Analyze Nested Collection Fields (Priority: P3)

An AI agent is inspecting an object that contains collection-typed fields (e.g., `Order.LineItems` is a `List<LineItem>`). When using `object_summarize`, collection fields are shown with their element count and type rather than just `{System.Collections.Generic.List}`. This gives the agent enough context to decide whether to drill into a specific collection without additional tool calls.

**Why this priority**: Bridges the two tools — object summarization becomes more useful when it recognizes collection fields and shows their counts inline. Lower priority because agents can always call `collection_analyze` separately on any field.

**Independent Test**: Can be tested by calling `object_summarize` on an object with collection-typed fields and verifying counts are shown inline.

**Acceptance Scenarios**:

1. **Given** the debugger is paused and a local variable is an `Order` with a `List<LineItem>` field containing 12 items, **When** the agent calls `object_summarize("order")`, **Then** the `LineItems` field shows type `List<LineItem>`, count 12, rather than raw `{System.Collections.Generic.List}`.
2. **Given** the debugger is paused and an object has a null collection field, **When** the agent calls `object_summarize("order")`, **Then** the collection field appears in the null fields list.

---

### Edge Cases

- What happens when the variable is not a collection and `collection_analyze` is called? The tool returns a clear error indicating the variable is not a recognized collection type.
- What happens when a collection contains mixed types (e.g., `List<object>` with strings, ints, and nulls)? The summary includes a type distribution breakdown (e.g., "String: 40, Int32: 35, null: 25").
- What happens when a collection is extremely large (100,000+ elements)? Statistical summaries (count, min/max/avg) are computed by sampling — the tool does not iterate every element. The response indicates whether results are exact or sampled.
- What happens when the process is running (not paused)? The tools return an error, same as other inspection tools.
- What happens when the variable expression is invalid or out of scope? The tools return an evaluation error with the same error codes as the existing `evaluate` tool.
- What happens when object fields cannot be read (e.g., optimized away in Release builds)? The summary includes only fields that are readable and notes the count of inaccessible fields.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `collection_analyze` operation that accepts a variable name or expression and returns a structured summary of the collection's contents in a single call.
- **FR-002**: The collection summary MUST include: element count, element type(s), null element count, and preview of first N and last N elements (where N defaults to 5).
- **FR-003**: For collections of numeric primitives (int, long, float, double, decimal), the summary MUST include min, max, and average values.
- **FR-004**: For collections with mixed element types, the summary MUST include a type distribution (type name and count for each distinct runtime type).
- **FR-005**: System MUST provide an `object_summarize` operation that accepts a variable name or expression and returns a curated view of the object's fields in a single call.
- **FR-006**: The object summary MUST include: type name, object size, non-default field values, list of null fields, and flags for interesting values (empty strings, NaN, infinity, default numeric values where non-default is expected).
- **FR-007**: When `object_summarize` encounters a field whose type is a recognized collection, it MUST show the collection's element count and element type inline rather than a raw type name.
- **FR-008**: Both tools MUST accept an optional `maxPreviewItems` parameter to control how many preview elements are returned (default: 5, max: 50).
- **FR-009**: Both tools MUST return errors consistent with the existing inspection tool error format (success/error with error codes) when the process is not paused, the variable is out of scope, or the expression is invalid.
- **FR-010**: Both tools MUST recognize standard .NET collection types: arrays, `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `SortedSet<T>`, `SortedDictionary<K,V>`, `ConcurrentDictionary<K,V>`, `ImmutableArray<T>`, `ImmutableList<T>`, and any type implementing `ICollection<T>` or `ICollection`.
- **FR-011**: For very large collections (configurable threshold), the system MUST use sampling rather than full iteration for statistical summaries, and indicate in the response whether results are exact or approximate.
- **FR-012**: The number of preview elements shown MUST be configurable via the `maxPreviewItems` parameter, allowing agents to request more or fewer items as needed.

### Key Entities

- **CollectionSummary**: Represents the analysis result for a collection — element count, element type(s), null count, type distribution, numeric statistics, first/last element previews, sampling indicator.
- **ObjectSummary**: Represents the curated view of an object — type name, size, categorized fields (valued, null, interesting), collection field inline summaries, inaccessible field count.
- **NumericStatistics**: Min, max, average for numeric collections. Present only when elements are numeric primitives.
- **TypeDistribution**: Mapping of runtime type names to counts. Present when a collection contains heterogeneous types.
- **FieldSummary**: A single field's name, type, value, and optional flags (null, empty, NaN, default, interesting).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An agent can understand the shape and contents of any collection variable in exactly 1 tool call, compared to the current 5-50+ calls depending on collection size.
- **SC-002**: An agent can identify anomalous fields in a complex object (nulls, empty strings, NaN, defaults) in exactly 1 tool call, compared to the current 2-5 calls (inspect + evaluate individual fields).
- **SC-003**: Summarizing a collection of 10,000 elements completes within 2 seconds — the tool does not scale linearly with collection size due to sampling.
- **SC-004**: The combined token output for summarizing a 1,000-element collection is under 500 tokens — a 90%+ reduction compared to expanding all elements individually.
- **SC-005**: All existing inspection workflows continue to work unchanged — the new tools are additive, not replacements.
