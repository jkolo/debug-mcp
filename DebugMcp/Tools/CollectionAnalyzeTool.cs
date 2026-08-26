using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services.Inspection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for analyzing a collection variable and returning a structured summary.
/// </summary>
[McpServerToolType]
public sealed class CollectionAnalyzeTool
{
    private readonly ICollectionAnalyzer _analyzer;
    private readonly ILogger<CollectionAnalyzeTool> _logger;

    public CollectionAnalyzeTool(ICollectionAnalyzer analyzer, ILogger<CollectionAnalyzeTool> logger)
    {
        _analyzer = analyzer;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a collection variable and return a structured summary with count, element types,
    /// null count, first/last element previews, and numeric statistics (min/max/avg).
    /// Replaces 5-50+ tool calls typically needed to understand a collection's contents.
    /// </summary>
    [McpServerTool(Name = "collection_analyze", Title = "Analyze Collection",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Analyze a collection (array, List, Dictionary, HashSet, etc.) and return a single-call summary: count, element types, null count, first/last N element previews, numeric statistics (min/max/avg), and type distribution for mixed-type collections.")]
    public async Task<CollectionAnalyzeResult> AnalyzeCollection(
        [Description("Variable name or expression evaluating to a collection")]
        string expression,
        [Description("Number of first/last elements to include in preview (1-50, default: 5)")]
        int max_preview_items = 5,
        [Description("Thread context (default: current thread)")]
        int? thread_id = null,
        [Description("Stack frame context (0 = top of stack)")]
        int frame_index = 0,
        [Description("Evaluation timeout in milliseconds")]
        int timeout_ms = 5000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("collection_analyze", JsonSerializer.Serialize(new { expression, max_preview_items, thread_id, frame_index }));

        try
        {
            var summary = await _analyzer.AnalyzeAsync(expression, max_preview_items, thread_id, frame_index, timeout_ms, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("collection_analyze", stopwatch.ElapsedMilliseconds);

            return new CollectionAnalyzeResult(
                Success: true,
                Summary: new CollectionAnalyzeSummary(
                    Count: summary.Count,
                    ElementType: summary.ElementType,
                    CollectionType: summary.CollectionType,
                    Kind: summary.Kind.ToString(),
                    NullCount: summary.NullCount,
                    NumericStats: summary.NumericStats is { } ns ? new CollectionNumericStats(ns.Min, ns.Max, ns.Average) : null,
                    TypeDistribution: summary.TypeDistribution?.Select(td => new CollectionTypeCount(td.TypeName, td.Count)).ToList(),
                    FirstElements: summary.FirstElements.Select(e => new CollectionElementPreview(e.Index, e.Value, e.Type)).ToList(),
                    LastElements: summary.LastElements.Select(e => new CollectionElementPreview(e.Index, e.Value, e.Type)).ToList(),
                    KeyValuePairs: summary.KeyValuePairs?.Select(kv => new CollectionKeyValuePreview(kv.Key, kv.KeyType, kv.Value, kv.ValueType)).ToList(),
                    IsSampled: summary.IsSampled));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not a recognized collection"))
        {
            _logger.ToolError("collection_analyze", "NOT_COLLECTION");
            return new CollectionAnalyzeResult(Success: false, Error: new ToolError("not_collection", ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("paused") || ex.Message.Contains("Paused"))
        {
            _logger.ToolError("collection_analyze", ErrorCodes.NotPaused);
            return new CollectionAnalyzeResult(Success: false, Error: new ToolError(
                ErrorCodes.NotPaused, "Process is not paused. Cannot inspect variables while running."));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("session"))
        {
            _logger.ToolError("collection_analyze", ErrorCodes.NoSession);
            return new CollectionAnalyzeResult(Success: false, Error: new ToolError(ErrorCodes.NoSession, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("available") || ex.Message.Contains("scope") || ex.Message.Contains("Failed to evaluate"))
        {
            _logger.ToolError("collection_analyze", "VARIABLE_UNAVAILABLE");
            return new CollectionAnalyzeResult(Success: false, Error: new ToolError(
                "variable_unavailable", $"Variable '{expression}' is not available in the current scope."));
        }
        catch (Exception ex)
        {
            _logger.ToolError("collection_analyze", ErrorCodes.VariablesFailed);
            return new CollectionAnalyzeResult(Success: false, Error: new ToolError(
                ErrorCodes.VariablesFailed,
                $"Failed to analyze collection: {ex.Message}",
                new { exceptionType = ex.GetType().Name }));
        }
    }
}
