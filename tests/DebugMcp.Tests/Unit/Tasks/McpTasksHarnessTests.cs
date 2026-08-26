using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Services.Tasks;
using DebugMcp.Tests.Support;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using Xunit;

namespace DebugMcp.Tests.Unit.Tasks;

/// <summary>
/// Wire-level MCP Tasks behavior, verified against a real MCP client and server connected over an
/// in-process duplex transport (<see cref="InProcessMcpHarness"/>). research.md originally assumed
/// this needed a stdio smoke test; it does not — the SDK's own <c>StreamServerTransport</c> /
/// <c>StreamClientTransport</c> pair makes it a deterministic, fast unit test.
///
/// Central finding these tests encode: the SDK owns the entire task lifecycle around a deferred
/// tool call. It runs the tool method in the background and reports Completed/Cancelled based on
/// whether that <c>Task&lt;string&gt;</c> returns or is cancelled — an uncaught exception is
/// caught by the SDK itself and turned into a Completed task whose result carries
/// <c>isError:true</c>, never a Failed task. Since every one of DebugMcp's 5 qualifying tools
/// already catches its own exceptions and returns a structured <c>{success:false,...}</c> JSON
/// string, "Failed" is not a status our tools can organically reach — the correct, and already
/// satisfied, contract is: a deferred call's terminal Result carries the exact same JSON shape
/// the synchronous call would have returned.
/// </summary>
public sealed class McpTasksHarnessTests
{
    [Fact]
    public async Task OptedInClient_QualifyingTool_IsDeferredAsTask()
    {
        await using var harness = await InProcessMcpHarness.StartAsync(declareTasksCapability: true);
        harness.Tool.Gate = new TaskCompletionSource();

        var created = await harness.Client.CallToolAsTaskAsync(SlowCall("ok"), CancellationToken.None);

        created.IsTask.Should().BeTrue("a client that declared the tasks extension calling an Optional-mode tool must get a task handle");
        RequireTask(created).Status.Should().Be(McpTaskStatus.Working);

        harness.Tool.Gate.TrySetResult();
    }

    [Fact]
    public async Task ClientWithoutCapability_QualifyingTool_ReceivesDirectResultNeverATask()
    {
        await using var harness = await InProcessMcpHarness.StartAsync(declareTasksCapability: false);
        harness.Tool.Gate = new TaskCompletionSource();
        harness.Tool.Gate.SetResult();

        var result = await harness.Client.CallToolAsync(
            "slow_qualifying_tool", new Dictionary<string, object?> { ["mode"] = "ok" }, cancellationToken: CancellationToken.None);

        result.ResultType.Should().NotBe("task", "the server must never defer a call for a client that did not declare the MCP Tasks extension");
    }

    [Fact]
    public async Task DeferredResult_MatchesTheDirectSynchronousResult_ByteForByte()
    {
        await using var directHarness = await InProcessMcpHarness.StartAsync(declareTasksCapability: false);
        directHarness.Tool.Gate = new TaskCompletionSource();
        directHarness.Tool.Gate.SetResult();
        var direct = await directHarness.Client.CallToolAsync(
            "slow_qualifying_tool", new Dictionary<string, object?> { ["mode"] = "ok" }, cancellationToken: CancellationToken.None);

        await using var deferredHarness = await InProcessMcpHarness.StartAsync(declareTasksCapability: true);
        deferredHarness.Tool.Gate = new TaskCompletionSource();
        var created = await deferredHarness.Client.CallToolAsTaskAsync(SlowCall("ok"), CancellationToken.None);
        deferredHarness.Tool.Gate.TrySetResult();
        var finalStatus = await PollUntilTerminalAsync(deferredHarness.Client, RequireTask(created).TaskId);
        var deferredResult = await deferredHarness.RawStore.GetTaskAsync(RequireTask(created).TaskId, CancellationToken.None);

        finalStatus.Should().Be(McpTaskStatus.Completed);
        var deferredText = deferredResult!.Result!.Value.GetProperty("content")[0].GetProperty("text").GetString();
        var directText = direct.Content[0].Should().BeOfType<TextContentBlock>().Subject.Text;
        deferredText.Should().Be(directText, "the SDK must run the same tool method either way — deferral must not change the payload");
    }

