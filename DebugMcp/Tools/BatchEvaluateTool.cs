using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DebugMcp.Models.Batch;
using DebugMcp.Services.Batch;
using Microsoft.Extensions.Logging;
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
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Submit a batch of up to 20 micro-experiments in one call. Each experiment specifies a source location trigger, optional variable capture expressions, an optional condition, and a max hit count. Pre-existing breakpoints are disabled during the batch and restored after. Returns a structured summary with all captured variable values, hit timestamps, and a completion reason. Example response: {\"success\": true, \"completion_reason\": \"all_triggered\", \"triggered\": 2, \"not_triggered\": 0, \"experiments\": [{\"index\": 0, \"status\": \"triggered\", \"hit_count\": 1, \"hits\": [{\"thread_id\": 1, \"values\": {\"counter\": \"42\"}}]}]}")]
    public async Task<string> BatchEvaluateAsync(
        [Description("JSON array of experiment objects. Each object: {\"trigger\": {\"file\": \"path.cs\", \"line\": N}, \"mode\": \"blocking|non_blocking\", \"capture\": [\"expr1\", \"expr2\"], \"condition\": \"x > 5\", \"max_hits\": 3}. Mode defaults to blocking. max_hits defaults to 1.")] string experiments,
        [Description("Timeout in seconds before batch returns partial results (default 30)")] int timeoutSeconds = 30,
        [Description("Evaluation safety mode: safe (default, blocks unsafe expressions) or full (allows all expressions)")] string evalMode = "safe",
        [Description("Maximum total hits across all experiments before ending early (default 500)")] int maxTotalHits = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var experimentList = ParseExperiments(experiments);
            var evalModeEnum = evalMode.Equals("full", StringComparison.OrdinalIgnoreCase)
                ? EvalMode.Full
                : EvalMode.Safe;

            var request = new BatchRequest(experimentList, timeoutSeconds, evalModeEnum, maxTotalHits);
            var result = await _batchRunner.RunAsync(request, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                completion_reason = ToSnakeCase(result.CompletionReason.ToString()),
                total_experiments = result.TotalExperiments,
                triggered = result.TriggeredCount,
                not_triggered = result.NotTriggeredCount,
                errors = result.ErrorCount,
                experiments = result.ExperimentResults.Select(r => new
                {
                    index = r.Index,
                    status = ToSnakeCase(r.Status.ToString()),
                    hit_count = r.HitCount,
                    error = r.ErrorMessage,
                    hits = r.Hits.Select(h => new
                    {
                        timestamp = h.Timestamp,
                        thread_id = h.ThreadId,
                        location = new { file = h.Location.File, line = h.Location.Line },
                        values = h.Values,
                        eval_errors = h.EvalErrors.Count > 0 ? h.EvalErrors : (IReadOnlyDictionary<string, string>?)null,
                    }),
                }),
            }, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
        }
        catch (ArgumentException ex)
        {
            return Fail("validation_error", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("batch_already_running"))
        {
            return Fail("batch_already_running", "A batch is already running. Only one batch can run at a time.");
        }
        catch (JsonException ex)
        {
            return Fail("invalid_json", $"Could not parse experiments JSON: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return Fail("cancelled", "Batch evaluation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in batch_evaluate");
            return Fail("internal_error", ex.Message);
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

    private static string Fail(string code, string message)
        => JsonSerializer.Serialize(new
        {
            success = false,
            error = new { code, message },
        }, new JsonSerializerOptions { WriteIndented = true });
}
