using System.Text.Json.Serialization;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>evaluate</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record EvaluateResult(
    bool Success,
    string? Value = null,
    string? Type = null,
    [property: JsonPropertyName("has_children")] bool? HasChildren = null,
    ToolError? Error = null);
