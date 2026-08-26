using DebugMcp.Models.CodeAnalysis;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>code_load</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
/// <remarks>
/// <see cref="Data"/> reuses <see cref="WorkspaceInfo"/> as-is — the legacy JSON serialized the
/// service's <c>WorkspaceInfo</c> directly under <c>data</c> with no reshaping, so it matches 1:1.
/// </remarks>
public sealed record CodeLoadResult(
    bool Success,
    WorkspaceInfo? Data = null,
    ToolError? Error = null);
