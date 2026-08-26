using DebugMcp.Models.Modules;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>types_get</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record TypesGetResult(
    bool Success,
    string? ModuleName = null,
    string? NamespaceFilter = null,
    IReadOnlyList<TypeSummaryInfo>? Types = null,
    IReadOnlyList<NamespaceNode>? Namespaces = null,
    int? TotalCount = null,
    int? ReturnedCount = null,
    bool? Truncated = null,
    string? ContinuationToken = null,
    ToolError? Error = null,
    TruncationInfo? Truncation = null);

/// <summary>
/// A type summary. <see cref="Kind"/>/<see cref="Visibility"/> hold the same lowercased strings the
/// legacy tool computed rather than the <see cref="DebugMcp.Models.Modules.TypeKind"/>/
/// <see cref="DebugMcp.Models.Modules.Visibility"/> enums directly.
/// <see cref="DebugMcp.Models.Modules.NamespaceNode"/> is reused as-is for <c>namespaces</c> — it
/// already matches the legacy shape field-for-field with no enums involved.
/// </summary>
public sealed record TypeSummaryInfo(
    string FullName,
    string Name,
    string? Namespace,
    string Kind,
    string Visibility,
    bool IsGeneric,
    string[]? GenericParameters,
    bool IsNested,
    string? DeclaringType,
    string ModuleName,
    string? BaseType,
    string[]? Interfaces);
