# Data Model: Collection & Object Summarizer

**Feature**: 028-collection-object-summarizer
**Date**: 2026-02-11

## Entities

All models are positional records (immutable), following the project convention.

### CollectionKind (Enum)

Classification of recognized collection types.

| Value | Description | Element Access Strategy |
|-------|-------------|------------------------|
| `Array` | T[] / multidimensional arrays | `GetElementAtPosition(i)` |
| `List` | `List<T>`, `ImmutableList<T>`, `ImmutableArray<T>` | Read `_items` field |
| `Dictionary` | `Dictionary<K,V>`, `SortedDictionary<K,V>`, `ConcurrentDictionary<K,V>` | Eval-based key/value access |
| `Set` | `HashSet<T>`, `SortedSet<T>` | Eval-based enumeration |
| `Queue` | `Queue<T>`, `Stack<T>` | Read `_array` field |
| `Other` | Any type with `Count` property | Eval-based |

### CollectionSummary

Result of `collection_analyze` operation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Count` | `int` | Yes | Total number of elements |
| `ElementType` | `string` | Yes | Element type name (e.g., "System.Int32") |
| `CollectionType` | `string` | Yes | Full type name of the collection |
| `Kind` | `CollectionKind` | Yes | Classification of collection type |
| `NullCount` | `int` | Yes | Number of null elements (0 for value types) |
| `NumericStats` | `NumericStatistics?` | No | Present only for numeric element types |
| `TypeDistribution` | `IReadOnlyList<TypeCount>?` | No | Present when elements have mixed runtime types |
| `FirstElements` | `IReadOnlyList<ElementPreview>` | Yes | First N elements (empty if collection empty) |
| `LastElements` | `IReadOnlyList<ElementPreview>` | Yes | Last N elements (empty if collection empty or N >= count) |
| `KeyValuePairs` | `IReadOnlyList<KeyValuePreview>?` | No | For dictionaries — first N key-value pairs |
| `IsSampled` | `bool` | Yes | True if statistics are approximate |

### NumericStatistics

Statistical summary for numeric collections.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Min` | `string` | Yes | Minimum value (formatted as string) |
| `Max` | `string` | Yes | Maximum value (formatted as string) |
| `Average` | `string` | Yes | Average value (formatted as string) |

### TypeCount

Entry in a type distribution.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `TypeName` | `string` | Yes | Runtime type name |
| `Count` | `int` | Yes | Number of elements of this type |

### ElementPreview

Preview of a single collection element.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Index` | `int` | Yes | Element index in the collection |
| `Value` | `string` | Yes | Formatted value (from `FormatValue`) |
| `Type` | `string` | Yes | Runtime type name |

### KeyValuePreview

Preview of a dictionary key-value pair.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Key` | `string` | Yes | Formatted key value |
| `KeyType` | `string` | Yes | Key type name |
| `Value` | `string` | Yes | Formatted value |
| `ValueType` | `string` | Yes | Value type name |

### ObjectSummary

Result of `object_summarize` operation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `TypeName` | `string` | Yes | Full type name |
| `Size` | `int` | Yes | Object size in bytes |
| `IsNull` | `bool` | Yes | True if the variable is null |
| `Fields` | `IReadOnlyList<FieldSummary>` | Yes | All non-null, non-default fields with values |
| `NullFields` | `IReadOnlyList<string>` | Yes | Names of fields that are null |
| `InterestingFields` | `IReadOnlyList<InterestingField>` | Yes | Fields with anomalous values |
| `InaccessibleFieldCount` | `int` | Yes | Count of fields that couldn't be read |
| `TotalFieldCount` | `int` | Yes | Total fields on the type (including null, inaccessible) |

### FieldSummary

Summary of a single object field.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | `string` | Yes | Field name |
| `Type` | `string` | Yes | Field type name |
| `Value` | `string` | Yes | Formatted value |
| `CollectionCount` | `int?` | No | If field is a collection, its element count |
| `CollectionElementType` | `string?` | No | If field is a collection, its element type |

### InterestingField

A field flagged for anomalous value.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | `string` | Yes | Field name |
| `Type` | `string` | Yes | Field type name |
| `Value` | `string` | Yes | Current value |
| `Reason` | `string` | Yes | Flag reason: `empty_string`, `nan`, `infinity`, `default_datetime`, `default_guid` |

## Relationships

```text
CollectionSummary
├── NumericStatistics? (1:0..1)
├── TypeCount* (1:0..N)
├── ElementPreview* (1:0..N, first + last)
└── KeyValuePreview* (1:0..N, dictionaries only)

ObjectSummary
├── FieldSummary* (1:0..N, valued fields)
└── InterestingField* (1:0..N, anomalous fields)
```

## Validation Rules

- `CollectionSummary.Count` >= 0
- `CollectionSummary.NullCount` >= 0 and <= `Count`
- `ElementPreview.Index` >= 0 and < `Count`
- `ObjectSummary.InaccessibleFieldCount` >= 0
- `ObjectSummary.TotalFieldCount` = `Fields.Count` + `NullFields.Count` + `InaccessibleFieldCount` + (fields in InterestingFields that aren't in Fields)
- `maxPreviewItems` parameter: 1–50, default 5
