using System.Text.Json;
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

    private static (bool success, string? code) Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var success = root.GetProperty("success").GetBoolean();
        string? code = null;
        if (!success)
        {
            code = root.GetProperty("error").GetProperty("code").GetString();
        }
        return (success, code);
    }

    [Fact]
    public async Task Solution_EmptyPath_ReturnsInvalidPath()
    {
        var (success, code) = Parse(await SolutionTool().InspectSolutionAsync(""));
        success.Should().BeFalse();
        code.Should().Be(DebugMcp.Models.ErrorCodes.InvalidPath);
    }

    [Fact]
    public async Task Solution_WrongExtension_ReturnsInvalidPath()
    {
        var (success, code) = Parse(await SolutionTool().InspectSolutionAsync("/x/not-a-solution.txt"));
        success.Should().BeFalse();
        code.Should().Be(DebugMcp.Models.ErrorCodes.InvalidPath);
    }

    [Fact]
    public async Task Solution_MissingFile_ReturnsInvalidPath()
    {
        var (success, code) = Parse(await SolutionTool().InspectSolutionAsync("/nope/Missing.sln"));
        success.Should().BeFalse();
        code.Should().Be(DebugMcp.Models.ErrorCodes.InvalidPath);
    }

    [Fact]
    public async Task Project_WrongExtension_ReturnsInvalidPath()
    {
        var (success, code) = Parse(await ProjectTool().InspectProjectAsync("/x/App.sln"));
        success.Should().BeFalse();
        code.Should().Be(DebugMcp.Models.ErrorCodes.InvalidPath);
    }

    [Fact]
    public async Task Solution_ServiceException_MapsToErrorCode()
    {
        var sln = Path.Combine(Path.GetTempPath(), $"rs-tool-{Guid.NewGuid():N}.sln");
        await File.WriteAllTextAsync(sln, "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        _service.Setup(s => s.InspectAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReSharperAcquisitionException("offline"));

        var (success, code) = Parse(await SolutionTool().InspectSolutionAsync(sln));

        success.Should().BeFalse();
        code.Should().Be(DebugMcp.Models.ErrorCodes.EngineAcquisitionFailed);
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

        var (success, _) = Parse(await SolutionTool().InspectSolutionAsync(sln));
        success.Should().BeTrue();
        File.Delete(sln);
    }
}
