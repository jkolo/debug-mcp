using System.Globalization;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Memory;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Inspection;

/// <summary>
/// Analyzes collections in the debuggee using IDebugSessionManager for element access.
/// </summary>
public sealed class CollectionAnalyzer : ICollectionAnalyzer
{
    private const int SamplingThreshold = 1000;

    private static readonly Dictionary<string, CollectionKind> KnownCollectionPrefixes = new()
    {
        ["System.Collections.Generic.List`1"] = CollectionKind.List,
        ["System.Collections.Generic.Dictionary`2"] = CollectionKind.Dictionary,
        ["System.Collections.Generic.HashSet`1"] = CollectionKind.Set,
        ["System.Collections.Generic.SortedSet`1"] = CollectionKind.Set,
        ["System.Collections.Generic.Queue`1"] = CollectionKind.Queue,
        ["System.Collections.Generic.Stack`1"] = CollectionKind.Queue,
        ["System.Collections.Generic.LinkedList`1"] = CollectionKind.Stack,
        ["System.Collections.Generic.SortedDictionary`2"] = CollectionKind.Dictionary,
        ["System.Collections.Concurrent.ConcurrentDictionary`2"] = CollectionKind.Dictionary,
        ["System.Collections.Immutable.ImmutableArray`1"] = CollectionKind.List,
        ["System.Collections.Immutable.ImmutableList`1"] = CollectionKind.List,
    };

    private static readonly HashSet<string> NumericTypes = new()
    {
        "System.Byte", "System.SByte",
        "System.Int16", "System.UInt16",
        "System.Int32", "System.UInt32",
        "System.Int64", "System.UInt64",
        "System.Single", "System.Double", "System.Decimal",
    };

    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<CollectionAnalyzer> _logger;

