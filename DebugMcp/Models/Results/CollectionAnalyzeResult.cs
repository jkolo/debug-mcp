namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>collection_analyze</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record CollectionAnalyzeResult(
    bool Success,
    CollectionAnalyzeSummary? Summary = null,
    ToolError? Error = null);

/// <summary>The <c>summary</c> object nested under a successful <c>collection_analyze</c> result.</summary>
public sealed record CollectionAnalyzeSummary(
    int Count,
    string ElementType,
    string CollectionType,
    string Kind,
    int NullCount,
    CollectionNumericStats? NumericStats,
    IReadOnlyList<CollectionTypeCount>? TypeDistribution,
    IReadOnlyList<CollectionElementPreview> FirstElements,
    IReadOnlyList<CollectionElementPreview> LastElements,
    IReadOnlyList<CollectionKeyValuePreview>? KeyValuePairs,
    bool IsSampled);

public sealed record CollectionNumericStats(string Min, string Max, string Average);

public sealed record CollectionTypeCount(string TypeName, int Count);

public sealed record CollectionElementPreview(int Index, string Value, string Type);

public sealed record CollectionKeyValuePreview(string Key, string KeyType, string Value, string ValueType);
