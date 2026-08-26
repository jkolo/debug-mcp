using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services.CodeAnalysis;
using DebugMcp.Services.Progress;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
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
    [McpServerTool(Name = "code_load", Title = "Load Workspace",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Load a .sln or .csproj file into the analysis workspace. Replaces any previously loaded workspace.")]
    public async Task<CodeLoadResult> LoadAsync(
        [Description("Absolute path to .sln or .csproj file")] string path,
        [Description("Maximum time to wait for the workspace to load, in milliseconds (default: 30000)")] int timeoutMs = 30000,
        CancellationToken cancellationToken = default,
        IProgress<ProgressNotificationValue>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.ToolInvoked("code_load", JsonSerializer.Serialize(new { path }));

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Validate path parameter
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.ToolError("code_load", ErrorCodes.InvalidPath);
                return new CodeLoadResult(Success: false, Error: new ToolError(ErrorCodes.InvalidPath, "Path is required"));
            }

            // Validate file extension
            if (!path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                _logger.ToolError("code_load", ErrorCodes.InvalidPath);
                return new CodeLoadResult(Success: false, Error: new ToolError(ErrorCodes.InvalidPath, "Path must be a .sln or .csproj file"));
            }

            // Validate file exists
            if (!File.Exists(path))
            {
                _logger.ToolError("code_load", ErrorCodes.InvalidPath);
                return new CodeLoadResult(Success: false, Error: new ToolError(ErrorCodes.InvalidPath, $"File not found: {path}"));
            }

            // Load the workspace
            var workspaceInfo = await _codeAnalysisService.LoadAsync(
                path, linkedCts.Token, ProgressReporterAdapter.Create(progress));

            stopwatch.Stop();
            _logger.ToolCompleted("code_load", stopwatch.ElapsedMilliseconds);

            return new CodeLoadResult(Success: true, Data: workspaceInfo);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.ToolError("code_load", ErrorCodes.Timeout);
            return new CodeLoadResult(Success: false, Error: new ToolError(ErrorCodes.Timeout, $"code_load timed out after {timeoutMs}ms", new { timeout = timeoutMs }));
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("code_load", ErrorCodes.Timeout);
            return new CodeLoadResult(Success: false, Error: new ToolError(ErrorCodes.Timeout, "Load operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.ToolError("code_load", ErrorCodes.LoadFailed);
            return new CodeLoadResult(Success: false, Error: new ToolError(ErrorCodes.LoadFailed, $"Failed to load workspace: {ex.Message}"));
        }
    }
}
