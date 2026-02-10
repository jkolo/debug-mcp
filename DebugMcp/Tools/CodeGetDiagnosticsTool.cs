using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.CodeAnalysis;
using DebugMcp.Services.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for retrieving compilation diagnostics from the workspace.
/// </summary>
[McpServerToolType]
public sealed class CodeGetDiagnosticsTool
{
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly ILogger<CodeGetDiagnosticsTool> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CodeGetDiagnosticsTool(ICodeAnalysisService codeAnalysisService, ILogger<CodeGetDiagnosticsTool> logger)
    {
        _codeAnalysisService = codeAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Get compilation diagnostics (errors and warnings) for projects in the workspace.
    /// </summary>
    /// <param name="projectName">Optional project name. If omitted, returns diagnostics for all projects.</param>
    /// <param name="minSeverity">Minimum severity to include: Hidden, Info, Warning (default), Error.</param>
    /// <param name="maxResults">Maximum number of diagnostics to return (default: 100, max: 500).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of diagnostics or error response.</returns>
    [McpServerTool(Name = "code_get_diagnostics", Title = "Get Diagnostics",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get compilation diagnostics (errors and warnings) for projects in the workspace.")]
    public async Task<string> GetDiagnosticsAsync(
        [Description("Optional project name. If omitted, returns diagnostics for all projects.")] string? projectName = null,
        [Description("Minimum severity to include: Hidden, Info, Warning (default), Error")] string? minSeverity = null,
        [Description("Maximum number of diagnostics to return (default: 100, max: 500)")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.ToolInvoked("code_get_diagnostics", JsonSerializer.Serialize(new { projectName, minSeverity, maxResults }));

        try
        {
            // Validate workspace is loaded
            if (_codeAnalysisService.CurrentWorkspace is null)
            {
                _logger.ToolError("code_get_diagnostics", ErrorCodes.NoWorkspace);
                return CreateErrorResponse(ErrorCodes.NoWorkspace, "No workspace loaded. Call code_load first.");
            }

            // Parse severity
            var severity = DiagnosticSeverity.Warning;
            if (!string.IsNullOrWhiteSpace(minSeverity))
            {
                if (!Enum.TryParse<DiagnosticSeverity>(minSeverity, ignoreCase: true, out severity))
                {
                    _logger.ToolError("code_get_diagnostics", ErrorCodes.InvalidParameter);
                    return CreateErrorResponse(ErrorCodes.InvalidParameter, $"Invalid severity: {minSeverity}. Valid values: Hidden, Info, Warning, Error.");
                }
            }

            // Validate max results
            var limit = Math.Min(maxResults ?? 100, 500);
            if (limit <= 0)
            {
                limit = 100;
            }

            // Get diagnostics
            var diagnostics = await _codeAnalysisService.GetDiagnosticsAsync(
                projectName,
                severity,
                limit,
                cancellationToken);

            // Group by severity for summary
            var summary = diagnostics
                .GroupBy(d => d.Severity)
                .ToDictionary(g => g.Key.ToString().ToLowerInvariant(), g => g.Count());

            stopwatch.Stop();
            _logger.ToolCompleted("code_get_diagnostics", stopwatch.ElapsedMilliseconds);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    total_count = diagnostics.Count,
                    limited_to = limit,
                    summary,
                    diagnostics
                }
            }, JsonOptions);
        }
        catch (ArgumentException ex)
        {
            _logger.ToolError("code_get_diagnostics", ErrorCodes.ProjectNotFound);
            return CreateErrorResponse(ErrorCodes.ProjectNotFound, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.ToolError("code_get_diagnostics", ErrorCodes.NoWorkspace);
            return CreateErrorResponse(ErrorCodes.NoWorkspace, ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("code_get_diagnostics", ErrorCodes.Timeout);
            return CreateErrorResponse(ErrorCodes.Timeout, "Get diagnostics operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.ToolError("code_get_diagnostics", ErrorCodes.AnalysisFailed);
            return CreateErrorResponse(ErrorCodes.AnalysisFailed, $"Get diagnostics failed: {ex.Message}");
        }
    }

    private static string CreateErrorResponse(string code, string message, object? details = null)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new ErrorResponse
            {
                Code = code,
                Message = message,
                Details = details
            }
        }, JsonOptions);
    }
}
