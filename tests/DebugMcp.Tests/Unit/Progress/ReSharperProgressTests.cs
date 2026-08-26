using DebugMcp.Models.ReSharper;
using DebugMcp.Services.ReSharper;
using DebugMcp.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Unit.Progress;

/// <summary>
/// FR-004: <c>resharper_inspect_solution</c>/<c>resharper_inspect_project</c> report the
/// corrected 3-stage sequence — acquiring engine → running inspection → parsing report — not
/// the 5-stage sequence an earlier draft claimed (progress-contract.md, data-model.md §4).
/// </summary>
public sealed class ReSharperProgressTests
{
    private readonly Mock<IReSharperEngineProvider> _provider = new();
    private readonly Mock<IReSharperRunner> _runner = new();
    private readonly InspectionReportParser _parser = new();
    private readonly ReSharperOptions _options = new();
    private readonly RecordingProgressReporter _progress = new();

    private const string EmptyReport = "<Report><IssueTypes/><Issues/></Report>";

    private ReSharperInspectionService CreateSut()
    {
        _provider
            .Setup(p => p.EnsureEngineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EngineInstallState("/fake/jb", "2026.1.2", Acquired: false));
        _runner
            .Setup(r => r.RunInspectCodeAsync(It.IsAny<InspectionRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyReport);
        return new ReSharperInspectionService(
            _provider.Object, _runner.Object, _parser, _options, NullLogger<ReSharperInspectionService>.Instance);
    }

    [Fact]
    public async Task InspectAsync_ReportsThreeStagesInOrder()
    {
        var sut = CreateSut();

        await sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None, _progress);

        _progress.Reported.Select(r => r.Stage).Should().Equal(
            "acquiring engine", "running inspection", "parsing report");
    }

    [Fact]
    public async Task InspectAsync_StagesAreNotCounted()
    {
        var sut = CreateSut();

        await sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None, _progress);

        _progress.Reported.Should().OnlyContain(r => r.Completed == null && r.Total == null);
    }
}
