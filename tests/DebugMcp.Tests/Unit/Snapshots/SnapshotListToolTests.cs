using System.Text.Json;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotListToolTests
{
    private readonly Mock<ISnapshotService> _serviceMock;
    private readonly SnapshotListTool _tool;

    public SnapshotListToolTests()
    {
        _serviceMock = new Mock<ISnapshotService>();
        var logger = new Mock<ILogger<SnapshotListTool>>();
        _tool = new SnapshotListTool(_serviceMock.Object, logger.Object);
    }

    [Fact]
    public void ListSnapshots_ReturnsAllSnapshotsWithMetadata()
    {
        var snapshots = new List<Snapshot>
        {
            new("snap-1", "before", DateTimeOffset.UtcNow, 1, 0, "Main", 0,
                new List<SnapshotVariable> { new("x", "x", "System.Int32", "1", VariableScope.Local) }),
            new("snap-2", "after", DateTimeOffset.UtcNow, 1, 0, "Main", 0,
                new List<SnapshotVariable> { new("x", "x", "System.Int32", "2", VariableScope.Local), new("y", "y", "System.String", "\"hi\"", VariableScope.Local) })
        };

        _serviceMock.Setup(s => s.ListSnapshots()).Returns(snapshots);

        var result = _tool.ListSnapshots();

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("count").GetInt32().Should().Be(2);

        var arr = root.GetProperty("snapshots");
        arr.GetArrayLength().Should().Be(2);
        arr[0].GetProperty("id").GetString().Should().Be("snap-1");
        arr[0].GetProperty("variableCount").GetInt32().Should().Be(1);
        arr[1].GetProperty("id").GetString().Should().Be("snap-2");
        arr[1].GetProperty("variableCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public void ListSnapshots_Empty_ReturnsEmptyArray()
    {
        _serviceMock.Setup(s => s.ListSnapshots()).Returns(new List<Snapshot>());

        var result = _tool.ListSnapshots();

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("count").GetInt32().Should().Be(0);
        root.GetProperty("snapshots").GetArrayLength().Should().Be(0);
    }
}
