namespace DebugMcp.Models.Results;

/// <summary>
/// Wire shape for <c>modules_search</c>. Field names preserved from the pre-US3 hand-rolled JSON
/// (FR-021). <c>modules_search</c> is one of the 14 FR-035 collection-returning tools, hence the
/// (currently unpopulated — wired in T054) <see cref="Truncation"/> field.
/// </summary>
/// <remarks>
/// Legacy note: the pre-migration serializer used <c>WriteIndented = true</c> only (no
/// null-omission), so <c>continuationToken</c> (and a type's <c>namespace</c> when null) were
/// emitted as literal JSON <c>null</c>. The SDK's structured-content serializer omits null
/// properties by default (confirmed on the pilot), so those keys are now absent instead of
/// null-valued when not applicable — a wire-visible but expected consequence of this migration
/// (flagged in the US3 T052 report).
/// </remarks>
public sealed record ModulesSearchResult(
    bool Success,
    string? Query = null,
    string? SearchType = null,
    IReadOnlyList<ModulesSearchTypeMatch>? Types = null,
    IReadOnlyList<ModulesSearchMethodMatch>? Methods = null,
    int? TotalMatches = null,
    int? ReturnedMatches = null,
    bool? Truncated = null,
    string? ContinuationToken = null,
    TruncationInfo? Truncation = null,
    ToolError? Error = null);

/// <summary>
/// A type that matched a <c>modules_search</c> query. <see cref="Namespace"/> is
/// nullable-with-default (reproduces legacy conditional omission for global-namespace types — the
/// requiredness pitfall applies recursively to nested wire types too, not just the top-level
/// result); it is placed last purely so C# accepts the default (field order is not part of the
/// wire contract).
/// </summary>
public sealed record ModulesSearchTypeMatch(
    string FullName,
    string Name,
    string Kind,
    string Visibility,
    string ModuleName,
    string? Namespace = null);

/// <summary>A method that matched a <c>modules_search</c> query.</summary>
public sealed record ModulesSearchMethodMatch(
    string DeclaringType,
    string ModuleName,
    string MatchReason,
    ModulesSearchMethodDetail Method);

/// <summary>Method detail nested under <see cref="ModulesSearchMethodMatch"/>.</summary>
public sealed record ModulesSearchMethodDetail(
    string Name,
    string Signature,
    string ReturnType,
    string Visibility,
    bool IsStatic);
