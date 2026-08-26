using System.Text.Json.Serialization;
using DebugMcp.Models.CodeAnalysis;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>code_get_diagnostics</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record CodeGetDiagnosticsResult(
    bool Success,
    CodeGetDiagnosticsData? Data = null,
    ToolError? Error = null);

/// <summary>The <c>data</c> object for <c>code_get_diagnostics</c>.</summary>
public sealed record CodeGetDiagnosticsData
{
    [JsonPropertyName("total_count")]
    public required int TotalCount { get; init; }

    [JsonPropertyName("limited_to")]
    public required int LimitedTo { get; init; }

    /// <summary>Diagnostic count grouped by lowercased severity name (e.g. "error", "warning").</summary>
    public required IReadOnlyDictionary<string, int> Summary { get; init; }

    public required IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; }
}
