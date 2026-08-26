using DebugMcp.Models.ReSharper;
using DebugMcp.Services.ReSharper;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Unit.ReSharper;

public sealed class ReSharperToolsTests
{
    private readonly Mock<IReSharperInspectionService> _service = new();
    private readonly ReSharperOptions _options = new();

    private ReSharperInspectSolutionTool SolutionTool() =>
        new(_service.Object, _options, NullLogger<ReSharperInspectSolutionTool>.Instance);

    private ReSharperInspectProjectTool ProjectTool() =>
        new(_service.Object, _options, NullLogger<ReSharperInspectProjectTool>.Instance);

    [Fact]
    public async Task Solution_EmptyPath_ReturnsInvalidPath()
    {
        var result = await SolutionTool().InspectSolutionAsync("");
        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(DebugMcp.Models.ErrorCodes.InvalidPath);
    }

    [Fact]
    public async Task Solution_WrongExtension_ReturnsInvalidPath()
    {
        var result = await SolutionTool().InspectSolutionAsync("/x/not-a-solution.txt");
        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(DebugMcp.Models.ErrorCodes.InvalidPath);
    }

    [Fact]
    public async Task Solution_MissingFile_ReturnsInvalidPath()
    {
        var result = await SolutionTool().InspectSolutionAsync("/nope/Missing.sln");
        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(DebugMcp.Models.ErrorCodes.InvalidPath);
    }

    [Fact]
    public async Task Project_WrongExtension_ReturnsInvalidPath()
    {
        var result = await ProjectTool().InspectProjectAsync("/x/App.sln");
        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(DebugMcp.Models.ErrorCodes.InvalidPath);
    }

    [Fact]
    public async Task Solution_ServiceException_MapsToErrorCode()
    {
        var sln = Path.Combine(Path.GetTempPath(), $"rs-tool-{Guid.NewGuid():N}.sln");
        await File.WriteAllTextAsync(sln, "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        _service.Setup(s => s.InspectAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReSharperAcquisitionException("offline"));

        var result = await SolutionTool().InspectSolutionAsync(sln);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(DebugMcp.Models.ErrorCodes.EngineAcquisitionFailed);
        File.Delete(sln);
    }

    [Fact]
    public async Task Solution_HappyPath_ReturnsSuccessData()
    {
        var sln = Path.Combine(Path.GetTempPath(), $"rs-tool-{Guid.NewGuid():N}.sln");
        await File.WriteAllTextAsync(sln, "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        _service.Setup(s => s.InspectAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InspectionResult
            {
                Target = sln, Findings = [], TotalCount = 0, ReturnedCount = 0, Truncated = false,
                MaxResults = 500, Summary = new Dictionary<string, int>(), EngineVersion = "2026.1.2",
                DurationMs = 5, Built = true
            });

        var result = await SolutionTool().InspectSolutionAsync(sln);
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.Target.Should().Be(sln);
        File.Delete(sln);
    }
}
