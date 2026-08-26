using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using AwesomeAssertions;
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
    public async Task DeleteSnapshot_ById_ReturnsSuccessWithRemaining()
    {
        _serviceMock.Setup(s => s.DeleteSnapshot("snap-1")).Returns(true);
        _storeMock.Setup(s => s.Count).Returns(4);

        var result = await _tool.DeleteSnapshotAsync("snap-1");

        result.Success.Should().BeTrue();
        result.Deleted.Should().Be("snap-1");
        result.Remaining.Should().Be(4);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSnapshot_NotFound_ReturnsErrorJson()
    {
        _serviceMock.Setup(s => s.DeleteSnapshot("snap-missing")).Returns(false);

        var result = await _tool.DeleteSnapshotAsync("snap-missing");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SNAPSHOT_NOT_FOUND");
    }

    [Fact]
    public async Task DeleteSnapshot_NoId_ClearsAll()
    {
        var result = await _tool.DeleteSnapshotAsync(null);

        _serviceMock.Verify(s => s.ClearAll(), Times.Once);

        result.Success.Should().BeTrue();
        result.Deleted.Should().Be("all");
        result.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task DeleteSnapshot_UnexpectedError_ReturnsGenericError()
    {
        _serviceMock.Setup(s => s.DeleteSnapshot("snap-x"))
            .Throws(new InvalidOperationException("boom"));

        var result = await _tool.DeleteSnapshotAsync("snap-x");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("VARIABLES_FAILED");
    }
}
