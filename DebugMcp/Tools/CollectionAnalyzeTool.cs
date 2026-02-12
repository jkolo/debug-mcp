using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
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
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Analyze a collection (array, List, Dictionary, HashSet, etc.) and return a single-call summary: count, element types, null count, first/last N element previews, numeric statistics (min/max/avg), and type distribution for mixed-type collections.")]
    public async Task<string> AnalyzeCollection(
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

            return JsonSerializer.Serialize(new
            {
                success = true,
                summary = new
                {
                    count = summary.Count,
                    elementType = summary.ElementType,
                    collectionType = summary.CollectionType,
                    kind = summary.Kind.ToString(),
                    nullCount = summary.NullCount,
                    numericStats = summary.NumericStats is { } ns ? new { min = ns.Min, max = ns.Max, average = ns.Average } : null,
                    typeDistribution = summary.TypeDistribution?.Select(td => new { typeName = td.TypeName, count = td.Count }),
                    firstElements = summary.FirstElements.Select(e => new { index = e.Index, value = e.Value, type = e.Type }),
                    lastElements = summary.LastElements.Select(e => new { index = e.Index, value = e.Value, type = e.Type }),
                    keyValuePairs = summary.KeyValuePairs?.Select(kv => new { key = kv.Key, keyType = kv.KeyType, value = kv.Value, valueType = kv.ValueType }),
                    isSampled = summary.IsSampled
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not a recognized collection"))
        {
            _logger.ToolError("collection_analyze", "NOT_COLLECTION");
            return CreateErrorResponse("not_collection", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("paused") || ex.Message.Contains("Paused"))
        {
            _logger.ToolError("collection_analyze", ErrorCodes.NotPaused);
            return CreateErrorResponse(ErrorCodes.NotPaused,
                "Process is not paused. Cannot inspect variables while running.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("session"))
        {
            _logger.ToolError("collection_analyze", ErrorCodes.NoSession);
            return CreateErrorResponse(ErrorCodes.NoSession, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("available") || ex.Message.Contains("scope") || ex.Message.Contains("Failed to evaluate"))
        {
            _logger.ToolError("collection_analyze", "VARIABLE_UNAVAILABLE");
            return CreateErrorResponse("variable_unavailable",
                $"Variable '{expression}' is not available in the current scope.");
        }
        catch (Exception ex)
        {
            _logger.ToolError("collection_analyze", ErrorCodes.VariablesFailed);
            return CreateErrorResponse(ErrorCodes.VariablesFailed,
                $"Failed to analyze collection: {ex.Message}",
                new { exceptionType = ex.GetType().Name });
        }
    }

    private static string CreateErrorResponse(string code, string message, object? details = null)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new { code, message, details }
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
