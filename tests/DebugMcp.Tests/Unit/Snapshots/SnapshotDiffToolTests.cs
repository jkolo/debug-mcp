using System.Text.Json;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using FluentAssertions;
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
    public void DiffSnapshots_ReturnsSuccessJson_WithDiffStructure()
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

        var result = _tool.DiffSnapshots("snap-a", "snap-b");

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();

        var d = root.GetProperty("diff");
        d.GetProperty("snapshotIdA").GetString().Should().Be("snap-a");
        d.GetProperty("snapshotIdB").GetString().Should().Be("snap-b");
        d.GetProperty("threadMismatch").GetBoolean().Should().BeFalse();

        var summary = d.GetProperty("summary");
        summary.GetProperty("added").GetInt32().Should().Be(1);
        summary.GetProperty("removed").GetInt32().Should().Be(1);
        summary.GetProperty("modified").GetInt32().Should().Be(1);
        summary.GetProperty("unchanged").GetInt32().Should().Be(5);
    }

    [Fact]
    public void DiffSnapshots_SnapshotNotFound_ReturnsErrorJson()
    {
        _serviceMock.Setup(s => s.DiffSnapshots("snap-a", "snap-missing"))
            .Throws(new KeyNotFoundException("Snapshot 'snap-missing' not found."));

        var result = _tool.DiffSnapshots("snap-a", "snap-missing");

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("SNAPSHOT_NOT_FOUND");
    }

    [Fact]
    public void DiffSnapshots_UnexpectedError_ReturnsGenericError()
    {
        _serviceMock.Setup(s => s.DiffSnapshots("a", "b"))
            .Throws(new InvalidOperationException("boom"));

        var result = _tool.DiffSnapshots("a", "b");

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("VARIABLES_FAILED");
    }

    [Fact]
    public void DiffSnapshots_ModifiedEntry_HasOldAndNewValues()
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

        var result = _tool.DiffSnapshots("snap-a", "snap-b");

        using var doc = JsonDocument.Parse(result);
        var modified = doc.RootElement.GetProperty("diff").GetProperty("modified");
        modified.GetArrayLength().Should().Be(1);
        modified[0].GetProperty("oldValue").GetString().Should().Be("0");
        modified[0].GetProperty("newValue").GetString().Should().Be("42");
    }
}
