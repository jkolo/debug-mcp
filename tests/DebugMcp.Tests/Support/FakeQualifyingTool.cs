using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Test-only MCP tool with a controllable completion gate, used by <see cref="InProcessMcpHarness"/>
/// to exercise MCP Tasks deferral (opt-in gating, lifecycle transitions, cancellation) without
/// depending on any real debugger/ReSharper/Roslyn service.
/// </summary>
[McpServerToolType]
public sealed class FakeQualifyingTool
{
    /// <summary>When set, RunAsync awaits this before returning — lets a test hold a call "Working".</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Every progress report RunAsync issued, in order — for asserting whether/how progress surfaces once deferred.</summary>
    public List<ProgressNotificationValue> ProgressReports { get; } = [];

    [McpServerTool(Name = "slow_qualifying_tool")]
    [Description("Test-only tool with a controllable completion gate.")]
    public async Task<string> RunAsync(
        [Description("ok | throw | domain_error | cancelwatch")] string mode = "ok",
        CancellationToken cancellationToken = default,
        IProgress<ProgressNotificationValue>? progress = null)
    {
        var report = new ProgressNotificationValue { Progress = 1, Total = 2, Message = "stage one" };
        ProgressReports.Add(report);
        progress?.Report(report);

        if (mode == "cancelwatch")
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (Gate is not null)
        {
            await Gate.Task;
        }

        if (mode == "throw")
        {
            throw new InvalidOperationException("boom");
        }

        if (mode == "domain_error")
        {
            return JsonSerializer.Serialize(new { success = false, error = new { code = "SIMULATED", message = "simulated domain failure" } });
        }

        return JsonSerializer.Serialize(new { success = true, value = "done:" + mode });
    }

    [McpServerTool(Name = "fast_tool")]
    [Description("Test-only tool that is never deferred.")]
    public Task<string> FastAsync(CancellationToken cancellationToken = default) => Task.FromResult("fast-ok");
}
