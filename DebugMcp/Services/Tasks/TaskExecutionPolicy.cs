using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DebugMcp.Services.Tasks;

/// <summary>
/// The FR-013 qualifying-tool table, consulted by <c>McpTasksOptions.ExecutionModeSelector</c>.
/// The SDK's own default selector treats every tool as task-capable, so this must be supplied
/// explicitly at registration (research.md R1) — there is no per-tool setting to "pin"; this
/// table is the single source of truth.
/// </summary>
public static class TaskExecutionPolicy
{
    /// <summary>The five tools FR-013 qualifies for deferred execution.</summary>
    public static readonly IReadOnlySet<string> QualifyingTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "resharper_inspect_solution",
        "resharper_inspect_project",
        "batch_evaluate",
        "debug_launch",
        "code_load",
    };

    public static McpTaskExecutionMode SelectMode(RequestContext<CallToolRequestParams> context) =>
        GetMode(context.Params?.Name);

    /// <summary>Pure classification, factored out of <see cref="SelectMode"/> so tests can exercise it without constructing a <c>RequestContext</c>.</summary>
    public static McpTaskExecutionMode GetMode(string? toolName) =>
        toolName is not null && QualifyingTools.Contains(toolName)
            ? McpTaskExecutionMode.Optional
            : McpTaskExecutionMode.Synchronous;
}
