using DebugMcp.Models;
using DebugMcp.Models.ReSharper;
using DebugMcp.Services;
using DebugMcp.Services.ReSharper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Unit.Progress;

/// <summary>
/// FR-005: a tool with no progress reporter attached completes normally — the SDK discards
/// reports when the client supplied no progress token, and no service may assume a reporter
/// is present. <c>progress: null</c> must always be safe.
/// </summary>
public class ProgressDegradationTests
{
    [Fact]
    public async Task ReSharperInspectAsync_NoProgressReporter_CompletesNormally()
    {
        var provider = new Mock<IReSharperEngineProvider>();
        provider.Setup(p => p.EnsureEngineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EngineInstallState("/fake/jb", "2026.1.2", Acquired: false));
        var runner = new Mock<IReSharperRunner>();
        runner.Setup(r => r.RunInspectCodeAsync(It.IsAny<InspectionRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<Report><IssueTypes/><Issues/></Report>");
        var sut = new ReSharperInspectionService(
            provider.Object, runner.Object, new InspectionReportParser(), new ReSharperOptions(),
            NullLogger<ReSharperInspectionService>.Instance);

        var act = () => sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None, progress: null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LaunchAsync_NoProgressReporter_CompletesNormally()
    {
        var processDebuggerMock = new Mock<IProcessDebugger>();
        processDebuggerMock
            .Setup(x => x.LaunchAsync(
                "app.dll", null, null, null, true, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessInfo(
                Pid: 1, Name: "app", ExecutablePath: "app.dll", IsManaged: true,
                CommandLine: null, RuntimeVersion: ".NET 10.0"));
        var sut = new DebugSessionManager(processDebuggerMock.Object, Mock.Of<ILogger<DebugSessionManager>>());

        var act = () => sut.LaunchAsync("app.dll", progress: null);

        await act.Should().NotThrowAsync();
    }
}
