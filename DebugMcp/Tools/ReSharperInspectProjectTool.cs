using System.ComponentModel;
using DebugMcp.Models.Results;
using DebugMcp.Services.ReSharper;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool that runs ReSharper InspectCode over a single project.
/// </summary>
[McpServerToolType]
public sealed class ReSharperInspectProjectTool
{
    private readonly IReSharperInspectionService _service;
    private readonly ReSharperOptions _options;
    private readonly ILogger<ReSharperInspectProjectTool> _logger;

    public ReSharperInspectProjectTool(
        IReSharperInspectionService service,
        ReSharperOptions options,
        ILogger<ReSharperInspectProjectTool> logger)
    {
        _service = service;
        _options = options;
        _logger = logger;
    }

    [McpServerTool(Name = "resharper_inspect_project", Title = "Inspect Project (ReSharper)",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Run JetBrains ReSharper's code inspections over a single .NET project (.csproj) and return structured findings (hundreds of inspections beyond the C# compiler / Roslyn). On first use the ReSharper engine is downloaded and cached automatically (a one-time ~180 MB acquisition); later calls reuse the cache. Findings carry ReSharper's native severity (error/warning/suggestion/hint), file, line, category, and help link. Complements code_get_diagnostics rather than replacing it. Returns: {success, data:{target, findings[], total_count, returned_count, truncated, limited_to, summary, engine_version, duration_ms, built}}.")]
    public async Task<ReSharperInspectionResult> InspectProjectAsync(
        [Description("Absolute path to a .csproj file")] string projectPath,
        [Description("Minimum native severity: error | warning | suggestion | hint (optional)")] string? severity = null,
        [Description("Skip the engine's pre-analysis build (default: false)")] bool noBuild = false,
        [Description("Per-call inspection timeout in seconds (10–1800; excludes one-time engine download) (default: 300)")] int? timeoutSeconds = null,
        [Description("Maximum findings to return (default 500, max 500)")] int? maxResults = null,
        CancellationToken cancellationToken = default,
        IProgress<ModelContextProtocol.ProgressNotificationValue>? progress = null)
    {
        return await ReSharperToolHelper.RunAsync(
            toolName: "resharper_inspect_project",
            target: projectPath,
            requiredExtension: ".csproj",
            severity: severity,
            project: null,
            noBuild: noBuild,
            timeoutSeconds: timeoutSeconds,
            maxResults: maxResults,
            service: _service,
            options: _options,
            logger: _logger,
            cancellationToken: cancellationToken,
            progress: progress);
    }
}
