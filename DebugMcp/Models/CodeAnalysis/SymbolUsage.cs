using System.Text.Json.Serialization;

namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// A location where a symbol is referenced.
/// </summary>
public sealed record SymbolUsage
{
    /// <summary>
    /// Absolute path to source file.
    /// </summary>
    [JsonPropertyName("file")]
    public required string File { get; init; }

    /// <summary>
    /// 1-based line number.
    /// </summary>
    [JsonPropertyName("line")]
    public required int Line { get; init; }

    /// <summary>
    /// 1-based column number.
    /// </summary>
    [JsonPropertyName("column")]
    public required int Column { get; init; }

    /// <summary>
    /// 1-based end line.
    /// </summary>
    [JsonPropertyName("end_line")]
    public required int EndLine { get; init; }

    /// <summary>
    /// 1-based end column.
    /// </summary>
    [JsonPropertyName("end_column")]
    public required int EndColumn { get; init; }

    /// <summary>
    /// Containing member name (method, property, etc.).
    /// </summary>
    [JsonPropertyName("context")]
    public string? Context { get; init; }

    /// <summary>
    /// Kind of usage (Read, Write, Declaration, Reference).
    /// </summary>
    [JsonPropertyName("kind")]
    public required UsageKind Kind { get; init; }
}
