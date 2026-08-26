using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging;
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
    public async Task SnapshotCreate_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var snapshot = new Snapshot("snap-abc", "test", DateTimeOffset.UtcNow, 1, 0, "Main", 0,
            new List<SnapshotVariable> { new("x", "x", "System.Int32", "42", VariableScope.Local) });
        serviceMock.Setup(s => s.CreateSnapshot(null, null, 0, 0)).Returns(snapshot);
        var tool = new SnapshotCreateTool(serviceMock.Object, Mock.Of<ILogger<SnapshotCreateTool>>());

        var result = await tool.CreateSnapshotAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var snap = parsed.GetProperty("snapshot");
        snap.GetProperty("id").GetString().Should().Be("snap-abc");
        snap.GetProperty("label").GetString().Should().Be("test");
        snap.GetProperty("threadId").GetInt32().Should().Be(1);
        snap.GetProperty("frameIndex").GetInt32().Should().Be(0);
        snap.GetProperty("functionName").GetString().Should().Be("Main");
        snap.GetProperty("variableCount").GetInt32().Should().Be(1);
        snap.GetProperty("depth").GetInt32().Should().Be(0);
        snap.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task SnapshotCreate_Failure_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        serviceMock.Setup(s => s.CreateSnapshot(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Throws(new InvalidOperationException("Cannot create snapshot while process is not paused."));
        var tool = new SnapshotCreateTool(serviceMock.Object, Mock.Of<ILogger<SnapshotCreateTool>>());

        var result = await tool.CreateSnapshotAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_PAUSED");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SnapshotDiff_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var diff = new SnapshotDiff(
            "snap-a", "snap-b",
            Added: [new DiffEntry("y", "y", "System.Int32", null, "10", DiffChangeType.Added)],
            Removed: [new DiffEntry("z", "z", "System.String", "\"hi\"", null, DiffChangeType.Removed)],
            Modified: [new DiffEntry("x", "x", "System.Int32", "1", "2", DiffChangeType.Modified)],
            ThreadMismatch: false,
            TimeDelta: TimeSpan.FromSeconds(3),
            Unchanged: 5);
        serviceMock.Setup(s => s.DiffSnapshots("snap-a", "snap-b")).Returns(diff);
        var tool = new SnapshotDiffTool(serviceMock.Object, Mock.Of<ILogger<SnapshotDiffTool>>());

        var result = await tool.DiffSnapshotsAsync("snap-a", "snap-b");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var d = parsed.GetProperty("diff");
        d.GetProperty("snapshotIdA").GetString().Should().Be("snap-a");
        d.GetProperty("snapshotIdB").GetString().Should().Be("snap-b");
        d.GetProperty("threadMismatch").GetBoolean().Should().BeFalse();
        d.GetProperty("timeDelta").GetString().Should().Be(TimeSpan.FromSeconds(3).ToString());

        var summary = d.GetProperty("summary");
        summary.GetProperty("added").GetInt32().Should().Be(1);
        summary.GetProperty("removed").GetInt32().Should().Be(1);
        summary.GetProperty("modified").GetInt32().Should().Be(1);
        summary.GetProperty("unchanged").GetInt32().Should().Be(5);

        var added = d.GetProperty("added");
        added.GetArrayLength().Should().Be(1);
        added[0].GetProperty("name").GetString().Should().Be("y");
        added[0].GetProperty("value").GetString().Should().Be("10");

        var removed = d.GetProperty("removed");
        removed.GetArrayLength().Should().Be(1);
        removed[0].GetProperty("value").GetString().Should().Be("\"hi\"");

        var modified = d.GetProperty("modified");
        modified.GetArrayLength().Should().Be(1);
        modified[0].GetProperty("oldValue").GetString().Should().Be("1");
        modified[0].GetProperty("newValue").GetString().Should().Be("2");
    }

    [Fact]
    public async Task SnapshotDiff_Failure_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        serviceMock.Setup(s => s.DiffSnapshots("snap-a", "snap-missing"))
            .Throws(new KeyNotFoundException("Snapshot 'snap-missing' not found."));
        var tool = new SnapshotDiffTool(serviceMock.Object, Mock.Of<ILogger<SnapshotDiffTool>>());

        var result = await tool.DiffSnapshotsAsync("snap-a", "snap-missing");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("SNAPSHOT_NOT_FOUND");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }
}
