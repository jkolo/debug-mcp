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
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Summarize an object's fields in a single call: non-default valued fields, null fields, and anomalous fields (empty strings, NaN, Infinity, default dates, empty GUIDs). Collection-typed fields show element count inline.")]
    public async Task<ObjectSummarizeResult> SummarizeObject(
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

            return new ObjectSummarizeResult(
                Success: true,
                Summary: new ObjectSummaryResult(
                    TypeName: summary.TypeName,
                    Size: summary.Size,
                    IsNull: summary.IsNull,
                    TotalFieldCount: summary.TotalFieldCount,
                    InaccessibleFieldCount: summary.InaccessibleFieldCount,
                    Fields: summary.Fields.Select(f => new FieldSummaryResult(
                        Name: f.Name,
                        Type: f.Type,
                        Value: f.Value,
                        CollectionCount: f.CollectionCount,
                        CollectionElementType: f.CollectionElementType)).ToList(),
                    NullFields: summary.NullFields,
                    InterestingFields: summary.InterestingFields.Select(f => new InterestingFieldResult(
                        Name: f.Name,
                        Type: f.Type,
                        Value: f.Value,
                        Reason: f.Reason)).ToList()));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("paused") || ex.Message.Contains("Paused"))
        {
            _logger.ToolError("object_summarize", ErrorCodes.NotPaused);
            return CreateErrorResult(ErrorCodes.NotPaused,
                "Process is not paused. Cannot inspect variables while running.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("session"))
        {
            _logger.ToolError("object_summarize", ErrorCodes.NoSession);
            return CreateErrorResult(ErrorCodes.NoSession, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("available") || ex.Message.Contains("scope") || ex.Message.Contains("not found"))
        {
            _logger.ToolError("object_summarize", "VARIABLE_UNAVAILABLE");
            return CreateErrorResult("variable_unavailable",
                $"Variable '{expression}' is not available in the current scope.");
        }
        catch (Exception ex)
        {
            _logger.ToolError("object_summarize", ErrorCodes.VariablesFailed);
            return CreateErrorResult(ErrorCodes.VariablesFailed,
                $"Failed to summarize object: {ex.Message}",
                new { exceptionType = ex.GetType().Name });
        }
    }

    private static ObjectSummarizeResult CreateErrorResult(string code, string message, object? details = null)
    {
        return new ObjectSummarizeResult(Success: false, Error: new ToolError(code, message, details));
    }
}
