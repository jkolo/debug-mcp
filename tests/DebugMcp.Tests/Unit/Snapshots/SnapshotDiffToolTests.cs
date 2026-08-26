using DebugMcp.Models.Snapshots;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotDiffToolTests
{
    private readonly Mock<ISnapshotService> _serviceMock;
    private readonly SnapshotDiffTool _tool;

    public SnapshotDiffToolTests()
    {
        _serviceMock = new Mock<ISnapshotService>();
        var logger = new Mock<ILogger<SnapshotDiffTool>>();
        _tool = new SnapshotDiffTool(_serviceMock.Object, logger.Object);
    }

    [Fact]
    public async Task DiffSnapshots_ReturnsSuccessJson_WithDiffStructure()
    {
        var diff = new SnapshotDiff(
            "snap-a", "snap-b",
            Added: [new DiffEntry("y", "y", "System.Int32", null, "10", DiffChangeType.Added)],
            Removed: [new DiffEntry("z", "z", "System.String", "\"hi\"", null, DiffChangeType.Removed)],
            Modified: [new DiffEntry("x", "x", "System.Int32", "1", "2", DiffChangeType.Modified)],
            ThreadMismatch: false,
            TimeDelta: TimeSpan.FromSeconds(3),
            Unchanged: 5);

        _serviceMock.Setup(s => s.DiffSnapshots("snap-a", "snap-b")).Returns(diff);

        var result = await _tool.DiffSnapshotsAsync("snap-a", "snap-b");

        result.Success.Should().BeTrue();
        result.Diff.Should().NotBeNull();

        var d = result.Diff!;
        d.SnapshotIdA.Should().Be("snap-a");
        d.SnapshotIdB.Should().Be("snap-b");
        d.ThreadMismatch.Should().BeFalse();

        d.Summary.Added.Should().Be(1);
        d.Summary.Removed.Should().Be(1);
        d.Summary.Modified.Should().Be(1);
        d.Summary.Unchanged.Should().Be(5);
    }

    [Fact]
    public async Task DiffSnapshots_SnapshotNotFound_ReturnsErrorJson()
    {
        _serviceMock.Setup(s => s.DiffSnapshots("snap-a", "snap-missing"))
            .Throws(new KeyNotFoundException("Snapshot 'snap-missing' not found."));

        var result = await _tool.DiffSnapshotsAsync("snap-a", "snap-missing");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SNAPSHOT_NOT_FOUND");
    }

    [Fact]
    public async Task DiffSnapshots_UnexpectedError_ReturnsGenericError()
    {
        _serviceMock.Setup(s => s.DiffSnapshots("a", "b"))
            .Throws(new InvalidOperationException("boom"));

        var result = await _tool.DiffSnapshotsAsync("a", "b");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("VARIABLES_FAILED");
    }

    [Fact]
    public async Task DiffSnapshots_ModifiedEntry_HasOldAndNewValues()
    {
        var diff = new SnapshotDiff(
            "snap-a", "snap-b",
            Added: [],
            Removed: [],
            Modified: [new DiffEntry("counter", "counter", "System.Int32", "0", "42", DiffChangeType.Modified)],
            ThreadMismatch: false,
            TimeDelta: TimeSpan.Zero,
            Unchanged: 0);

        _serviceMock.Setup(s => s.DiffSnapshots("snap-a", "snap-b")).Returns(diff);

        var result = await _tool.DiffSnapshotsAsync("snap-a", "snap-b");

        var modified = result.Diff!.Modified;
        modified.Should().HaveCount(1);
        modified[0].OldValue.Should().Be("0");
        modified[0].NewValue.Should().Be("42");
    }
}
