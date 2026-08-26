namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>members_get</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record MembersGetResult(
    bool Success,
    string? TypeName = null,
    IReadOnlyList<MemberMethodInfo>? Methods = null,
    IReadOnlyList<MemberPropertyInfo>? Properties = null,
    IReadOnlyList<MemberFieldInfo>? Fields = null,
    IReadOnlyList<MemberEventInfo>? Events = null,
    bool? IncludesInherited = null,
    int? MethodCount = null,
    int? PropertyCount = null,
    int? FieldCount = null,
    int? EventCount = null,
    ToolError? Error = null,
    TruncationInfo? Truncation = null);

/// <summary>
/// A method member. <see cref="Visibility"/> holds the same lowercased string the legacy tool
/// computed (<c>m.Visibility.ToString().ToLowerInvariant()</c>) rather than
/// <see cref="DebugMcp.Models.Modules.Visibility"/> directly.
/// </summary>
public sealed record MemberMethodInfo(
    string Name,
    string Signature,
    string ReturnType,
    IReadOnlyList<MemberParameterInfo> Parameters,
    string Visibility,
    bool IsStatic,
    bool IsVirtual,
    bool IsAbstract,
    bool IsGeneric,
    string[]? GenericParameters,
    string DeclaringType);

/// <summary>A method parameter (full shape — distinct from <see cref="MemberIndexerParameterInfo"/>).</summary>
public sealed record MemberParameterInfo(
    string Name,
    string Type,
    bool IsOptional,
    bool IsOut,
    bool IsRef,
    string? DefaultValue = null);

public sealed record MemberPropertyInfo(
    string Name,
    string Type,
    string Visibility,
    bool IsStatic,
    bool HasGetter,
    bool HasSetter,
    string? GetterVisibility,
    string? SetterVisibility,
    bool IsIndexer,
    IReadOnlyList<MemberIndexerParameterInfo>? IndexerParameters = null);

/// <summary>An indexer parameter — legacy JSON only ever included name/type here, not the full parameter shape.</summary>
public sealed record MemberIndexerParameterInfo(string Name, string Type);

public sealed record MemberFieldInfo(
    string Name,
    string Type,
    string Visibility,
    bool IsStatic,
    bool IsReadOnly,
    bool IsConst,
    string? ConstValue = null);

public sealed record MemberEventInfo(
    string Name,
    string Type,
    string Visibility,
    bool IsStatic,
    string? AddMethod = null,
    string? RemoveMethod = null);
