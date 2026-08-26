using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for navigating to symbol definitions (go-to-definition).
/// </summary>
[McpServerToolType]
public sealed class CodeGoToDefinitionTool
{
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly ILogger<CodeGoToDefinitionTool> _logger;

    public CodeGoToDefinitionTool(ICodeAnalysisService codeAnalysisService, ILogger<CodeGoToDefinitionTool> logger)
    {
        _codeAnalysisService = codeAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Navigate to the definition of a symbol at a given source location.
    /// </summary>
    /// <param name="file">Absolute path to source file.</param>
    /// <param name="line">1-based line number where the symbol appears.</param>
    /// <param name="column">1-based column number where the symbol appears.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Definition location(s) or error response.</returns>
    [McpServerTool(Name = "code_goto_definition", Title = "Go to Definition",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Navigate to the definition of a symbol at a given source location. Returns source file location or assembly info for metadata symbols.")]
    public async Task<CodeGoToDefinitionResult> GoToDefinitionAsync(
        [Description("Absolute path to source file")] string file,
        [Description("1-based line number where the symbol appears")] int line,
        [Description("1-based column number where the symbol appears")] int column,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.ToolInvoked("code_goto_definition", JsonSerializer.Serialize(new { file, line, column }));

        try
        {
            // Validate workspace is loaded
            if (_codeAnalysisService.CurrentWorkspace is null)
            {
                _logger.ToolError("code_goto_definition", ErrorCodes.NoWorkspace);
                return new CodeGoToDefinitionResult(Success: false, Error: new ToolError(ErrorCodes.NoWorkspace, "No workspace loaded. Call code_load first."));
            }

            // Validate parameters
            if (string.IsNullOrWhiteSpace(file))
            {
                _logger.ToolError("code_goto_definition", ErrorCodes.InvalidParameter);
                return new CodeGoToDefinitionResult(Success: false, Error: new ToolError(ErrorCodes.InvalidParameter, "File path is required."));
            }

            if (line <= 0)
            {
                _logger.ToolError("code_goto_definition", ErrorCodes.InvalidParameter);
                return new CodeGoToDefinitionResult(Success: false, Error: new ToolError(ErrorCodes.InvalidParameter, "Line must be a positive number."));
            }

            if (column <= 0)
            {
                _logger.ToolError("code_goto_definition", ErrorCodes.InvalidParameter);
                return new CodeGoToDefinitionResult(Success: false, Error: new ToolError(ErrorCodes.InvalidParameter, "Column must be a positive number."));
            }

            // Go to definition
            var result = await _codeAnalysisService.GoToDefinitionAsync(file, line, column, cancellationToken);

            if (result is null)
            {
                _logger.ToolError("code_goto_definition", ErrorCodes.SymbolNotFound);
                return new CodeGoToDefinitionResult(Success: false, Error: new ToolError(ErrorCodes.SymbolNotFound, $"No symbol found at {file}:{line}:{column}"));
            }

            stopwatch.Stop();
            _logger.ToolCompleted("code_goto_definition", stopwatch.ElapsedMilliseconds);

            return new CodeGoToDefinitionResult(
                Success: true,
                Data: new CodeGoToDefinitionData
                {
                    Symbol = new GoToDefinitionSymbolSummary
                    {
                        Name = result.Symbol.Name,
                        FullyQualifiedName = result.Symbol.FullyQualifiedName,
                        Kind = result.Symbol.Kind.ToString(),
                        ContainingType = result.Symbol.ContainingType,
                        ContainingNamespace = result.Symbol.ContainingNamespace
                    },
                    DefinitionsCount = result.Definitions.Count,
                    Definitions = result.Definitions
                });
        }
        catch (InvalidOperationException ex)
        {
            _logger.ToolError("code_goto_definition", ErrorCodes.NoWorkspace);
            return new CodeGoToDefinitionResult(Success: false, Error: new ToolError(ErrorCodes.NoWorkspace, ex.Message));
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("code_goto_definition", ErrorCodes.Timeout);
            return new CodeGoToDefinitionResult(Success: false, Error: new ToolError(ErrorCodes.Timeout, "Go to definition operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.ToolError("code_goto_definition", ErrorCodes.AnalysisFailed);
            return new CodeGoToDefinitionResult(Success: false, Error: new ToolError(ErrorCodes.AnalysisFailed, $"Go to definition failed: {ex.Message}"));
        }
    }
}
