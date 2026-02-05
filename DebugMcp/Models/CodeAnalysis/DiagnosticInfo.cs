using System.Text.Json.Serialization;

namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Information about a compilation diagnostic (error or warning).
/// </summary>
public sealed record DiagnosticInfo
{
    /// <summary>
    /// Diagnostic code (e.g., "CS0001", "CA1000").
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable diagnostic message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Severity level (Error, Warning, Info, Hidden).
    /// </summary>
    [JsonPropertyName("severity")]
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Category (e.g., "Compiler", "Microsoft.CodeAnalysis.Analyzers").
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>
    /// Absolute path to source file, if applicable.
    /// </summary>
    [JsonPropertyName("file")]
    public string? File { get; init; }

    /// <summary>
    /// 1-based line number, if applicable.
    /// </summary>
    [JsonPropertyName("line")]
    public int? Line { get; init; }

    /// <summary>
    /// 1-based column number, if applicable.
    /// </summary>
    [JsonPropertyName("column")]
    public int? Column { get; init; }

    /// <summary>
    /// 1-based end line, if applicable.
    /// </summary>
    [JsonPropertyName("end_line")]
    public int? EndLine { get; init; }

    /// <summary>
    /// 1-based end column, if applicable.
    /// </summary>
    [JsonPropertyName("end_column")]
    public int? EndColumn { get; init; }

    /// <summary>
    /// Project name where diagnostic occurred.
    /// </summary>
    [JsonPropertyName("project")]
    public string? Project { get; init; }

    /// <summary>
    /// Help link URL for more information about the diagnostic.
    /// </summary>
    [JsonPropertyName("help_link")]
    public string? HelpLink { get; init; }
}
