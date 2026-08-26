using DebugMcp.Models.Batch;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services.Batch;
using DebugMcp.Services.Progress;
using DebugMcp.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Batch;

public class BatchEvaluateToolTests
{
    private readonly Mock<IBatchRunner> _runnerMock;
    private readonly BatchEvaluateTool _tool;

    public BatchEvaluateToolTests()
    {
        _runnerMock = new Mock<IBatchRunner>();
        var logger = new Mock<ILogger<BatchEvaluateTool>>();
        _tool = new BatchEvaluateTool(_runnerMock.Object, logger.Object);
    }

    private const string OneExperiment = """[{"trigger":{"file":"src/App.cs","line":10}}]""";

    [Fact]
    public async Task BatchEvaluateAsync_Success_ReturnsSnakeCaseFieldsAndData()
    {
        var hit = new ExperimentHit(
            DateTimeOffset.UtcNow, 1,
            new BreakpointLocation("src/App.cs", 10, null),
            new Dictionary<string, string> { ["counter"] = "42" },
            new Dictionary<string, string>());
        var experimentResult = new ExperimentResult(0, ExperimentStatus.Triggered, 1, [hit]);
        var batchResult = new BatchResult(BatchCompletionReason.AllTriggered, 1, 1, 0, 0, [experimentResult]);
        _runnerMock.Setup(r => r.RunAsync(It.IsAny<BatchRequest>(), It.IsAny<CancellationToken>(), It.IsAny<IProgressReporter?>()))
            .ReturnsAsync(batchResult);

        var result = await _tool.BatchEvaluateAsync(OneExperiment);

        result.Success.Should().BeTrue();
        result.CompletionReason.Should().Be("all_triggered");
        result.TotalExperiments.Should().Be(1);
        result.Triggered.Should().Be(1);
        result.NotTriggered.Should().Be(0);
        result.Errors.Should().Be(0);
        result.Experiments.Should().HaveCount(1);
        var exp = result.Experiments![0];
        exp.Index.Should().Be(0);
        exp.Status.Should().Be("triggered");
        exp.HitCount.Should().Be(1);
        exp.Hits.Should().ContainSingle();
        var wireHit = exp.Hits.Single();
        wireHit.ThreadId.Should().Be(1);
        wireHit.Values.Should().ContainKey("counter").WhoseValue.Should().Be("42");
        wireHit.EvalErrors.Should().BeNull("legacy omitted eval_errors when empty");
        wireHit.Location.File.Should().Be("src/App.cs");
        wireHit.Location.Line.Should().Be(10);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task BatchEvaluateAsync_EvalErrorsPresent_AreCarriedThrough()
    {
        var hit = new ExperimentHit(
            DateTimeOffset.UtcNow, 1,
            new BreakpointLocation("src/App.cs", 10, null),
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["bad"] = "not in scope" });
        var experimentResult = new ExperimentResult(0, ExperimentStatus.Triggered, 1, [hit]);
        var batchResult = new BatchResult(BatchCompletionReason.AllTriggered, 1, 1, 0, 0, [experimentResult]);
        _runnerMock.Setup(r => r.RunAsync(It.IsAny<BatchRequest>(), It.IsAny<CancellationToken>(), It.IsAny<IProgressReporter?>()))
            .ReturnsAsync(batchResult);

        var result = await _tool.BatchEvaluateAsync(OneExperiment);

        result.Experiments![0].Hits.Single().EvalErrors.Should().ContainKey("bad");
    }

    [Fact]
    public async Task BatchEvaluateAsync_MissingTrigger_ReturnsValidationError()
    {
        var result = await _tool.BatchEvaluateAsync("""[{}]""");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("validation_error");
    }

    [Fact]
    public async Task BatchEvaluateAsync_MalformedJson_ReturnsInvalidJsonError()
    {
        var result = await _tool.BatchEvaluateAsync("not json");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("invalid_json");
    }

    [Fact]
    public async Task BatchEvaluateAsync_BatchAlreadyRunning_ReturnsBatchAlreadyRunningError()
    {
        _runnerMock.Setup(r => r.RunAsync(It.IsAny<BatchRequest>(), It.IsAny<CancellationToken>(), It.IsAny<IProgressReporter?>()))
            .ThrowsAsync(new InvalidOperationException("batch_already_running"));

        var result = await _tool.BatchEvaluateAsync(OneExperiment);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("batch_already_running");
    }
}
