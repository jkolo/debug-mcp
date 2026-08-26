using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.CodeAnalysis;
using DebugMcp.Models.Results;
using DebugMcp.Services.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for finding all assignments to a symbol across the workspace.
/// </summary>
[McpServerToolType]
public sealed class CodeFindAssignmentsTool
{
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly ILogger<CodeFindAssignmentsTool> _logger;

    public CodeFindAssignmentsTool(ICodeAnalysisService codeAnalysisService, ILogger<CodeFindAssignmentsTool> logger)
    {
        _codeAnalysisService = codeAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Find all assignments to a variable, field, or property.
    /// </summary>
    /// <param name="name">Fully qualified symbol name. Mutually exclusive with file/line/column.</param>
    /// <param name="symbolKind">Optional symbol kind filter: Field, Property, Local, Parameter.</param>
    /// <param name="file">Absolute path to source file containing the symbol. Used with line/column.</param>
    /// <param name="line">1-based line number where the symbol is located. Used with file/column.</param>
    /// <param name="column">1-based column number where the symbol is located. Used with file/line.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of assignment locations or error response.</returns>
    [McpServerTool(Name = "code_find_assignments", Title = "Find Assignments",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Find all assignments to a variable, field, or property. Includes simple assignments, compound (+=, -=), increment/decrement (++, --), and out/ref parameters.")]
    public async Task<CodeFindAssignmentsResult> FindAssignmentsAsync(
        [Description("Fully qualified symbol name. Mutually exclusive with file/line/column.")] string? name = null,
        [Description("Optional symbol kind filter: Field, Property, Local, Parameter")] string? symbolKind = null,
        [Description("Absolute path to source file containing the symbol. Used with line/column.")] string? file = null,
        [Description("1-based line number where the symbol is located. Used with file/column.")] int? line = null,
        [Description("1-based column number where the symbol is located. Used with file/line.")] int? column = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.ToolInvoked("code_find_assignments", JsonSerializer.Serialize(new { name, symbolKind, file, line, column }));

        try
        {
            // Validate workspace is loaded
            if (_codeAnalysisService.CurrentWorkspace is null)
            {
                _logger.ToolError("code_find_assignments", ErrorCodes.NoWorkspace);
                return new CodeFindAssignmentsResult(Success: false, Error: new ToolError(ErrorCodes.NoWorkspace, "No workspace loaded. Call code_load first."));
            }

            // Validate parameters
            var hasName = !string.IsNullOrWhiteSpace(name);
            var hasLocation = !string.IsNullOrWhiteSpace(file) && line.HasValue && column.HasValue;

            if (!hasName && !hasLocation)
            {
                _logger.ToolError("code_find_assignments", ErrorCodes.InvalidParameter);
                return new CodeFindAssignmentsResult(Success: false, Error: new ToolError(ErrorCodes.InvalidParameter, "Either 'name' or 'file'+'line'+'column' must be provided."));
            }

            if (hasName && hasLocation)
            {
                _logger.ToolError("code_find_assignments", ErrorCodes.InvalidParameter);
                return new CodeFindAssignmentsResult(Success: false, Error: new ToolError(ErrorCodes.InvalidParameter, "Provide either 'name' or 'file'+'line'+'column', not both."));
            }

            // Parse symbol kind if provided
            SymbolKind? parsedKind = null;
            if (!string.IsNullOrWhiteSpace(symbolKind))
            {
                if (!Enum.TryParse<SymbolKind>(symbolKind, ignoreCase: true, out var kind))
                {
                    _logger.ToolError("code_find_assignments", ErrorCodes.InvalidParameter);
                    return new CodeFindAssignmentsResult(Success: false, Error: new ToolError(ErrorCodes.InvalidParameter, $"Invalid symbol kind: {symbolKind}. Valid values: Field, Property, Local, Parameter."));
                }

                // Validate it's an assignable symbol kind
                if (kind is not (SymbolKind.Field or SymbolKind.Property or SymbolKind.Local or SymbolKind.Parameter))
                {
                    _logger.ToolError("code_find_assignments", ErrorCodes.InvalidParameter);
                    return new CodeFindAssignmentsResult(Success: false, Error: new ToolError(ErrorCodes.InvalidParameter, $"Symbol kind {symbolKind} is not assignable. Use Field, Property, Local, or Parameter."));
                }

                parsedKind = kind;
            }

            // Find the symbol
            SymbolInfo? symbol;

            if (hasName)
            {
                symbol = await _codeAnalysisService.FindSymbolByNameAsync(name!, parsedKind, cancellationToken);
            }
            else
            {
                symbol = await _codeAnalysisService.GetSymbolAtLocationAsync(file!, line!.Value, column!.Value, cancellationToken);
            }

            if (symbol is null)
            {
                _logger.ToolError("code_find_assignments", ErrorCodes.SymbolNotFound);
                return new CodeFindAssignmentsResult(
                    Success: false,
                    Error: new ToolError(
                        ErrorCodes.SymbolNotFound,
                        hasName
                            ? $"Symbol not found: {name}"
                            : $"No symbol found at {file}:{line}:{column}"));
            }

            // Find all assignments
            var assignments = await _codeAnalysisService.FindAssignmentsAsync(symbol, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("code_find_assignments", stopwatch.ElapsedMilliseconds);

            var (boundedAssignments, truncation) = ResultTruncation.Bound(
                assignments.ToList(), "code_find_assignments result exceeded the 256 KB size budget");

            return new CodeFindAssignmentsResult(
                Success: true,
                Data: new CodeFindAssignmentsData
                {
                    Symbol = new FindAssignmentsSymbolSummary
                    {
                        Name = symbol.Name,
                        FullyQualifiedName = symbol.FullyQualifiedName,
                        Kind = symbol.Kind.ToString(),
                        ContainingType = symbol.ContainingType,
                        DeclarationFile = symbol.DeclarationFile,
                        DeclarationLine = symbol.DeclarationLine
                    },
                    AssignmentsCount = assignments.Count,
                    Assignments = boundedAssignments
                },
                Truncation: truncation);
        }
        catch (InvalidOperationException ex)
        {
            _logger.ToolError("code_find_assignments", ErrorCodes.NoWorkspace);
            return new CodeFindAssignmentsResult(Success: false, Error: new ToolError(ErrorCodes.NoWorkspace, ex.Message));
        }
        catch (OperationCanceledException)
        {
            _logger.ToolError("code_find_assignments", ErrorCodes.Timeout);
            return new CodeFindAssignmentsResult(Success: false, Error: new ToolError(ErrorCodes.Timeout, "Find assignments operation was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.ToolError("code_find_assignments", ErrorCodes.AnalysisFailed);
            return new CodeFindAssignmentsResult(Success: false, Error: new ToolError(ErrorCodes.AnalysisFailed, $"Find assignments failed: {ex.Message}"));
        }
    }
}
