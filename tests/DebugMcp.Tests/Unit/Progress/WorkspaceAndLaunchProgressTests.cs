using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Services.CodeAnalysis;
using DebugMcp.Tests.Support;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Tests.Unit.Progress;

/// <summary>
/// FR-004: <c>debug_launch</c> and <c>code_load</c> report their (corrected — see
/// progress-contract.md) stage sequences.
/// </summary>
public class WorkspaceAndLaunchProgressTests
{
    [Fact]
    public async Task LaunchAsync_ReportsStartingProcessThenReady()
    {
        var processDebuggerMock = new Mock<IProcessDebugger>();
        processDebuggerMock
            .Setup(x => x.LaunchAsync(
                "app.dll", null, null, null, true, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 4242, Name: "app", ExecutablePath: "app.dll", IsManaged: true,
                CommandLine: null, RuntimeVersion: ".NET 10.0"));
        var sut = new DebugSessionManager(processDebuggerMock.Object, Mock.Of<ILogger<DebugSessionManager>>());
        var progress = new RecordingProgressReporter();

        await sut.LaunchAsync("app.dll", progress: progress);

        progress.Reported.Select(r => r.Stage).Should().Equal("starting process", "ready");
    }

    [Fact]
    public async Task LoadAsync_MultiProjectTarget_ReportsIncreasingProjectCount()
    {
        // TestTargetApp.csproj transitively references several Libs/* projects, giving a real
        // multi-project load — no fake/mocked MSBuildWorkspace exists to substitute.
        //
        // Total is deliberately null throughout: Roslyn's IProgress<ProjectLoadProgress> reports
        // projects as it discovers them (a .csproj's transitive ProjectReference graph isn't
        // known upfront without duplicating MSBuild's own evaluation), so "how many, of how
        // many" isn't answerable until the load has already finished — data-model.md §4's
        // "Total: int? — null when not knowable in advance" applies here, not the closed-count
        // case `batch_evaluate` has.
        using var sut = new CodeAnalysisService();
        var progress = new RecordingProgressReporter();
        var path = GetTestTargetAppProjectPath();

        var workspace = await sut.LoadAsync(path, progress: progress);

        var loadingUpdates = progress.Reported.Where(r => r.Stage == "loading workspace").ToList();
        loadingUpdates.Should().NotBeEmpty();
        loadingUpdates.Select(r => r.Total).Should().OnlyContain(t => t == null);
        loadingUpdates.Select(r => r.Completed).Should().BeInAscendingOrder();
        loadingUpdates.Last().Completed.Should().Be(workspace.Projects.Count);
    }

    private static string GetTestTargetAppProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new InvalidOperationException("Could not locate repository root from test base directory.");
        }

        return Path.Combine(dir.FullName, "tests", "TestTargetApp", "TestTargetApp.csproj");
    }
}