    public CollectionAnalyzer(IDebugSessionManager sessionManager, ILogger<CollectionAnalyzer> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<CollectionSummary> AnalyzeAsync(
        string expression,
        int maxPreviewItems = 5,
        int? threadId = null,
        int frameIndex = 0,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        maxPreviewItems = Math.Clamp(maxPreviewItems, 1, 50);

        // Step 1: Evaluate the expression to get type info
        var evalResult = await _sessionManager.EvaluateAsync(expression, threadId, frameIndex, timeoutMs, cancellationToken);
        if (!evalResult.Success)
            throw new InvalidOperationException(evalResult.Error?.Message ?? $"Failed to evaluate '{expression}'.");

        var typeName = evalResult.Type ?? "unknown";

        // Detect collection kind
        var kind = ClassifyCollection(typeName);
        if (kind == null)
            throw new InvalidOperationException(
                $"Expression '{expression}' evaluates to type '{typeName}', which is not a recognized collection type. Use object_summarize instead.");

        // Step 2: Get count
        var count = await GetCountAsync(expression, kind.Value, threadId, frameIndex, timeoutMs, cancellationToken);

        // Step 3: Get element type
        var elementType = ExtractElementType(typeName, kind.Value);

        // Step 4: For dictionaries, get key-value pairs
        if (kind == CollectionKind.Dictionary)
        {
            var kvPairs = await GetKeyValuePairsAsync(expression, maxPreviewItems, threadId, frameIndex, timeoutMs, cancellationToken);
            return new CollectionSummary(
                Count: count,
                ElementType: elementType,
                CollectionType: typeName,
                Kind: kind.Value,
                NullCount: 0,
                NumericStats: null,
                TypeDistribution: null,
                FirstElements: [],
                LastElements: [],
                KeyValuePairs: kvPairs,
                IsSampled: false);
        }

        // Step 5: For other collections, enumerate elements
        var isSampled = false;
        var enumLimit = count;
        if (count > SamplingThreshold)
        {
            enumLimit = SamplingThreshold;
            isSampled = true;
        }

        var (firstElements, lastElements, nullCount, typeDistribution, numericStats) =
            await EnumerateElementsAsync(expression, kind.Value, count, enumLimit, maxPreviewItems,
                elementType, threadId, frameIndex, timeoutMs, cancellationToken);

        return new CollectionSummary(
            Count: count,
            ElementType: elementType,
            CollectionType: typeName,
            Kind: kind.Value,
            NullCount: nullCount,
            NumericStats: numericStats,
            TypeDistribution: typeDistribution?.Count > 1 ? typeDistribution : null,
            FirstElements: firstElements,
            LastElements: lastElements,
            KeyValuePairs: null,
            IsSampled: isSampled);
    }

    /// <summary>
    /// Classifies a type name as a collection kind, or returns null if not a collection.
    /// </summary>
    internal static CollectionKind? ClassifyCollection(string typeName)
    {
        if (typeName.Contains("[]"))
            return CollectionKind.Array;

        foreach (var (prefix, kind) in KnownCollectionPrefixes)
        {
            if (typeName.StartsWith(prefix, StringComparison.Ordinal))
                return kind;
        }

        return null;
    }

    /// <summary>
    /// Classifies a type, falling back to eval-based Count property detection.
    /// </summary>
    internal async Task<CollectionKind?> ClassifyCollectionWithFallbackAsync(
        string typeName, string expression, int? threadId, int frameIndex, int timeoutMs, CancellationToken ct)
    {
        var kind = ClassifyCollection(typeName);
        if (kind != null) return kind;

        // Fallback: try to access .Count property
        var countResult = await _sessionManager.EvaluateAsync($"{expression}.Count", threadId, frameIndex, timeoutMs, ct);
        if (countResult.Success)
            return CollectionKind.Other;

        return null;
    }

    private async Task<int> GetCountAsync(
        string expression, CollectionKind kind, int? threadId, int frameIndex, int timeoutMs, CancellationToken ct)
    {
        if (kind == CollectionKind.Array)
        {
            var lengthResult = await _sessionManager.EvaluateAsync($"{expression}.Length", threadId, frameIndex, timeoutMs, ct);
            if (lengthResult.Success && int.TryParse(lengthResult.Value, out var length))
                return length;
        }

        var countResult = await _sessionManager.EvaluateAsync($"{expression}.Count", threadId, frameIndex, timeoutMs, ct);
        if (countResult.Success && int.TryParse(countResult.Value, out var count))
            return count;

        return 0;
    }

    private static string ExtractElementType(string collectionType, CollectionKind kind)
    {
        if (kind == CollectionKind.Array)
        {
            var bracketIdx = collectionType.IndexOf('[');
            return bracketIdx > 0 ? collectionType[..bracketIdx] : collectionType;
        }

        // For generics like System.Collections.Generic.List`1[System.Int32]
        var openBracket = collectionType.IndexOf('[');
        var closeBracket = collectionType.LastIndexOf(']');
        if (openBracket >= 0 && closeBracket > openBracket)
        {
            var inner = collectionType[(openBracket + 1)..closeBracket];
            // For Dictionary<K,V>, return the full KeyValuePair type
            if (kind == CollectionKind.Dictionary && inner.Contains(','))
            {
                return $"System.Collections.Generic.KeyValuePair`2[{inner}]";
            }
            return inner;
        }

        return "System.Object";
    }

    private async Task<IReadOnlyList<KeyValuePreview>> GetKeyValuePairsAsync(
        string expression, int maxItems, int? threadId, int frameIndex, int timeoutMs, CancellationToken ct)
    {
        var pairs = new List<KeyValuePreview>();

        // Use LINQ ElementAt to access dictionary entries
        for (int i = 0; i < maxItems; i++)
        {
            var entryExpr = $"System.Linq.Enumerable.ElementAt({expression}, {i})";
            var entryResult = await _sessionManager.EvaluateAsync(entryExpr, threadId, frameIndex, timeoutMs, ct);
            if (entryResult == null || !entryResult.Success)
                break;

            // Get Key and Value from KeyValuePair
            var keyResult = await _sessionManager.EvaluateAsync($"{entryExpr}.Key", threadId, frameIndex, timeoutMs, ct);
            var valueResult = await _sessionManager.EvaluateAsync($"{entryExpr}.Value", threadId, frameIndex, timeoutMs, ct);

            if (keyResult.Success && valueResult.Success)
            {
                pairs.Add(new KeyValuePreview(
                    Key: keyResult.Value ?? "null",
                    KeyType: keyResult.Type ?? "unknown",
                    Value: valueResult.Value ?? "null",
                    ValueType: valueResult.Type ?? "unknown"));
            }
            else
            {
                break;
            }
        }

        return pairs;
    }

    private async Task<(IReadOnlyList<ElementPreview> First, IReadOnlyList<ElementPreview> Last, int NullCount,
        IReadOnlyList<TypeCount>? TypeDistribution, NumericStatistics? NumericStats)>
        EnumerateElementsAsync(
            string expression, CollectionKind kind, int totalCount, int enumLimit, int maxPreviewItems,
            string elementType, int? threadId, int frameIndex, int timeoutMs, CancellationToken ct)
    {
        if (totalCount == 0)
            return ([], [], 0, null, null);

        var firstElements = new List<ElementPreview>();
        var lastElements = new List<ElementPreview>();
        var nullCount = 0;
        var typeCounts = new Dictionary<string, int>();
        var isNumeric = NumericTypes.Contains(elementType);
        double? min = null, max = null;
        double sum = 0;
        int numericCount = 0;

        // Enumerate first N elements
        var firstLimit = Math.Min(maxPreviewItems, totalCount);
        for (int i = 0; i < firstLimit; i++)
        {
            var elem = await GetElementAtAsync(expression, kind, i, threadId, frameIndex, timeoutMs, ct);
            if (elem == null) continue;

            firstElements.Add(new ElementPreview(i, elem.Value.Value, elem.Value.Type));

            TrackElement(elem.Value.Value, elem.Value.Type, ref nullCount, typeCounts,
                isNumeric, ref min, ref max, ref sum, ref numericCount);
        }

        // If we need stats from more elements (between firstLimit and enumLimit)
        if (isNumeric || elementType == "System.Object")
        {
            for (int i = firstLimit; i < enumLimit && i < totalCount; i++)
            {
                var elem = await GetElementAtAsync(expression, kind, i, threadId, frameIndex, timeoutMs, ct);
                if (elem == null) continue;

                TrackElement(elem.Value.Value, elem.Value.Type, ref nullCount, typeCounts,
                    isNumeric, ref min, ref max, ref sum, ref numericCount);
            }
        }

        // Enumerate last N elements (if different from first N)
        if (totalCount > maxPreviewItems)
        {
            var lastStart = Math.Max(totalCount - maxPreviewItems, maxPreviewItems);
            for (int i = lastStart; i < totalCount; i++)
            {
                var elem = await GetElementAtAsync(expression, kind, i, threadId, frameIndex, timeoutMs, ct);
                if (elem == null) continue;

                lastElements.Add(new ElementPreview(i, elem.Value.Value, elem.Value.Type));

                if (i >= enumLimit) // Only track if we haven't already
                {
                    TrackElement(elem.Value.Value, elem.Value.Type, ref nullCount, typeCounts,
                        isNumeric, ref min, ref max, ref sum, ref numericCount);
                }
            }
        }

        // Build type distribution
        IReadOnlyList<TypeCount>? typeDistribution = null;
        if (typeCounts.Count > 1)
        {
            typeDistribution = typeCounts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new TypeCount(kv.Key, kv.Value))
                .ToList();
        }

        // Build numeric stats
        NumericStatistics? numericStats = null;
        if (isNumeric && numericCount > 0 && min.HasValue && max.HasValue)
        {
            numericStats = new NumericStatistics(
                Min: FormatNumber(min.Value),
                Max: FormatNumber(max.Value),
                Average: FormatNumber(sum / numericCount));
        }

        return (firstElements, lastElements, nullCount, typeDistribution, numericStats);
    }

