using System.Text.Json.Serialization;
using DebugMcp.Models.CodeAnalysis;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>code_find_assignments</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record CodeFindAssignmentsResult(
    bool Success,
    CodeFindAssignmentsData? Data = null,
    ToolError? Error = null,
    TruncationInfo? Truncation = null);

/// <summary>The <c>data</c> object for <c>code_find_assignments</c>.</summary>
public sealed record CodeFindAssignmentsData
{
    public required FindAssignmentsSymbolSummary Symbol { get; init; }

    [JsonPropertyName("assignments_count")]
    public required int AssignmentsCount { get; init; }

    public required IReadOnlyList<SymbolAssignment> Assignments { get; init; }
}

/// <summary>
/// The subset of <see cref="SymbolInfo"/> fields <c>code_find_assignments</c> exposes today —
/// name, fully_qualified_name, kind, containing_type, declaration_file, declaration_line only.
/// Unlike <see cref="SymbolInfo"/> it omits containing_namespace and declaration_column,
/// matching the legacy hand-rolled anonymous object exactly.
/// </summary>
public sealed record FindAssignmentsSymbolSummary
{
    public required string Name { get; init; }

    [JsonPropertyName("fully_qualified_name")]
    public required string FullyQualifiedName { get; init; }

    /// <summary>Legacy code computed this via <c>Kind.ToString()</c> (PascalCase, not lowercased).</summary>
    public required string Kind { get; init; }

    [JsonPropertyName("containing_type")]
    public string? ContainingType { get; init; }

    [JsonPropertyName("declaration_file")]
    public string? DeclarationFile { get; init; }

    [JsonPropertyName("declaration_line")]
    public int? DeclarationLine { get; init; }
}
