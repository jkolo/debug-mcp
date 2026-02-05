using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Services.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for loading a solution or project for Roslyn code analysis.
/// </summary>
[McpServerToolType]
public sealed class CodeLoadTool
{
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly ILogger<CodeLoadTool> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CodeLoadTool(ICodeAnalysisService codeAnalysisService, ILogger<CodeLoadTool> logger)
    {
        _codeAnalysisService = codeAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Load a .sln or .csproj file into the analysis workspace.
    /// </summary>
    /// <param name="path">Absolute path to .sln or .csproj file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Workspace information or error response.</returns>
    [McpServerTool(Name = "code_load")]
    [Description("Load a .sln or .csproj file into the analysis workspace. Replaces any previously loaded workspace.")]
    public async Task<string> LoadAsync(
        [Description("Absolute path to .sln or .csproj file")] string path,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.ToolInvoked("code_load", JsonSerializer.Serialize(new { path }));

        try
        {
            // Validate path parameter
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.ToolError("code_load", ErrorCodes.InvalidPath);
                return CreateErrorResponse(ErrorCodes.InvalidPath, "Path is required");
            }

            // Validate file extension
            if (!path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                _logger.ToolError("code_load", ErrorCodes.InvalidPath);
                return CreateErrorResponse(ErrorCodes.InvalidPath, "Path must be a .sln or .csproj file");
            }

            // Validate file exists
            if (!File.Exists(path))
            {
                _logger.ToolError("code_load", ErrorCodes.InvalidPath);
                return CreateErrorResponse(ErrorCodes.InvalidPath, $"File not found: {path}");
            }

            // Load the workspace
            var workspaceInfo = await _codeAnalysisService.LoadAsync(path, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("code_load", stopwatch.ElapsedMilliseconds);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = workspaceInfo
            }, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("code_load", ErrorCodes.Timeout);
            return CreateErrorResponse(ErrorCodes.Timeout, "Load operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.ToolError("code_load", ErrorCodes.LoadFailed);
            return CreateErrorResponse(ErrorCodes.LoadFailed, $"Failed to load workspace: {ex.Message}");
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
