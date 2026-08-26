using System.Text.Json.Serialization;

namespace DebugMcp.Models.Results;

/// <summary>Wire shape for <c>stacktrace_get</c>. Field names preserved from the pre-US3 hand-rolled JSON (FR-021).</summary>
public sealed record StacktraceGetResult(
    bool Success,
    [property: JsonPropertyName("thread_id")] int? ThreadId = null,
    [property: JsonPropertyName("total_frames")] int? TotalFrames = null,
    IReadOnlyList<StackFrameResult>? Frames = null,
    [property: JsonPropertyName("raw_frames")] IReadOnlyList<RawStackFrameResult>? RawFrames = null,
    ToolError? Error = null);

/// <summary>A single logical stack frame (async-reconstructed), as emitted in <c>frames</c>.</summary>
public sealed record StackFrameResult(
    int Index,
    string Function,
    string Module,
    [property: JsonPropertyName("is_external")] bool IsExternal,
    [property: JsonPropertyName("frame_kind")] string FrameKind,
    [property: JsonPropertyName("is_awaiting")] bool IsAwaiting,
    [property: JsonPropertyName("logical_function")] string? LogicalFunction = null,
    FrameLocationResult? Location = null,
    IReadOnlyList<VariableResult>? Arguments = null);

/// <summary>A single raw (physical) stack frame, as emitted in <c>raw_frames</c> when <c>include_raw</c> is set.</summary>
public sealed record RawStackFrameResult(
    int Index,
    string Function,
    string Module,
    [property: JsonPropertyName("is_external")] bool IsExternal,
    [property: JsonPropertyName("frame_kind")] string FrameKind,
    FrameLocationResult? Location = null);

/// <summary>Source location for a stack frame.</summary>
public sealed record FrameLocationResult(
    string File,
    int Line,
    int? Column = null,
    string? Function = null);
