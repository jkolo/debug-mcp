using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models.ReSharper;
using DebugMcp.Services.ReSharper;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-021/FR-017: a client that reads only <c>content[0].text</c>, as every client does today,
/// must see the same field names and meanings after US3's typed-result migration. Verified by
/// serializing each tool's returned record the way the SDK does (camelCase, nulls omitted — see
/// data-model.md §1), then parsing and comparing fields, not by string equality — the contract's
/// own wire example (contracts/tool-result-contract.md) is compact JSON, while today's
/// pre-migration hand-rolled tools used <c>WriteIndented = true</c>; indentation was never part
/// of the contract, field presence and values are. One case per migrated tool, added as each tool
/// moves off its hand-rolled <c>JsonSerializer.Serialize(new {...})</c> (T044–T052); this file
/// starts with the pilot (<c>snapshot_delete</c>) as the worked example the rest follows.
/// </summary>
public class LegacyTextContractTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public async Task SnapshotDelete_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var storeMock = new Mock<ISnapshotStore>();
        serviceMock.Setup(s => s.DeleteSnapshot("snap-1")).Returns(true);
        storeMock.Setup(s => s.Count).Returns(3);
        var tool = new SnapshotDeleteTool(serviceMock.Object, storeMock.Object, Mock.Of<ILogger<SnapshotDeleteTool>>());

        var result = await tool.DeleteSnapshotAsync("snap-1");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("deleted").GetString().Should().Be("snap-1");
        parsed.GetProperty("remaining").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task SnapshotDelete_Failure_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var storeMock = new Mock<ISnapshotStore>();
        serviceMock.Setup(s => s.DeleteSnapshot("snap-missing")).Returns(false);
        var tool = new SnapshotDeleteTool(serviceMock.Object, storeMock.Object, Mock.Of<ILogger<SnapshotDeleteTool>>());

        var result = await tool.DeleteSnapshotAsync("snap-missing");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("SNAPSHOT_NOT_FOUND");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResharperInspectSolution_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<IReSharperInspectionService>();
        var sln = Path.Combine(Path.GetTempPath(), $"rs-contract-{Guid.NewGuid():N}.sln");
        await File.WriteAllTextAsync(sln, "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        serviceMock.Setup(s => s.InspectAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InspectionResult
            {
                Target = sln,
                Findings = [new InspectionFinding { Id = "RedundantCast", Message = "Redundant cast", Severity = ReSharperSeverity.Warning }],
                TotalCount = 1, ReturnedCount = 1, Truncated = false, MaxResults = 500,
                Summary = new Dictionary<string, int> { ["warning"] = 1 }, EngineVersion = "2026.2.1",
                DurationMs = 42, Built = true
            });
        var tool = new ReSharperInspectSolutionTool(serviceMock.Object, new ReSharperOptions(),
            NullLogger<ReSharperInspectSolutionTool>.Instance);

        var result = await tool.InspectSolutionAsync(sln);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = parsed.GetProperty("data");
        data.GetProperty("target").GetString().Should().Be(sln);
        data.GetProperty("total_count").GetInt32().Should().Be(1);
        data.GetProperty("returned_count").GetInt32().Should().Be(1);
        data.GetProperty("truncated").GetBoolean().Should().BeFalse();
        data.GetProperty("limited_to").GetInt32().Should().Be(500);
        data.GetProperty("engine_version").GetString().Should().Be("2026.2.1");
        data.GetProperty("duration_ms").GetInt64().Should().Be(42);
        data.GetProperty("built").GetBoolean().Should().BeTrue();
        data.GetProperty("findings")[0].GetProperty("id").GetString().Should().Be("RedundantCast");
        data.GetProperty("findings")[0].GetProperty("severity").GetString().Should().Be("warning");
        data.GetProperty("summary").GetProperty("warning").GetInt32().Should().Be(1);
        File.Delete(sln);
    }

    [Fact]
    public async Task ResharperInspectSolution_Failure_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<IReSharperInspectionService>();
        var tool = new ReSharperInspectSolutionTool(serviceMock.Object, new ReSharperOptions(),
            NullLogger<ReSharperInspectSolutionTool>.Instance);

        var result = await tool.InspectSolutionAsync("/nope/Missing.sln");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_PATH");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        parsed.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ResharperInspectProject_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<IReSharperInspectionService>();
        var csproj = Path.Combine(Path.GetTempPath(), $"rs-contract-{Guid.NewGuid():N}.csproj");
        await File.WriteAllTextAsync(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n");
        serviceMock.Setup(s => s.InspectAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InspectionResult
            {
                Target = csproj, Findings = [], TotalCount = 0, ReturnedCount = 0, Truncated = false,
                MaxResults = 500, Summary = new Dictionary<string, int>(), EngineVersion = "2026.2.1",
                DurationMs = 7, Built = true
            });
        var tool = new ReSharperInspectProjectTool(serviceMock.Object, new ReSharperOptions(),
            NullLogger<ReSharperInspectProjectTool>.Instance);

        var result = await tool.InspectProjectAsync(csproj);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = parsed.GetProperty("data");
        data.GetProperty("target").GetString().Should().Be(csproj);
        data.GetProperty("returned_count").GetInt32().Should().Be(0);
        data.GetProperty("engine_version").GetString().Should().Be("2026.2.1");
        File.Delete(csproj);
    }

    [Fact]
    public async Task ResharperInspectProject_Failure_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<IReSharperInspectionService>();
        var tool = new ReSharperInspectProjectTool(serviceMock.Object, new ReSharperOptions(),
            NullLogger<ReSharperInspectProjectTool>.Instance);

        var result = await tool.InspectProjectAsync("/x/App.sln");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_PATH");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        parsed.TryGetProperty("data", out _).Should().BeFalse();
    }
}
