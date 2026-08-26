using DebugMcp.Models.Inspection;

namespace DebugMcp.Models.Results;

/// <summary>
/// Wire shape for <c>exception_get_context</c>. Field names preserved from the pre-US3
/// hand-rolled JSON (FR-021). The legacy success path did not emit a <c>success</c> field
/// (only the tool's own doc example did); every migrated tool now carries one so the central
/// <c>IsErrorFilter</c> can derive <c>isError</c> — an intentional, additive wire delta.
/// </summary>
public sealed record ExceptionGetContextResult(
    bool Success,
    int? ThreadId = null,
    ExceptionDetail? Exception = null,
    IReadOnlyList<InnerExceptionEntry>? InnerExceptions = null,
    bool? InnerExceptionsTruncated = null,
    IReadOnlyList<ExceptionFrameInfo>? Frames = null,
    int? TotalFrames = null,
    int? ThrowingFrameIndex = null,
    ToolError? Error = null,
    IReadOnlyList<RankedSuspect>? Ranking = null,
    RankingUnavailable? RankingUnavailable = null);

/// <summary>A single stack frame within <c>exception_get_context</c>'s <c>frames</c> array.</summary>
public sealed record ExceptionFrameInfo(
    int Index,
    string Function,
    string Module,
    bool IsExternal,
    SourceLocation? Location = null,
    IReadOnlyList<ExceptionFrameArgument>? Arguments = null,
    ExceptionFrameVariables? Variables = null);

public sealed record ExceptionFrameArgument(
    string Name,
    string Type,
    string Value,
    string Scope,
    bool HasChildren);

public sealed record ExceptionFrameVariables(
    IReadOnlyList<ExceptionFrameLocal> Locals,
    IReadOnlyList<VariableError>? Errors = null);

public sealed record ExceptionFrameLocal(
    string Name,
    string Type,
    string Value,
    string Scope,
    bool HasChildren,
    int? ChildrenCount = null);
