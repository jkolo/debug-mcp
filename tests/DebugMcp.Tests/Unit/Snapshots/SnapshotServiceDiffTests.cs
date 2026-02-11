using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services;
using DebugMcp.Services.Snapshots;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotServiceDiffTests
{
    private readonly SnapshotStore _store;
    private readonly SnapshotService _service;

    public SnapshotServiceDiffTests()
    {
        var storeLogger = new Mock<ILogger<SnapshotStore>>();
        _store = new SnapshotStore(storeLogger.Object);

        var sessionManagerMock = new Mock<IDebugSessionManager>();
        var processDebuggerMock = new Mock<IProcessDebugger>();
        var serviceLogger = new Mock<ILogger<SnapshotService>>();

        _service = new SnapshotService(_store, sessionManagerMock.Object, processDebuggerMock.Object, serviceLogger.Object);
    }

    private static SnapshotVariable V(string name, string type, string value, string? path = null)
        => new(name, path ?? name, type, value, VariableScope.Local);

    private static Snapshot Snap(string id, IReadOnlyList<SnapshotVariable> vars,
        int threadId = 123, DateTimeOffset? createdAt = null)
        => new(id, "test", createdAt ?? DateTimeOffset.UtcNow, threadId, 0, "TestApp.Program.Main", 0, vars);

    [Fact]
    public void DiffSnapshots_ModifiedVariable_AppearsInModifiedList()
    {
        _store.Add(Snap("snap-a", [V("x", "System.Int32", "2")]));
        _store.Add(Snap("snap-b", [V("x", "System.Int32", "3")]));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.Modified.Should().ContainSingle();
        diff.Modified[0].Name.Should().Be("x");
        diff.Modified[0].OldValue.Should().Be("2");
        diff.Modified[0].NewValue.Should().Be("3");
        diff.Modified[0].ChangeType.Should().Be(DiffChangeType.Modified);
    }

    [Fact]
    public void DiffSnapshots_AddedVariable_AppearsInAddedList()
    {
        _store.Add(Snap("snap-a", [V("x", "System.Int32", "1")]));
        _store.Add(Snap("snap-b", [V("x", "System.Int32", "1"), V("result", "System.String", "\"ok\"")]));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.Added.Should().ContainSingle();
        diff.Added[0].Name.Should().Be("result");
        diff.Added[0].NewValue.Should().Be("\"ok\"");
        diff.Added[0].ChangeType.Should().Be(DiffChangeType.Added);
    }

    [Fact]
    public void DiffSnapshots_RemovedVariable_AppearsInRemovedList()
    {
        _store.Add(Snap("snap-a", [V("x", "System.Int32", "1"), V("temp", "System.String", "\"buf\"")]));
        _store.Add(Snap("snap-b", [V("x", "System.Int32", "1")]));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.Removed.Should().ContainSingle();
        diff.Removed[0].Name.Should().Be("temp");
        diff.Removed[0].OldValue.Should().Be("\"buf\"");
        diff.Removed[0].ChangeType.Should().Be(DiffChangeType.Removed);
    }

    [Fact]
    public void DiffSnapshots_IdenticalSnapshots_ProducesEmptyDiff()
    {
        _store.Add(Snap("snap-a", [V("x", "System.Int32", "42"), V("y", "System.String", "\"hello\"")]));
        _store.Add(Snap("snap-b", [V("x", "System.Int32", "42"), V("y", "System.String", "\"hello\"")]));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
        diff.Modified.Should().BeEmpty();
        diff.Unchanged.Should().Be(2);
    }

    [Fact]
    public void DiffSnapshots_ThreadMismatch_SetsFlag()
    {
        _store.Add(Snap("snap-a", [V("x", "System.Int32", "1")], threadId: 100));
        _store.Add(Snap("snap-b", [V("x", "System.Int32", "1")], threadId: 200));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.ThreadMismatch.Should().BeTrue();
    }

    [Fact]
    public void DiffSnapshots_SameThread_ClearsFlag()
    {
        _store.Add(Snap("snap-a", [V("x", "System.Int32", "1")], threadId: 123));
        _store.Add(Snap("snap-b", [V("x", "System.Int32", "1")], threadId: 123));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.ThreadMismatch.Should().BeFalse();
    }

    [Fact]
    public void DiffSnapshots_TimeDelta_ComputedCorrectly()
    {
        var t1 = DateTimeOffset.UtcNow;
        var t2 = t1.AddSeconds(5);

        _store.Add(Snap("snap-a", [V("x", "System.Int32", "1")], createdAt: t1));
        _store.Add(Snap("snap-b", [V("x", "System.Int32", "1")], createdAt: t2));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.TimeDelta.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DiffSnapshots_InvalidSnapshotId_ThrowsKeyNotFoundException()
    {
        _store.Add(Snap("snap-a", [V("x", "System.Int32", "1")]));

        var act = () => _service.DiffSnapshots("snap-a", "snap-invalid");

        act.Should().Throw<KeyNotFoundException>().WithMessage("*snap-invalid*");
    }

    [Fact]
    public void DiffSnapshots_UnchangedCount_IsCorrect()
    {
        _store.Add(Snap("snap-a", [V("a", "System.Int32", "1"), V("b", "System.Int32", "2"), V("c", "System.Int32", "3")]));
        _store.Add(Snap("snap-b", [V("a", "System.Int32", "1"), V("b", "System.Int32", "99"), V("c", "System.Int32", "3")]));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.Unchanged.Should().Be(2);
        diff.Modified.Should().ContainSingle();
    }

    [Fact]
    public void DiffSnapshots_NestedPaths_ComparedByFullPath()
    {
        var varA = new SnapshotVariable("City", "order.Customer.City", "System.String", "\"Warsaw\"", VariableScope.Field);
        var varB = new SnapshotVariable("City", "order.Customer.City", "System.String", "\"Krakow\"", VariableScope.Field);

        _store.Add(Snap("snap-a", [varA]));
        _store.Add(Snap("snap-b", [varB]));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        diff.Modified.Should().ContainSingle();
        diff.Modified[0].Path.Should().Be("order.Customer.City");
        diff.Modified[0].OldValue.Should().Be("\"Warsaw\"");
        diff.Modified[0].NewValue.Should().Be("\"Krakow\"");
    }
}
