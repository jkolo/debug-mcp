using System.Text.Json.Serialization;

namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Warning or error encountered during workspace loading.
/// </summary>
public sealed record WorkspaceDiagnostic
{
    /// <summary>
    /// Kind of diagnostic (Warning or Failure).
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>
    /// Description of the issue.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Affected project name (if applicable).
    /// </summary>
    [JsonPropertyName("project")]
    public string? Project { get; init; }
}
