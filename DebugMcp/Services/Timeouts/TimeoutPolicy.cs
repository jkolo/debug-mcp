namespace DebugMcp.Services.Timeouts;

public enum TimeoutUnit
{
    Milliseconds,
    Seconds,
}

/// <summary>
/// One tool's FR-031 classification. <see cref="IsBlocking"/> false means the tool only reads
/// in-memory server state and MUST NOT gain a timeout parameter. True means it waits on
/// something outside the server's own memory (the debuggee, a build, a symbol server, or the
/// ReSharper engine) and MUST carry <see cref="ParameterName"/> with the documented
/// <see cref="DefaultValue"/> (FR-032).
/// </summary>
public sealed record ToolTimeoutSpec(
    bool IsBlocking,
    string? ParameterName = null,
    TimeoutUnit? Unit = null,
    int? DefaultValue = null);

/// <summary>
/// The classification for all 39 tools (FR-031), traced against each tool's actual service calls
/// — not inferred from its name. This is data, not scattered judgement: T072's contract test
/// asserts every tool's real signature against this table, both directions.
///
/// Parameter naming/casing follows each tool's own existing convention (snake_case for most
/// families; camelCase for session/execution — matching `debug_launch`'s pre-existing `timeout` —
/// and for the `code_*` family, matching `code_get_diagnostics`'s pre-existing `projectName` etc.).
/// Units follow the family's own existing convention where one already exists.
///
/// FR-032 deviations, recorded here rather than silently: `evaluate`/`evaluate_safe`/
/// `object_summarize`/`collection_analyze` already document a 5000 ms default — *shorter* than
/// the 30s standard. FR-032's letter only carves out an exception for *longer* pre-existing
/// defaults (ReSharper); a pre-existing shorter default for a genuinely fast operation (evaluating
/// one expression) wasn't anticipated by the FR text either way. Kept as-is: changing a
/// already-shipped default to 30s would be an unrequested behavior change, and wire/behavior
/// stability has been this feature's consistent tie-breaker (see data-model.md §1's other
/// documented deviations).
/// </summary>
public static class TimeoutPolicy
{
    public static readonly IReadOnlyDictionary<string, ToolTimeoutSpec> Specs = new Dictionary<string, ToolTimeoutSpec>
    {
        // Session
        ["debug_launch"] = new(true, "timeout", TimeoutUnit.Milliseconds, 30000),
        ["debug_attach"] = new(true, "timeout", TimeoutUnit.Milliseconds, 30000),
        ["debug_disconnect"] = new(true, "timeout", TimeoutUnit.Milliseconds, 10000),

        // Breakpoints
        ["breakpoint_set"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["breakpoint_remove"] = new(false),
        ["breakpoint_enable"] = new(false),
        ["breakpoint_set_exception"] = new(false),
        ["tracepoint_set"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["exception_get_context"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),

        // Execution
        ["debug_continue"] = new(true, "timeout", TimeoutUnit.Milliseconds, 30000),
        ["debug_pause"] = new(true, "timeout", TimeoutUnit.Milliseconds, 30000),
        ["debug_step"] = new(true, "timeout", TimeoutUnit.Milliseconds, 30000),

        // Inspection
        ["stacktrace_get"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["variables_get"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["evaluate"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 5000),
        ["evaluate_safe"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 5000),
        ["object_inspect"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["object_summarize"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 5000),
        ["collection_analyze"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 5000),

        // Memory
        ["memory_read"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["layout_get"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["references_get"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),

        // Modules
        ["modules_search"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["types_get"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["members_get"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),

        // Code analysis
        ["code_load"] = new(true, "timeoutMs", TimeoutUnit.Milliseconds, 30000),
        ["code_find_usages"] = new(true, "timeoutMs", TimeoutUnit.Milliseconds, 30000),
        ["code_find_assignments"] = new(true, "timeoutMs", TimeoutUnit.Milliseconds, 30000),
        ["code_get_diagnostics"] = new(true, "timeoutMs", TimeoutUnit.Milliseconds, 30000),
        ["code_goto_definition"] = new(true, "timeoutMs", TimeoutUnit.Milliseconds, 30000),

        // ReSharper — the "long-running five" per TaskExecutionPolicy.QualifyingTools, along with
        // batch_evaluate, debug_launch and code_load; these two keep their existing 300s default
        // (FR-032's explicit exception).
        ["resharper_inspect_solution"] = new(true, "timeoutSeconds", TimeoutUnit.Seconds, 300),
        ["resharper_inspect_project"] = new(true, "timeoutSeconds", TimeoutUnit.Seconds, 300),

        // Process I/O
        ["process_write_input"] = new(false),
        ["process_read_output"] = new(false),

        // Snapshots
        ["snapshot_create"] = new(true, "timeout_ms", TimeoutUnit.Milliseconds, 30000),
        ["snapshot_delete"] = new(false),
        ["snapshot_diff"] = new(false),

        // Batch
        ["batch_evaluate"] = new(true, "timeoutSeconds", TimeoutUnit.Seconds, 30),

        // Timeline
        ["timeline_query"] = new(false),
    };
}