    private async Task<(string Value, string Type)?> GetElementAtAsync(
        string expression, CollectionKind kind, int index, int? threadId, int frameIndex, int timeoutMs, CancellationToken ct)
    {
        var indexExpr = kind == CollectionKind.Array || kind == CollectionKind.List
            ? $"{expression}[{index}]"
            : $"System.Linq.Enumerable.ElementAt({expression}, {index})";

        var result = await _sessionManager.EvaluateAsync(indexExpr, threadId, frameIndex, timeoutMs, ct);
        if (!result.Success)
            return null;

        return (result.Value ?? "null", result.Type ?? "null");
    }

    private static void TrackElement(
        string value, string type,
        ref int nullCount, Dictionary<string, int> typeCounts,
        bool isNumeric, ref double? min, ref double? max, ref double sum, ref int numericCount)
    {
        if (value == "null")
        {
            nullCount++;
            typeCounts["null"] = typeCounts.GetValueOrDefault("null") + 1;
            return;
        }

        typeCounts[type] = typeCounts.GetValueOrDefault(type) + 1;

        if (isNumeric && double.TryParse(value, CultureInfo.InvariantCulture, out var numValue) && !double.IsNaN(numValue))
        {
            if (!min.HasValue || numValue < min.Value) min = numValue;
            if (!max.HasValue || numValue > max.Value) max = numValue;
            sum += numValue;
            numericCount++;
        }
    }

    private static string FormatNumber(double value)
    {
        if (value == Math.Floor(value) && !double.IsInfinity(value))
            return value.ToString("F0", CultureInfo.InvariantCulture);
        return value.ToString("G", CultureInfo.InvariantCulture);
    }
}
