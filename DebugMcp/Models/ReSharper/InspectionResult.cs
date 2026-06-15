using System.Text.Json.Serialization;

namespace DebugMcp.Models.ReSharper;

/// <summary>
/// Outcome of one ReSharper inspection run — the <c>data</c> payload of a tool success response.
/// </summary>
public sealed record InspectionResult
{
    /// <summary>Absolute path of the inspected .sln/.csproj.</summary>
    [JsonPropertyName("target")]
    public required string Target { get; init; }

    /// <summary>Findings, capped to <see cref="MaxResults"/>.</summary>
    [JsonPropertyName("findings")]
    public required IReadOnlyList<InspectionFinding> Findings { get; init; }

    /// <summary>Pre-cap count of findings at/above the requested severity.</summary>
    [JsonPropertyName("total_count")]
    public required int TotalCount { get; init; }

    /// <summary>Number of findings actually returned (post-cap).</summary>
    [JsonPropertyName("returned_count")]
    public required int ReturnedCount { get; init; }

    /// <summary>True when <see cref="TotalCount"/> exceeded the cap and findings were dropped.</summary>
    [JsonPropertyName("truncated")]
    public required bool Truncated { get; init; }

    /// <summary>The cap applied to the returned findings.</summary>
    [JsonPropertyName("limited_to")]
    public required int MaxResults { get; init; }

    /// <summary>Count by native severity over the returned set (e.g. {"warning":3,"suggestion":12}).</summary>
    [JsonPropertyName("summary")]
    public required IReadOnlyDictionary<string, int> Summary { get; init; }

    /// <summary>Pinned engine version used for this run.</summary>
    [JsonPropertyName("engine_version")]
    public required string EngineVersion { get; init; }

    /// <summary>Inspection wall-clock in milliseconds (excludes one-time engine acquisition).</summary>
    [JsonPropertyName("duration_ms")]
    public required long DurationMs { get; init; }

    /// <summary>Whether the engine built the target before analysis (false when no-build).</summary>
    [JsonPropertyName("built")]
    public required bool Built { get; init; }
}
