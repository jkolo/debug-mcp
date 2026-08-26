using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DebugMcp.Models;
using DebugMcp.Models.Batch;
using DebugMcp.Models.Results;
using DebugMcp.Services.Batch;
using DebugMcp.Services.Progress;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

[McpServerToolType]
public sealed class BatchEvaluateTool
{
    private readonly IBatchRunner _batchRunner;
    private readonly ILogger<BatchEvaluateTool> _logger;

    public BatchEvaluateTool(IBatchRunner batchRunner, ILogger<BatchEvaluateTool> logger)
    {
        _batchRunner = batchRunner;
        _logger = logger;
    }

    [McpServerTool(Name = "batch_evaluate", Title = "Batch Evaluate",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Submit a batch of up to 20 micro-experiments in one call. Each experiment specifies a source location trigger, optional variable capture expressions, an optional condition, and a max hit count. Pre-existing breakpoints are disabled during the batch and restored after. Returns a structured summary with all captured variable values, hit timestamps, and a completion reason. Example response: {\"success\": true, \"completion_reason\": \"all_triggered\", \"triggered\": 2, \"not_triggered\": 0, \"experiments\": [{\"index\": 0, \"status\": \"triggered\", \"hit_count\": 1, \"hits\": [{\"thread_id\": 1, \"values\": {\"counter\": \"42\"}}]}]}")]
    public async Task<BatchEvaluateResult> BatchEvaluateAsync(
        [Description("JSON array of experiment objects. Each object: {\"trigger\": {\"file\": \"path.cs\", \"line\": N}, \"mode\": \"blocking|non_blocking\", \"capture\": [\"expr1\", \"expr2\"], \"condition\": \"x > 5\", \"max_hits\": 3}. Mode defaults to blocking. max_hits defaults to 1.")] string experiments,
        [Description("Timeout in seconds before batch returns partial results (default 30)")] int timeoutSeconds = 30,
        [Description("Evaluation safety mode: safe (default, blocks unsafe expressions) or full (allows all expressions)")] string evalMode = "safe",
        [Description("Maximum total hits across all experiments before ending early (default 500)")] int maxTotalHits = 500,
        CancellationToken cancellationToken = default,
        IProgress<ProgressNotificationValue>? progress = null)
    {
        try
        {
            var experimentList = ParseExperiments(experiments);
            var evalModeEnum = evalMode.Equals("full", StringComparison.OrdinalIgnoreCase)
                ? EvalMode.Full
                : EvalMode.Safe;

            var request = new BatchRequest(experimentList, timeoutSeconds, evalModeEnum, maxTotalHits);
            var result = await _batchRunner.RunAsync(request, cancellationToken, ProgressReporterAdapter.Create(progress));

            return new BatchEvaluateResult(
                Success: true,
                CompletionReason: ToSnakeCase(result.CompletionReason.ToString()),
                TotalExperiments: result.TotalExperiments,
                Triggered: result.TriggeredCount,
                NotTriggered: result.NotTriggeredCount,
                Errors: result.ErrorCount,
                Experiments: result.ExperimentResults.Select(r => new BatchExperimentResultWire(
                    Index: r.Index,
                    Status: ToSnakeCase(r.Status.ToString()),
                    HitCount: r.HitCount,
                    Error: r.ErrorMessage,
                    Hits: r.Hits.Select(h => new BatchExperimentHitWire(
                        Timestamp: h.Timestamp,
                        ThreadId: h.ThreadId,
                        Location: new BatchHitLocationWire(h.Location.File, h.Location.Line),
                        Values: h.Values,
                        EvalErrors: h.EvalErrors.Count > 0 ? h.EvalErrors : null)))).ToList());
        }
        catch (ArgumentException ex)
        {
            return Fail(ErrorCodes.ValidationError, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("batch_already_running"))
        {
            return Fail(ErrorCodes.BatchAlreadyRunning, "A batch is already running. Only one batch can run at a time.");
        }
        catch (JsonException ex)
        {
            return Fail(ErrorCodes.InvalidJson, $"Could not parse experiments JSON: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return Fail(ErrorCodes.Cancelled, "Batch evaluation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in batch_evaluate");
            return Fail(ErrorCodes.InternalError, ex.Message);
        }
    }

    private static IReadOnlyList<Experiment> ParseExperiments(string json)
    {
        var array = JsonNode.Parse(json)?.AsArray()
            ?? throw new ArgumentException("experiments must be a JSON array");

        var list = new List<Experiment>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            var node = array[i] ?? throw new ArgumentException($"experiments[{i}] is null");

            var triggerNode = node["trigger"] ?? throw new ArgumentException($"experiments[{i}].trigger is required");
            var file = triggerNode["file"]?.GetValue<string>()
                ?? throw new ArgumentException($"experiments[{i}].trigger.file is required");
            var line = triggerNode["line"]?.GetValue<int>()
                ?? throw new ArgumentException($"experiments[{i}].trigger.line is required");
            var trigger = new ExperimentTrigger.SourceLocation(file, line);

            var modeStr = node["mode"]?.GetValue<string>();
            var mode = modeStr?.ToLowerInvariant() is "non_blocking" or "nonblocking"
                ? ExperimentMode.NonBlocking
                : ExperimentMode.Blocking;

            IReadOnlyList<string>? capture = null;
            if (node["capture"]?.AsArray() is { } captureArray && captureArray.Count > 0)
                capture = captureArray.Select(n => n?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToList();

            var condition = node["condition"]?.GetValue<string>();
            var maxHits = node["max_hits"]?.GetValue<int>() ?? 1;

            list.Add(new Experiment(trigger, mode, capture, condition, maxHits));
        }
        return list;
    }

    private static string ToSnakeCase(string value)
    {
        var sb = new StringBuilder(value.Length + 4);
        foreach (var c in value)
        {
            if (char.IsUpper(c) && sb.Length > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static BatchEvaluateResult Fail(string code, string message)
        => new(Success: false, Error: new ToolError(code, message));
}
