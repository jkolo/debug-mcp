using System.Text.Json.Serialization;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>variables_get</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record VariablesGetResult(
    bool Success,
    IReadOnlyList<VariableResult>? Variables = null,
    ToolError? Error = null,
    TruncationInfo? Truncation = null);

/// <summary>
/// A single variable, as emitted by <c>variables_get</c> and (reused, minus <see cref="Path"/>)
/// by <c>stacktrace_get</c>'s per-frame <c>arguments</c> array.
/// </summary>
public sealed record VariableResult(
    string Name,
    string Type,
    string Value,
    string Scope,
    [property: JsonPropertyName("has_children")] bool HasChildren,
    [property: JsonPropertyName("children_count")] int? ChildrenCount = null,
    string? Path = null);
