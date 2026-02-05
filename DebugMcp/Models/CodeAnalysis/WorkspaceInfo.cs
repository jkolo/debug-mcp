using System.Text.Json.Serialization;

namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Represents a loaded solution or project with summary statistics.
/// </summary>
public sealed record WorkspaceInfo
{
    /// <summary>
    /// Absolute path to .sln or .csproj file.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Type of workspace (Solution or Project).
    /// </summary>
    [JsonPropertyName("type")]
    public required WorkspaceType Type { get; init; }

    /// <summary>
    /// List of loaded projects.
    /// </summary>
    [JsonPropertyName("projects")]
    public required IReadOnlyList<ProjectInfo> Projects { get; init; }

    /// <summary>
    /// Warnings/errors from loading.
    /// </summary>
    [JsonPropertyName("diagnostics")]
    public required IReadOnlyList<WorkspaceDiagnostic> Diagnostics { get; init; }

    /// <summary>
    /// Timestamp when workspace was loaded.
    /// </summary>
    [JsonPropertyName("loaded_at")]
    public required DateTimeOffset LoadedAt { get; init; }
}
