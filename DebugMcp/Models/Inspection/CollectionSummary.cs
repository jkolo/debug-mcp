namespace DebugMcp.Models.Inspection;

/// <summary>
/// Result of analyzing a collection via collection_analyze.
/// </summary>
public sealed record CollectionSummary(
    int Count,
    string ElementType,
    string CollectionType,
    CollectionKind Kind,
    int NullCount,
    NumericStatistics? NumericStats,
    IReadOnlyList<TypeCount>? TypeDistribution,
    IReadOnlyList<ElementPreview> FirstElements,
    IReadOnlyList<ElementPreview> LastElements,
    IReadOnlyList<KeyValuePreview>? KeyValuePairs,
    bool IsSampled);

/// <summary>
/// Statistical summary for numeric collections (min, max, average).
/// </summary>
public sealed record NumericStatistics(
    string Min,
    string Max,
    string Average);

/// <summary>
/// Entry in a type distribution — maps a runtime type name to its count.
/// </summary>
public sealed record TypeCount(
    string TypeName,
    int Count);

/// <summary>
/// Preview of a single collection element.
/// </summary>
public sealed record ElementPreview(
    int Index,
    string Value,
    string Type);

/// <summary>
/// Preview of a dictionary key-value pair.
/// </summary>
public sealed record KeyValuePreview(
    string Key,
    string KeyType,
    string Value,
    string ValueType);
