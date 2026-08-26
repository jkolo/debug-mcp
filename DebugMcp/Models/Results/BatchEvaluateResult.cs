using System.Text.Json.Serialization;

namespace DebugMcp.Models.Results;

/// <summary>
/// Wire shape for <c>batch_evaluate</c>. Field names preserved from the pre-US3 hand-rolled JSON
/// (FR-021), including its snake_case fields — the default camelCase naming policy does not
/// reproduce those, so every snake_case field carries an explicit
/// <c>[JsonPropertyName]</c> override: <c>completion_reason</c>, <c>total_experiments</c>,
/// <c>not_triggered</c>, <c>hit_count</c>, <c>thread_id</c>, <c>eval_errors</c>.
/// </summary>
/// <remarks>
/// Legacy note: pre-migration failure responses were <c>{success:false, error:{code,message}}</c>
/// with tool-invented, lowercase <c>code</c> values (<c>validation_error</c>,
/// <c>batch_already_running</c>, <c>invalid_json</c>, <c>cancelled</c>, <c>internal_error</c>) —
/// never drawn from <c>DebugMcp.Models.ErrorCodes</c>. US3's <c>ErrorShapeContractTests</c> forces
/// every tool's <c>Error</c> onto the shared <see cref="ToolError"/> type but does not check
/// <c>Code</c> membership in <c>ErrorCodes</c>, so these legacy code *values* are preserved
/// verbatim to keep the wire byte-identical (flagged in the US3 T052 report as a deviation from
/// the "reuse ErrorCodes constants" guidance, since no such constants ever existed for this tool).
/// </remarks>
public sealed record BatchEvaluateResult(
    bool Success,
    [property: JsonPropertyName("completion_reason")] string? CompletionReason = null,
    [property: JsonPropertyName("total_experiments")] int? TotalExperiments = null,
    int? Triggered = null,
    [property: JsonPropertyName("not_triggered")] int? NotTriggered = null,
    int? Errors = null,
    IReadOnlyList<BatchExperimentResultWire>? Experiments = null,
    ToolError? Error = null);

/// <summary>
/// All collected data for one experiment, as emitted on the wire. <see cref="Error"/> is
/// nullable-with-default (reproduces legacy conditional omission for non-error experiments — the
/// requiredness pitfall applies recursively to nested wire types too, not just the top-level
/// result); it is placed last, after the always-present <see cref="Hits"/>, purely so C# accepts
/// the default (field order is not part of the wire contract).
/// </summary>
public sealed record BatchExperimentResultWire(
    int Index,
    string Status,
    [property: JsonPropertyName("hit_count")] int HitCount,
    IEnumerable<BatchExperimentHitWire> Hits,
    string? Error = null);

/// <summary>
/// A single firing of an experiment's trigger, as emitted on the wire. <see cref="EvalErrors"/> is
/// nullable-with-default (reproduces legacy conditional omission when there were no eval errors).
/// </summary>
public sealed record BatchExperimentHitWire(
    DateTimeOffset Timestamp,
    [property: JsonPropertyName("thread_id")] int ThreadId,
    BatchHitLocationWire Location,
    IReadOnlyDictionary<string, string> Values,
    [property: JsonPropertyName("eval_errors")] IReadOnlyDictionary<string, string>? EvalErrors = null);

/// <summary>Resolved source location nested under <see cref="BatchExperimentHitWire"/>.</summary>
public sealed record BatchHitLocationWire(string File, int Line);