    [Fact]
    public async Task UncaughtException_CompletesTheTaskWithIsError_NeverFailed()
    {
        await using var harness = await InProcessMcpHarness.StartAsync(declareTasksCapability: true);
        harness.Tool.Gate = new TaskCompletionSource();
        var created = await harness.Client.CallToolAsTaskAsync(SlowCall("throw"), CancellationToken.None);
        harness.Tool.Gate.TrySetResult();

        var finalStatus = await PollUntilTerminalAsync(harness.Client, RequireTask(created).TaskId);
        var stored = await harness.RawStore.GetTaskAsync(RequireTask(created).TaskId, CancellationToken.None);

        finalStatus.Should().Be(McpTaskStatus.Completed, "the SDK's tool-call wrapper converts an uncaught exception into isError:true, not a Failed task");
        stored!.Result!.Value.GetProperty("isError").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DomainFailure_SurvivesDeferral_WithTheSameStructuredJsonContract()
    {
        await using var harness = await InProcessMcpHarness.StartAsync(declareTasksCapability: true);
        harness.Tool.Gate = new TaskCompletionSource();
        var created = await harness.Client.CallToolAsTaskAsync(SlowCall("domain_error"), CancellationToken.None);
        harness.Tool.Gate.TrySetResult();

        await PollUntilTerminalAsync(harness.Client, RequireTask(created).TaskId);
        var stored = await harness.RawStore.GetTaskAsync(RequireTask(created).TaskId, CancellationToken.None);

        var text = stored!.Result!.Value.GetProperty("content")[0].GetProperty("text").GetString();
        var payload = JsonSerializer.Deserialize<JsonElement>(text!);
        payload.GetProperty("success").GetBoolean().Should().BeFalse();
        payload.GetProperty("error").GetProperty("code").GetString().Should().Be("SIMULATED");
    }

    [Fact]
    public async Task ExpiredTaskId_ThrowsADifferentErrorThanAnUnknownTaskId()
    {
        // TTL must comfortably outlast PollUntilTerminalAsync's own worst-case budget (up to
        // 100 * 20ms = 2000ms) -- a TTL shorter than that races the task expiring mid-poll
        // (observed on a contended CI runner: GetTaskAsync failed with a generic remote error
        // from inside the completion-poll loop, before the test ever reached the expiry check).
        await using var harness = await InProcessMcpHarness.StartAsync(
            declareTasksCapability: true, taskTimeToLive: TimeSpan.FromSeconds(3));
        harness.Tool.Gate = new TaskCompletionSource();
        harness.Tool.Gate.SetResult();
        var created = await harness.Client.CallToolAsTaskAsync(SlowCall("ok"), CancellationToken.None);
        await PollUntilTerminalAsync(harness.Client, RequireTask(created).TaskId);
        await Task.Delay(TimeSpan.FromSeconds(3.5));

        var expiredError = await Catch(() => McpTasksClientExtensions.GetTaskAsync(harness.Client, RequireTask(created).TaskId, CancellationToken.None).AsTask());
        var unknownError = await Catch(() => McpTasksClientExtensions.GetTaskAsync(harness.Client, "never-created-id", CancellationToken.None).AsTask());

        expiredError.Should().NotBeNull();
        unknownError.Should().NotBeNull();
        expiredError!.Message.Should().NotBe(unknownError!.Message, "FR-012 requires expired and unknown task ids to be distinguishable");
    }

    [Fact]
    public async Task TasksCancel_PropagatesToTheToolsCancellationToken()
    {
        // Deterministic — poll for the real signals (the tool having reached its cancellation
        // wait point; the store having recorded the terminal status) instead of blind fixed
        // delays, which raced under CI scheduling contention (same class of flake fixed above
        // for HeartbeatProgressTests and ExpiredTaskId_ThrowsADifferentErrorThanAnUnknownTaskId).
        await using var harness = await InProcessMcpHarness.StartAsync(declareTasksCapability: true);
        var created = await harness.Client.CallToolAsTaskAsync(SlowCall("cancelwatch"), CancellationToken.None);
        for (var i = 0; i < 100 && harness.Tool.ProgressReports.Count == 0; i++)
        {
            await Task.Delay(20);
        }
        harness.Tool.ProgressReports.Should().NotBeEmpty("the tool must have started running before we cancel it");

        await McpTasksClientExtensions.CancelTaskAsync(harness.Client, RequireTask(created).TaskId, CancellationToken.None);

        McpTaskInfo? stored = null;
        for (var i = 0; i < 100; i++)
        {
            stored = await harness.RawStore.GetTaskAsync(RequireTask(created).TaskId, CancellationToken.None);
            if (stored?.Status == McpTaskStatus.Cancelled)
            {
                break;
            }
            await Task.Delay(20);
        }
        stored!.Status.Should().Be(McpTaskStatus.Cancelled, "the CancellationToken threaded through every tool (Phase 3) is exactly what tasks/cancel needs to work — no extra plumbing required");
    }

    [Fact]
    public async Task Progress_ReportedDuringADeferredCall_DoesNotAppearInThePolledStatusMessage()
    {
        // Empirically confirmed against SDK 2.2.0: IMcpTaskStore has no method to update
        // StatusMessage mid-flight, and RequestContext.Items carries no ambient task id a tool
        // could use with SendTaskStatusNotificationAsync. Progress still reports via the normal
        // MCP notifications/progress channel — this only documents that the polled task's
        // StatusMessage field is not that channel. If a future SDK version bridges this, this
        // test starts failing and T036 should be revisited for real.
        await using var harness = await InProcessMcpHarness.StartAsync(declareTasksCapability: true);
        harness.Tool.Gate = new TaskCompletionSource();
        var created = await harness.Client.CallToolAsTaskAsync(SlowCall("ok"), CancellationToken.None);

        for (var i = 0; i < 50 && harness.Tool.ProgressReports.Count == 0; i++)
        {
            await Task.Delay(20);
        }
        harness.Tool.ProgressReports.Should().NotBeEmpty("the tool did report progress");

        var mid = await McpTasksClientExtensions.GetTaskAsync(harness.Client, RequireTask(created).TaskId, CancellationToken.None);
        mid.StatusMessage.Should().BeNullOrEmpty();

        harness.Tool.Gate.TrySetResult();
    }

    private static CreateTaskResult RequireTask(ResultOrCreatedTask<CallToolResult> created) =>
        created.IsTask
            ? created.TaskCreated!
            : throw new InvalidOperationException("Expected the call to be deferred as a task, but it completed synchronously.");

    private static CallToolRequestParams SlowCall(string mode) => new()
    {
        Name = "slow_qualifying_tool",
        Arguments = new Dictionary<string, JsonElement> { ["mode"] = JsonSerializer.SerializeToElement(mode) },
    };

    private static async Task<McpTaskStatus> PollUntilTerminalAsync(ModelContextProtocol.Client.McpClient client, string taskId)
    {
        for (var i = 0; i < 100; i++)
        {
            var polled = await McpTasksClientExtensions.GetTaskAsync(client, taskId, CancellationToken.None);
            if (polled.Status is McpTaskStatus.Completed or McpTaskStatus.Failed or McpTaskStatus.Cancelled)
            {
                return polled.Status;
            }
            await Task.Delay(20);
        }
        throw new TimeoutException($"Task {taskId} did not reach a terminal status in time.");
    }

    private static async Task<Exception?> Catch(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
