using System.Text.Json.Serialization;

namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Summary of a single project in the workspace.
/// </summary>
public sealed record ProjectInfo
{
    /// <summary>
    /// Project name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Absolute path to .csproj file.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Number of C# source files in the project.
    /// </summary>
    [JsonPropertyName("documents_count")]
    public required int DocumentsCount { get; init; }

    /// <summary>
    /// Target framework (e.g., "net10.0").
    /// </summary>
    [JsonPropertyName("target_framework")]
    public string? TargetFramework { get; init; }
}
