using System.Text.Json.Serialization;
using DebugMcp.Models.CodeAnalysis;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>code_find_usages</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record CodeFindUsagesResult(
    bool Success,
    CodeFindUsagesData? Data = null,
    ToolError? Error = null,
    TruncationInfo? Truncation = null);

/// <summary>The <c>data</c> object for <c>code_find_usages</c>.</summary>
public sealed record CodeFindUsagesData
{
    public required FindUsagesSymbolSummary Symbol { get; init; }

    [JsonPropertyName("usages_count")]
    public required int UsagesCount { get; init; }

    public required IReadOnlyList<SymbolUsage> Usages { get; init; }
}

/// <summary>
/// Mirrors every field of <see cref="SymbolInfo"/> (name, fully_qualified_name, kind,
/// containing_type, containing_namespace, declaration_file, declaration_line,
/// declaration_column) — the legacy anonymous object exposed the full symbol shape here, just
/// with <c>Kind</c> pre-rendered via <c>.ToString()</c> instead of the raw enum.
/// </summary>
public sealed record FindUsagesSymbolSummary
{
    public required string Name { get; init; }

    [JsonPropertyName("fully_qualified_name")]
    public required string FullyQualifiedName { get; init; }

    /// <summary>Legacy code computed this via <c>Kind.ToString()</c> (PascalCase, not lowercased).</summary>
    public required string Kind { get; init; }

    [JsonPropertyName("containing_type")]
    public string? ContainingType { get; init; }

    [JsonPropertyName("containing_namespace")]
    public string? ContainingNamespace { get; init; }

    [JsonPropertyName("declaration_file")]
    public string? DeclarationFile { get; init; }

    [JsonPropertyName("declaration_line")]
    public int? DeclarationLine { get; init; }

    [JsonPropertyName("declaration_column")]
    public int? DeclarationColumn { get; init; }
}
