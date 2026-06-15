using DebugMcp.Models.ReSharper;
using DebugMcp.Services.ReSharper;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Unit.ReSharper;

public sealed class ReSharperInspectionServiceTests
{
    private readonly Mock<IReSharperEngineProvider> _provider = new();
    private readonly Mock<IReSharperRunner> _runner = new();
    private readonly InspectionReportParser _parser = new();
    private readonly ReSharperOptions _options = new();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ReSharper", name));

    private const string EmptyReport = "<Report><IssueTypes/><Issues/></Report>";

    private ReSharperInspectionService CreateSut()
    {
        _provider
            .Setup(p => p.EnsureEngineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EngineInstallState("/fake/jb", "2026.1.2", Acquired: false));
        return new ReSharperInspectionService(_provider.Object, _runner.Object, _parser, _options, NullLogger<ReSharperInspectionService>.Instance);
    }

    private void RunnerReturns(string report) =>
        _runner.Setup(r => r.RunInspectCodeAsync(It.IsAny<InspectionRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

    // ── US1 happy path ────────────────────────────────────────────────────

    [Fact]
    public async Task InspectAsync_HappyPath_ReturnsFindingsWithMetadata()
    {
        RunnerReturns(Fixture("parser-fixture.xml"));
        var sut = CreateSut();

        var result = await sut.InspectAsync("/x/My.sln", null, null, noBuild: false, 300, 500, CancellationToken.None);

        result.TotalCount.Should().Be(7);
        result.ReturnedCount.Should().Be(7);
        result.Truncated.Should().BeFalse();
        result.EngineVersion.Should().Be("2026.1.2");
        result.Built.Should().BeTrue();
        result.Target.Should().Be(Path.GetFullPath("/x/My.sln"));
        result.Summary.Should().ContainKey("warning").And.ContainKey("suggestion").And.ContainKey("hint").And.ContainKey("error");
    }

    [Fact]
    public async Task InspectAsync_NoFindings_ReturnsEmptySuccess()
    {
        RunnerReturns(EmptyReport);
        var sut = CreateSut();

        var result = await sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None);

        result.TotalCount.Should().Be(0);
        result.Findings.Should().BeEmpty();
        result.Truncated.Should().BeFalse();
    }

    // ── US2 filtering / scope / build control ──────────────────────────────

    [Fact]
    public async Task InspectAsync_SeverityWarning_DropsSuggestionsAndHints()
    {
        RunnerReturns(Fixture("parser-fixture.xml"));
        var sut = CreateSut();

        var result = await sut.InspectAsync("/x/My.sln", "warning", null, false, 300, 500, CancellationToken.None);

        result.Findings.Should().OnlyContain(f => f.Severity >= ReSharperSeverity.Warning);
        result.Findings.Should().NotContain(f => f.Severity == ReSharperSeverity.Suggestion || f.Severity == ReSharperSeverity.Hint);
    }

    [Fact]
    public async Task InspectAsync_MaxResults_CapsAndFlagsTruncated()
    {
        RunnerReturns(Fixture("parser-fixture.xml"));
        var sut = CreateSut();

        var result = await sut.InspectAsync("/x/My.sln", null, null, false, 300, maxResults: 2, CancellationToken.None);

        result.ReturnedCount.Should().Be(2);
        result.TotalCount.Should().Be(7);
        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task InspectAsync_NoBuild_SetsBuiltFalseAndPassesFlag()
    {
        InspectionRunRequest? captured = null;
        _runner.Setup(r => r.RunInspectCodeAsync(It.IsAny<InspectionRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<InspectionRunRequest, string, CancellationToken>((req, _, _) => captured = req)
            .ReturnsAsync(EmptyReport);
        var sut = CreateSut();

        var result = await sut.InspectAsync("/x/My.sln", null, null, noBuild: true, 300, 500, CancellationToken.None);

        result.Built.Should().BeFalse();
        captured!.NoBuild.Should().BeTrue();
    }

    [Fact]
    public async Task InspectAsync_ValidProjectScope_PassesProjectToRunner()
    {
        var sln = WriteTempSln("KnownProj");
        InspectionRunRequest? captured = null;
        _runner.Setup(r => r.RunInspectCodeAsync(It.IsAny<InspectionRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<InspectionRunRequest, string, CancellationToken>((req, _, _) => captured = req)
            .ReturnsAsync(EmptyReport);
        var sut = CreateSut();

        await sut.InspectAsync(sln, null, "KnownProj", false, 300, 500, CancellationToken.None);

        captured!.Project.Should().Be("KnownProj");
    }

    [Fact]
    public async Task InspectAsync_UnknownProjectScope_ThrowsProjectNotFound()
    {
        var sln = WriteTempSln("KnownProj");
        var sut = CreateSut();

        var act = () => sut.InspectAsync(sln, null, "Ghost", false, 300, 500, CancellationToken.None);

        (await act.Should().ThrowAsync<ReSharperProjectNotFoundException>())
            .Which.Code.Should().Be(DebugMcp.Models.ErrorCodes.ProjectNotFound);
    }

    [Fact]
    public async Task InspectAsync_InvalidSeverity_ThrowsArgumentException()
    {
        RunnerReturns(EmptyReport);
        var sut = CreateSut();

        var act = () => sut.InspectAsync("/x/My.sln", "bogus", null, false, 300, 500, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── US3 error mapping / timeouts ───────────────────────────────────────

    [Fact]
    public async Task InspectAsync_PrerequisiteMissing_Propagates()
    {
        _provider.Setup(p => p.EnsureEngineAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReSharperPrerequisiteException("no dotnet"));
        var sut = new ReSharperInspectionService(_provider.Object, _runner.Object, _parser, _options, NullLogger<ReSharperInspectionService>.Instance);

        var act = () => sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None);
        (await act.Should().ThrowAsync<ReSharperPrerequisiteException>())
            .Which.Code.Should().Be(DebugMcp.Models.ErrorCodes.PrerequisiteMissing);
    }

    [Fact]
    public async Task InspectAsync_AcquisitionCancelled_BecomesAcquisitionTimeout()
    {
        _provider.Setup(p => p.EnsureEngineAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var sut = new ReSharperInspectionService(_provider.Object, _runner.Object, _parser, _options, NullLogger<ReSharperInspectionService>.Instance);

        var act = () => sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ReSharperTimeoutException>()).Which;
        ex.Phase.Should().Be("acquisition");
        ex.Code.Should().Be(DebugMcp.Models.ErrorCodes.Timeout);
    }

    [Fact]
    public async Task InspectAsync_InspectionCancelled_BecomesInspectionTimeout()
    {
        _runner.Setup(r => r.RunInspectCodeAsync(It.IsAny<InspectionRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var sut = CreateSut();

        var act = () => sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ReSharperTimeoutException>()).Which;
        ex.Phase.Should().Be("inspection");
    }

    [Fact]
    public async Task InspectAsync_BuildFailure_Propagates()
    {
        _runner.Setup(r => r.RunInspectCodeAsync(It.IsAny<InspectionRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReSharperBuildFailedException("build broke"));
        var sut = CreateSut();

        var act = () => sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None);
        (await act.Should().ThrowAsync<ReSharperBuildFailedException>())
            .Which.Code.Should().Be(DebugMcp.Models.ErrorCodes.BuildFailed);
    }

    [Fact]
    public async Task InspectAsync_MalformedReport_ThrowsParseException()
    {
        RunnerReturns("<Report><Issues>");
        var sut = CreateSut();

        var act = () => sut.InspectAsync("/x/My.sln", null, null, false, 300, 500, CancellationToken.None);
        (await act.Should().ThrowAsync<InspectionReportParseException>())
            .Which.Code.Should().Be(DebugMcp.Models.ErrorCodes.InspectionFailed);
    }

    private static string WriteTempSln(string projectName)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rs-test-{Guid.NewGuid():N}.sln");
        File.WriteAllText(path,
            "Microsoft Visual Studio Solution File, Format Version 12.00\n" +
            $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{projectName}\", \"{projectName}\\{projectName}.csproj\", \"{{11111111-1111-1111-1111-111111111111}}\"\n" +
            "EndProject\n");
        return path;
    }
}
