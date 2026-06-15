using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Services.ReSharper;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool that runs ReSharper InspectCode over an entire solution.
/// </summary>
[McpServerToolType]
public sealed class ReSharperInspectSolutionTool
{
    private readonly IReSharperInspectionService _service;
    private readonly ReSharperOptions _options;
    private readonly ILogger<ReSharperInspectSolutionTool> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ReSharperInspectSolutionTool(
        IReSharperInspectionService service,
        ReSharperOptions options,
        ILogger<ReSharperInspectSolutionTool> logger)
    {
        _service = service;
        _options = options;
        _logger = logger;
    }

    [McpServerTool(Name = "resharper_inspect_solution", Title = "Inspect Solution (ReSharper)",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Run JetBrains ReSharper's code inspections over an entire .NET solution (.sln) and return structured findings (hundreds of inspections beyond the C# compiler / Roslyn). On first use the ReSharper engine is downloaded and cached automatically (a one-time ~180 MB acquisition); later calls reuse the cache. Findings carry ReSharper's native severity (error/warning/suggestion/hint), file, line, category, and help link. Complements code_get_diagnostics rather than replacing it. Returns: {success, data:{target, findings[], total_count, returned_count, truncated, limited_to, summary, engine_version, duration_ms, built}}.")]
    public async Task<string> InspectSolutionAsync(
        [Description("Absolute path to a .sln file")] string solutionPath,
        [Description("Minimum native severity: error | warning | suggestion | hint (optional)")] string? severity = null,
        [Description("Restrict to a single project in the solution (optional)")] string? project = null,
        [Description("Skip the engine's pre-analysis build (default: false)")] bool noBuild = false,
        [Description("Per-call inspection timeout in seconds (10–1800; excludes one-time engine download)")] int? timeoutSeconds = null,
        [Description("Maximum findings to return (default 500, max 500)")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        return await ReSharperToolHelper.RunAsync(
            toolName: "resharper_inspect_solution",
            target: solutionPath,
            requiredExtension: ".sln",
            severity: severity,
            project: project,
            noBuild: noBuild,
            timeoutSeconds: timeoutSeconds,
            maxResults: maxResults,
            service: _service,
            options: _options,
            logger: _logger,
            jsonOptions: JsonOptions,
            cancellationToken: cancellationToken);
    }
}
