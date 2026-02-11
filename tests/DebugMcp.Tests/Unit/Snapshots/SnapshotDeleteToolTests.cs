using System.Text.Json;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotDeleteToolTests
{
    private readonly Mock<ISnapshotService> _serviceMock;
    private readonly Mock<ISnapshotStore> _storeMock;
    private readonly SnapshotDeleteTool _tool;

    public SnapshotDeleteToolTests()
    {
        _serviceMock = new Mock<ISnapshotService>();
        _storeMock = new Mock<ISnapshotStore>();
        var logger = new Mock<ILogger<SnapshotDeleteTool>>();
        _tool = new SnapshotDeleteTool(_serviceMock.Object, _storeMock.Object, logger.Object);
    }

    [Fact]
    public void DeleteSnapshot_ById_ReturnsSuccessWithRemaining()
    {
        _serviceMock.Setup(s => s.DeleteSnapshot("snap-1")).Returns(true);
        _storeMock.Setup(s => s.Count).Returns(4);

        var result = _tool.DeleteSnapshot("snap-1");

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("deleted").GetString().Should().Be("snap-1");
        root.GetProperty("remaining").GetInt32().Should().Be(4);
    }

    [Fact]
    public void DeleteSnapshot_NotFound_ReturnsErrorJson()
    {
        _serviceMock.Setup(s => s.DeleteSnapshot("snap-missing")).Returns(false);

        var result = _tool.DeleteSnapshot("snap-missing");

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("SNAPSHOT_NOT_FOUND");
    }

    [Fact]
    public void DeleteSnapshot_NoId_ClearsAll()
    {
        var result = _tool.DeleteSnapshot(null);

        _serviceMock.Verify(s => s.ClearAll(), Times.Once);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("deleted").GetString().Should().Be("all");
        root.GetProperty("remaining").GetInt32().Should().Be(0);
    }

    [Fact]
    public void DeleteSnapshot_UnexpectedError_ReturnsGenericError()
    {
        _serviceMock.Setup(s => s.DeleteSnapshot("snap-x"))
            .Throws(new InvalidOperationException("boom"));

        var result = _tool.DeleteSnapshot("snap-x");

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("VARIABLES_FAILED");
    }
}
