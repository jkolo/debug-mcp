using DebugMcp.Models.Memory;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>layout_get</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record LayoutGetResult(
    bool Success,
    LayoutInfo? Layout = null,
    ToolError? Error = null);

/// <summary>The <c>layout</c> object nested under a successful <c>layout_get</c> result.</summary>
public sealed record LayoutInfo(
    string TypeName,
    int TotalSize,
    int HeaderSize,
    int DataSize,
    IReadOnlyList<LayoutFieldInfo> Fields,
    bool IsValueType,
    IReadOnlyList<PaddingRegion>? Padding = null,
    string? BaseType = null);

/// <summary>
/// A single field's layout. Deliberately its own type rather than reusing
/// <see cref="DebugMcp.Models.Memory.LayoutField"/> — that model omits <c>alignment</c> when it is
/// 0 (<c>JsonIgnoreCondition.WhenWritingDefault</c>), while the legacy hand-rolled JSON here always
/// included it (only literal C# <c>null</c> values were filtered).
/// </summary>
public sealed record LayoutFieldInfo(
    string Name,
    string TypeName,
    int Offset,
    int Size,
    int Alignment,
    bool IsReference,
    string? DeclaringType = null);
