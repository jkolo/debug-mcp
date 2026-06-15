using System.Text.Json.Serialization;

namespace DebugMcp.Models.ReSharper;

/// <summary>
/// A single ReSharper inspection issue. Field layout mirrors <c>DiagnosticInfo</c> (Roslyn) so
/// AI consumers see a consistent shape across both analysis backends. Severity is reported in
/// ReSharper's native vocabulary, verbatim.
/// </summary>
public sealed record InspectionFinding
{
    /// <summary>ReSharper inspection/rule id (e.g. "RedundantCast").</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Human-readable issue message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Native ReSharper severity (verbatim, lower-cased in JSON).</summary>
    [JsonPropertyName("severity")]
    [JsonConverter(typeof(ReSharperSeverityJsonConverter))]
    public required ReSharperSeverity Severity { get; init; }

    /// <summary>Inspection category/group, if provided by the engine.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Absolute source file path; null for non-file-scoped (solution-level) issues.</summary>
    [JsonPropertyName("file")]
    public string? File { get; init; }

    /// <summary>1-based start line, if the finding has a physical location.</summary>
    [JsonPropertyName("line")]
    public int? Line { get; init; }

    /// <summary>1-based start column, if available.</summary>
    [JsonPropertyName("column")]
    public int? Column { get; init; }

    /// <summary>1-based end line, if available.</summary>
    [JsonPropertyName("end_line")]
    public int? EndLine { get; init; }

    /// <summary>1-based end column, if available.</summary>
    [JsonPropertyName("end_column")]
    public int? EndColumn { get; init; }

    /// <summary>Originating project name, where known.</summary>
    [JsonPropertyName("project")]
    public string? Project { get; init; }

    /// <summary>Rule help URL, if provided.</summary>
    [JsonPropertyName("help_link")]
    public string? HelpLink { get; init; }
}
