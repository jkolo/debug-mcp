using DebugMcp.Models.ReSharper;

namespace DebugMcp.Models.Results;

/// <summary>
/// Wire shape for <c>resharper_inspect_solution</c> and <c>resharper_inspect_project</c>.
/// Field names preserved from the pre-US3 hand-rolled JSON (FR-021). Both tools share this
/// record — they already returned an identical <c>{success, data}</c>/<c>{success, error}</c>
/// envelope around the same <see cref="Models.ReSharper.InspectionResult"/> payload, so no new
/// per-tool record type is invented (rule: reuse an existing 1:1-matching type before adding one).
/// </summary>
public sealed record ReSharperInspectionResult(
    bool Success,
    InspectionResult? Data = null,
    ToolError? Error = null);
