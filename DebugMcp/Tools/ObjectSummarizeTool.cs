using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Services.Inspection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for summarizing an object's fields with anomaly detection.
/// </summary>
[McpServerToolType]
public sealed class ObjectSummarizeTool
{
    private readonly IObjectSummarizer _summarizer;
    private readonly ILogger<ObjectSummarizeTool> _logger;

    public ObjectSummarizeTool(IObjectSummarizer summarizer, ILogger<ObjectSummarizeTool> logger)
    {
        _summarizer = summarizer;
        _logger = logger;
    }

    /// <summary>
    /// Summarize an object's fields, categorizing them into valued, null, and interesting (anomalous).
    /// Detects empty strings, NaN, Infinity, default dates, and empty GUIDs.
    /// Collection-typed fields show their element count and type inline.
    /// </summary>
    [McpServerTool(Name = "object_summarize", Title = "Summarize Object",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Summarize an object's fields in a single call: non-default valued fields, null fields, and anomalous fields (empty strings, NaN, Infinity, default dates, empty GUIDs). Collection-typed fields show element count inline.")]
    public async Task<string> SummarizeObject(
        [Description("Variable name or expression evaluating to an object")]
        string expression,
        [Description("Max collection elements to preview inline for collection-typed fields (1-50, default: 5)")]
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
        _logger.ToolInvoked("object_summarize", JsonSerializer.Serialize(new { expression, max_preview_items, thread_id, frame_index }));

        try
        {
            var summary = await _summarizer.SummarizeAsync(expression, max_preview_items, thread_id, frame_index, timeout_ms, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("object_summarize", stopwatch.ElapsedMilliseconds);

            return JsonSerializer.Serialize(new
            {
                success = true,
                summary = new
                {
                    typeName = summary.TypeName,
                    size = summary.Size,
                    isNull = summary.IsNull,
                    totalFieldCount = summary.TotalFieldCount,
                    inaccessibleFieldCount = summary.InaccessibleFieldCount,
                    fields = summary.Fields.Select(f => new
                    {
                        name = f.Name,
                        type = f.Type,
                        value = f.Value,
                        collectionCount = f.CollectionCount,
                        collectionElementType = f.CollectionElementType
                    }),
                    nullFields = summary.NullFields,
                    interestingFields = summary.InterestingFields.Select(f => new
                    {
                        name = f.Name,
                        type = f.Type,
                        value = f.Value,
                        reason = f.Reason
                    })
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("paused") || ex.Message.Contains("Paused"))
        {
            _logger.ToolError("object_summarize", ErrorCodes.NotPaused);
            return CreateErrorResponse(ErrorCodes.NotPaused,
                "Process is not paused. Cannot inspect variables while running.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("session"))
        {
            _logger.ToolError("object_summarize", ErrorCodes.NoSession);
            return CreateErrorResponse(ErrorCodes.NoSession, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("available") || ex.Message.Contains("scope") || ex.Message.Contains("not found"))
        {
            _logger.ToolError("object_summarize", "VARIABLE_UNAVAILABLE");
            return CreateErrorResponse("variable_unavailable",
                $"Variable '{expression}' is not available in the current scope.");
        }
        catch (Exception ex)
        {
            _logger.ToolError("object_summarize", ErrorCodes.VariablesFailed);
            return CreateErrorResponse(ErrorCodes.VariablesFailed,
                $"Failed to summarize object: {ex.Message}",
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
