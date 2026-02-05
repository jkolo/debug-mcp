using System.Text.Json.Serialization;

namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Information about a code symbol found via analysis.
/// </summary>
public sealed record SymbolInfo
{
    /// <summary>
    /// Symbol name (without namespace).
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Fully qualified name including namespace.
    /// </summary>
    [JsonPropertyName("fully_qualified_name")]
    public required string FullyQualifiedName { get; init; }

    /// <summary>
    /// Kind of symbol (Type, Method, Property, etc.).
    /// </summary>
    [JsonPropertyName("kind")]
    public required SymbolKind Kind { get; init; }

    /// <summary>
    /// Containing type name, if applicable.
    /// </summary>
    [JsonPropertyName("containing_type")]
    public string? ContainingType { get; init; }

    /// <summary>
    /// Containing namespace.
    /// </summary>
    [JsonPropertyName("containing_namespace")]
    public string? ContainingNamespace { get; init; }

    /// <summary>
    /// File where symbol is declared.
    /// </summary>
    [JsonPropertyName("declaration_file")]
    public string? DeclarationFile { get; init; }

    /// <summary>
    /// Line where symbol is declared (1-based).
    /// </summary>
    [JsonPropertyName("declaration_line")]
    public int? DeclarationLine { get; init; }

    /// <summary>
    /// Column where symbol is declared (1-based).
    /// </summary>
    [JsonPropertyName("declaration_column")]
    public int? DeclarationColumn { get; init; }

    /// <summary>
    /// Internal Roslyn symbol reference for further operations.
    /// Not serialized to JSON.
    /// </summary>
    [JsonIgnore]
    public object? RoslynSymbol { get; init; }
}
