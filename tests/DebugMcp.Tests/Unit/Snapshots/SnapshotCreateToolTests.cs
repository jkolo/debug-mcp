using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotCreateToolTests
{
    private readonly Mock<ISnapshotService> _serviceMock;
    private readonly SnapshotCreateTool _tool;

    public SnapshotCreateToolTests()
    {
        _serviceMock = new Mock<ISnapshotService>();
        var logger = new Mock<ILogger<SnapshotCreateTool>>();
        _tool = new SnapshotCreateTool(_serviceMock.Object, logger.Object);
    }

    private static Snapshot MakeSnapshot(string id = "snap-abc", string label = "test",
        int threadId = 1, int frameIndex = 0, string fn = "Main", int depth = 0)
        => new(id, label, DateTimeOffset.UtcNow, threadId, frameIndex, fn, depth,
            new List<SnapshotVariable>
            {
                new("x", "x", "System.Int32", "42", VariableScope.Local)
            });

    [Fact]
    public async Task CreateSnapshot_ReturnsSuccessJson_WithSnapshotMetadata()
    {
        var snapshot = MakeSnapshot();
        _serviceMock.Setup(s => s.CreateSnapshot(null, null, 0, 0)).Returns(snapshot);

        var result = await _tool.CreateSnapshotAsync();

        result.Success.Should().BeTrue();
        result.Snapshot.Should().NotBeNull();
        result.Snapshot!.Id.Should().Be("snap-abc");
        result.Snapshot.Label.Should().Be("test");
        result.Snapshot.VariableCount.Should().Be(1);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task CreateSnapshot_PassesParametersThrough()
    {
        var snapshot = MakeSnapshot(label: "my-snap", threadId: 5, frameIndex: 2, depth: 3);
        _serviceMock.Setup(s => s.CreateSnapshot("my-snap", 5, 2, 3)).Returns(snapshot);

        var result = await _tool.CreateSnapshotAsync(label: "my-snap", thread_id: 5, frame_index: 2, depth: 3);

        result.Success.Should().BeTrue();
        _serviceMock.Verify(s => s.CreateSnapshot("my-snap", 5, 2, 3), Times.Once);
    }

    [Fact]
    public async Task CreateSnapshot_NotPaused_ReturnsErrorJson()
    {
        _serviceMock.Setup(s => s.CreateSnapshot(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Throws(new InvalidOperationException("Cannot create snapshot while process is not paused."));

        var result = await _tool.CreateSnapshotAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("NOT_PAUSED");
    }

    [Fact]
    public async Task CreateSnapshot_NoSession_ReturnsErrorJson()
    {
        _serviceMock.Setup(s => s.CreateSnapshot(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Throws(new InvalidOperationException("No active debug session."));

        var result = await _tool.CreateSnapshotAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("NO_SESSION");
    }

    [Fact]
    public async Task CreateSnapshot_UnexpectedError_ReturnsGenericError()
    {
        _serviceMock.Setup(s => s.CreateSnapshot(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Throws(new ArgumentException("bad arg"));

        var result = await _tool.CreateSnapshotAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("VARIABLES_FAILED");
    }
}
