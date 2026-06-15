using DebugMcp.Models.ReSharper;
using DebugMcp.Services.ReSharper;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Integration;

/// <summary>
/// OPT-IN integration test exercising the real ReSharper engine against the sample app.
/// Excluded from the fast suite (Unit|Contract filter); run explicitly via the Integration
/// filter. Triggers a one-time ~180 MB engine download on a clean machine.
/// Proves SC-002: ReSharper surfaces a ReSharper-only issue (RedundantCast) that the C#
/// compiler does not (the sample builds with zero compiler warnings).
/// </summary>
public sealed class ReSharperInspectionIntegrationTests
{
    private static string FindSampleSolution()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "ReSharperSampleApp", "ReSharperSampleApp.sln");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate tests/ReSharperSampleApp/ReSharperSampleApp.sln");
    }

    private static ReSharperInspectionService RealService()
    {
        var options = new ReSharperOptions();
        var provider = new ReSharperEngineProvider(options, NullLogger<ReSharperEngineProvider>.Instance);
        var runner = new ReSharperCliRunner(NullLogger<ReSharperCliRunner>.Instance);
        var parser = new InspectionReportParser();
        return new ReSharperInspectionService(provider, runner, parser, options, NullLogger<ReSharperInspectionService>.Instance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Inspect_SampleSolution_FindsRedundantCast_RoslynOnlyIssue()
    {
        var sln = FindSampleSolution();
        var sut = RealService();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var result = await sut.InspectAsync(sln, severity: null, project: null, noBuild: true,
            inspectionTimeoutSeconds: 600, maxResults: 500, cts.Token);

        result.Findings.Should().Contain(f => f.Id == "RedundantCast",
            "the sample contains a redundant cast that ReSharper flags but the C# compiler does not");
        result.EngineVersion.Should().Be(ReSharperOptions.DefaultVersion);
        result.Findings.Should().OnlyContain(f => f.Severity == ReSharperSeverity.Error
            || f.Severity == ReSharperSeverity.Warning
            || f.Severity == ReSharperSeverity.Suggestion
            || f.Severity == ReSharperSeverity.Hint);
    }
}
