using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Memory;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Inspection;

/// <summary>
/// Summarizes objects in the debuggee, categorizing fields into valued, null, and interesting.
/// </summary>
public sealed class ObjectSummarizer : IObjectSummarizer
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<ObjectSummarizer> _logger;

    public ObjectSummarizer(IDebugSessionManager sessionManager, ILogger<ObjectSummarizer> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<ObjectSummary> SummarizeAsync(
        string expression,
        int maxPreviewItems = 5,
        int? threadId = null,
        int frameIndex = 0,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        maxPreviewItems = Math.Clamp(maxPreviewItems, 1, 50);

        // Step 1: Inspect the object to get fields
        var inspection = await _sessionManager.InspectObjectAsync(expression, 1, threadId, frameIndex, cancellationToken);

        // Handle null
        if (inspection.IsNull)
        {
            return new ObjectSummary(
                TypeName: inspection.TypeName,
                Size: 0,
                IsNull: true,
                Fields: [],
                NullFields: [],
                InterestingFields: [],
                InaccessibleFieldCount: 0,
                TotalFieldCount: 0);
        }

        var fields = new List<FieldSummary>();
        var nullFields = new List<string>();
        var interestingFields = new List<InterestingField>();
        var inaccessibleCount = 0;

        foreach (var field in inspection.Fields)
        {
            var cleanName = CleanFieldName(field.Name);

            // Skip null fields → add to nullFields list
            if (field.Value == "null")
            {
                nullFields.Add(cleanName);
                continue;
            }

            // Check for interesting values
            var reason = DetectInteresting(field.Value, field.TypeName);
            if (reason != null)
            {
                interestingFields.Add(new InterestingField(
                    Name: cleanName,
                    Type: field.TypeName,
                    Value: field.Value,
                    Reason: reason));
            }

            // Detect collection fields (US3)
            int? collectionCount = null;
            string? collectionElementType = null;
            var collectionKind = CollectionAnalyzer.ClassifyCollection(field.TypeName);
            if (collectionKind != null)
            {
                (collectionCount, collectionElementType) = await TryGetCollectionInfoAsync(
                    $"{expression}.{cleanName}",
                    field.TypeName, collectionKind.Value,
                    threadId, frameIndex, timeoutMs, cancellationToken);
            }

            fields.Add(new FieldSummary(
                Name: cleanName,
                Type: field.TypeName,
                Value: FormatCollectionValue(field.Value, field.TypeName, collectionKind, collectionCount, collectionElementType),
                CollectionCount: collectionCount,
                CollectionElementType: collectionElementType));
        }

        return new ObjectSummary(
            TypeName: inspection.TypeName,
            Size: inspection.Size,
            IsNull: false,
            Fields: fields,
            NullFields: nullFields,
            InterestingFields: interestingFields,
            InaccessibleFieldCount: inaccessibleCount,
            TotalFieldCount: fields.Count + nullFields.Count + inaccessibleCount);
    }

    /// <summary>
    /// Detects if a field value is "interesting" (likely anomalous).
    /// </summary>
    internal static string? DetectInteresting(string value, string typeName)
    {
        // Empty string
        if (value == "\"\"")
            return "empty_string";

        // NaN
        if (value == "NaN" && (typeName == "System.Single" || typeName == "System.Double"))
            return "nan";

        // Infinity
        if ((value == "Infinity" || value == "-Infinity" || value == "∞" || value == "-∞") &&
            (typeName == "System.Single" || typeName == "System.Double"))
            return "infinity";

        // Default DateTime/DateTimeOffset (0001-01-01)
        if ((typeName == "System.DateTime" || typeName == "System.DateTimeOffset") &&
            value.StartsWith("0001-01-01", StringComparison.Ordinal))
            return "default_datetime";

        // Guid.Empty
        if (typeName == "System.Guid" && value == "00000000-0000-0000-0000-000000000000")
            return "default_guid";

        return null;
    }

    private async Task<(int? Count, string? ElementType)> TryGetCollectionInfoAsync(
        string fieldExpression, string fieldTypeName, CollectionKind kind,
        int? threadId, int frameIndex, int timeoutMs, CancellationToken ct)
    {
        try
        {
            var countExpr = kind == CollectionKind.Array
                ? $"{fieldExpression}.Length"
                : $"{fieldExpression}.Count";

            var countResult = await _sessionManager.EvaluateAsync(countExpr, threadId, frameIndex, timeoutMs, ct);
            if (countResult.Success && int.TryParse(countResult.Value, out var count))
            {
                var elementType = ExtractElementTypeFromTypeName(fieldTypeName, kind);
                return (count, elementType);
            }
        }
        catch
        {
            // Non-critical — just skip collection info
        }

        return (null, null);
    }

    private static string ExtractElementTypeFromTypeName(string typeName, CollectionKind kind)
    {
        if (kind == CollectionKind.Array)
        {
            var bracketIdx = typeName.IndexOf('[');
            return bracketIdx > 0 ? typeName[..bracketIdx] : typeName;
        }

        var openBracket = typeName.IndexOf('[');
        var closeBracket = typeName.LastIndexOf(']');
        if (openBracket >= 0 && closeBracket > openBracket)
        {
            var inner = typeName[(openBracket + 1)..closeBracket];
            // For dictionaries, return value type (after comma)
            if (kind == CollectionKind.Dictionary)
            {
                var commaIdx = inner.IndexOf(',');
                return commaIdx >= 0 ? inner[(commaIdx + 1)..].Trim() : inner;
            }
            return inner;
        }

        return "System.Object";
    }

    private static string FormatCollectionValue(
        string originalValue, string typeName, CollectionKind? kind, int? count, string? elementType)
    {
        if (kind == null || count == null)
            return originalValue;

        var shortType = GetShortTypeName(typeName);
        return $"{shortType}[{count}]";
    }

    private static string GetShortTypeName(string fullTypeName)
    {
        // Strip namespace, keep generic form: System.Collections.Generic.List`1[System.Int32] → List<Int32>
        var backtickIdx = fullTypeName.IndexOf('`');
        var bracketIdx = fullTypeName.IndexOf('[');

        string baseName;
        if (backtickIdx >= 0)
        {
            var lastDot = fullTypeName.LastIndexOf('.', backtickIdx);
            baseName = lastDot >= 0 ? fullTypeName[(lastDot + 1)..backtickIdx] : fullTypeName[..backtickIdx];
        }
        else
        {
            var lastDot = fullTypeName.LastIndexOf('.');
            baseName = lastDot >= 0 ? fullTypeName[(lastDot + 1)..] : fullTypeName;
            if (baseName.Contains("[]"))
                return baseName;
        }

        if (bracketIdx >= 0)
        {
            var closeBracket = fullTypeName.LastIndexOf(']');
            if (closeBracket > bracketIdx)
            {
                var inner = fullTypeName[(bracketIdx + 1)..closeBracket];
                var shortInner = string.Join(", ", inner.Split(',').Select(t =>
                {
                    var trimmed = t.Trim();
                    var dot = trimmed.LastIndexOf('.');
                    return dot >= 0 ? trimmed[(dot + 1)..] : trimmed;
                }));
                return $"{baseName}<{shortInner}>";
            }
        }

        return baseName;
    }

    /// <summary>
    /// Strips auto-property backing field markers: "&lt;Name&gt;k__BackingField" → "Name"
    /// </summary>
    private static string CleanFieldName(string fieldName)
    {
        if (fieldName.StartsWith('<') && fieldName.Contains(">k__BackingField"))
            return fieldName[1..fieldName.IndexOf('>')];

        // Also handle underscore-prefixed private fields: _name → name for property access
        if (fieldName.StartsWith('_') && fieldName.Length > 1)
        {
            var propName = char.ToUpperInvariant(fieldName[1]) + fieldName[2..];
            return propName;
        }

        return fieldName;
    }
}
