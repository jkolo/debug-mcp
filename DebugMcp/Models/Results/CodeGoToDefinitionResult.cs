using System.Text.Json.Serialization;
using DebugMcp.Models.CodeAnalysis;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>code_goto_definition</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record CodeGoToDefinitionResult(
    bool Success,
    CodeGoToDefinitionData? Data = null,
    ToolError? Error = null);

/// <summary>The <c>data</c> object for <c>code_goto_definition</c>.</summary>
public sealed record CodeGoToDefinitionData
{
    public required GoToDefinitionSymbolSummary Symbol { get; init; }

    [JsonPropertyName("definitions_count")]
    public required int DefinitionsCount { get; init; }

    public required IReadOnlyList<SymbolDefinition> Definitions { get; init; }
}

/// <summary>
/// The subset of <see cref="SymbolInfo"/> fields <c>code_goto_definition</c> exposes today —
/// name, fully_qualified_name, kind, containing_type, containing_namespace only. Unlike
/// <see cref="SymbolInfo"/> it deliberately omits declaration_file/line/column, matching the
/// legacy hand-rolled anonymous object exactly.
/// </summary>
public sealed record GoToDefinitionSymbolSummary
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
}
