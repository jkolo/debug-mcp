using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services.Snapshots;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotStoreTests
{
    private readonly SnapshotStore _store;

    public SnapshotStoreTests()
    {
        var logger = new Mock<ILogger<SnapshotStore>>();
        _store = new SnapshotStore(logger.Object);
    }

    private static Snapshot CreateSnapshot(string id = "snap-test-1", string label = "test") => new(
        Id: id,
        Label: label,
        CreatedAt: DateTimeOffset.UtcNow,
        ThreadId: 123,
        FrameIndex: 0,
        FunctionName: "TestApp.Program.Main",
        Depth: 0,
        Variables: new List<SnapshotVariable>
        {
            new("x", "x", "System.Int32", "42", VariableScope.Local),
            new("name", "name", "System.String", "\"hello\"", VariableScope.Argument)
        });

    [Fact]
    public void Add_NewSnapshot_ReturnsTrue()
    {
        var snapshot = CreateSnapshot();
        _store.Add(snapshot).Should().BeTrue();
    }

    [Fact]
    public void Add_DuplicateId_ReturnsFalse()
    {
        var snapshot = CreateSnapshot();
        _store.Add(snapshot);
        _store.Add(snapshot).Should().BeFalse();
    }

    [Fact]
    public void Get_ExistingId_ReturnsSnapshot()
    {
        var snapshot = CreateSnapshot();
        _store.Add(snapshot);

        var retrieved = _store.Get(snapshot.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(snapshot.Id);
        retrieved.Label.Should().Be(snapshot.Label);
        retrieved.Variables.Should().HaveCount(2);
    }

    [Fact]
    public void Get_NonExistentId_ReturnsNull()
    {
        _store.Get("snap-nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllSnapshots()
    {
        _store.Add(CreateSnapshot("snap-1", "first"));
        _store.Add(CreateSnapshot("snap-2", "second"));
        _store.Add(CreateSnapshot("snap-3", "third"));

        var all = _store.GetAll();

        all.Should().HaveCount(3);
    }

    [Fact]
    public void GetAll_EmptyStore_ReturnsEmptyList()
    {
        _store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Remove_ExistingId_ReturnsTrueAndRemoves()
    {
        var snapshot = CreateSnapshot();
        _store.Add(snapshot);

        _store.Remove(snapshot.Id).Should().BeTrue();
        _store.Get(snapshot.Id).Should().BeNull();
    }

    [Fact]
    public void Remove_NonExistentId_ReturnsFalse()
    {
        _store.Remove("snap-nonexistent").Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllSnapshots()
    {
        _store.Add(CreateSnapshot("snap-1"));
        _store.Add(CreateSnapshot("snap-2"));

        _store.Clear();

        _store.Count.Should().Be(0);
        _store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Count_ReflectsNumberOfSnapshots()
    {
        _store.Count.Should().Be(0);

        _store.Add(CreateSnapshot("snap-1"));
        _store.Count.Should().Be(1);

        _store.Add(CreateSnapshot("snap-2"));
        _store.Count.Should().Be(2);

        _store.Remove("snap-1");
        _store.Count.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentAdds_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => _store.Add(CreateSnapshot($"snap-{i}"))))
            .ToArray();

        await Task.WhenAll(tasks);

        _store.Count.Should().Be(50);
    }
}
