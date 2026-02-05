using System.Text.Json.Serialization;

namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Definition location for a symbol.
/// </summary>
public sealed record SymbolDefinition
{
    /// <summary>
    /// Absolute path to source file, or null for metadata symbols.
    /// </summary>
    [JsonPropertyName("file")]
    public string? File { get; init; }

    /// <summary>
    /// 1-based line number, or null for metadata symbols.
    /// </summary>
    [JsonPropertyName("line")]
    public int? Line { get; init; }

    /// <summary>
    /// 1-based column number, or null for metadata symbols.
    /// </summary>
    [JsonPropertyName("column")]
    public int? Column { get; init; }

    /// <summary>
    /// 1-based end line.
    /// </summary>
    [JsonPropertyName("end_line")]
    public int? EndLine { get; init; }

    /// <summary>
    /// 1-based end column.
    /// </summary>
    [JsonPropertyName("end_column")]
    public int? EndColumn { get; init; }

    /// <summary>
    /// Whether this definition is in source code (vs. metadata).
    /// </summary>
    [JsonPropertyName("is_source")]
    public required bool IsSource { get; init; }

    /// <summary>
    /// Assembly name for metadata symbols.
    /// </summary>
    [JsonPropertyName("assembly_name")]
    public string? AssemblyName { get; init; }

    /// <summary>
    /// Assembly version for metadata symbols.
    /// </summary>
    [JsonPropertyName("assembly_version")]
    public string? AssemblyVersion { get; init; }
}

/// <summary>
/// Complete go-to-definition result including symbol info and all definition locations.
/// </summary>
public sealed record GoToDefinitionResult
{
    /// <summary>
    /// Information about the resolved symbol.
    /// </summary>
    [JsonPropertyName("symbol")]
    public required SymbolInfo Symbol { get; init; }

    /// <summary>
    /// All definition locations (may be multiple for partial classes).
    /// </summary>
    [JsonPropertyName("definitions")]
    public required IReadOnlyList<SymbolDefinition> Definitions { get; init; }
}
