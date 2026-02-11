using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Models.Snapshots;
using DebugMcp.Services;
using DebugMcp.Services.Snapshots;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Snapshots;

public class SnapshotServiceDepthTests
{
    private readonly SnapshotStore _store;
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly SnapshotService _service;

    public SnapshotServiceDepthTests()
    {
        var storeLogger = new Mock<ILogger<SnapshotStore>>();
        _store = new SnapshotStore(storeLogger.Object);

        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        var serviceLogger = new Mock<ILogger<SnapshotService>>();

        _service = new SnapshotService(
            _store,
            _sessionManagerMock.Object,
            _processDebuggerMock.Object,
            serviceLogger.Object);

        SetupPausedSession();
    }

    private void SetupPausedSession()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestApp",
            ExecutablePath = "/usr/bin/testapp",
            RuntimeVersion = ".NET 10.0",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Launch,
            ActiveThreadId = 123
        });

        _sessionManagerMock.Setup(s => s.GetStackFrames(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((new List<Models.Inspection.StackFrame>(), 0));
    }

    [Fact]
    public void Depth0_CapturesOnlyTopLevel_NoChildren()
    {
        var topLevel = new List<Variable>
        {
            new("x", "System.Int32", "42", VariableScope.Local, false),
            new("order", "TestApp.Order", "{TestApp.Order}", VariableScope.Local, true, 2, "order")
        };

        _sessionManagerMock.Setup(s => s.GetVariables(null, 0, "all", null))
            .Returns(topLevel);

        var snapshot = _service.CreateSnapshot(depth: 0);

        snapshot.Variables.Should().HaveCount(2);
        snapshot.Variables[0].Name.Should().Be("x");
        snapshot.Variables[0].Children.Should().BeNull();
        snapshot.Variables[1].Name.Should().Be("order");
        snapshot.Variables[1].Children.Should().BeNull();
    }

    [Fact]
    public void Depth1_ExpandsOneLevel()
    {
        var topLevel = new List<Variable>
        {
            new("x", "System.Int32", "42", VariableScope.Local, false),
            new("order", "TestApp.Order", "{TestApp.Order}", VariableScope.Local, true, 2, "order")
        };

        var orderChildren = new List<Variable>
        {
            new("Id", "System.Int32", "1", VariableScope.Field, false, Path: "order.Id"),
            new("Customer", "TestApp.Customer", "{TestApp.Customer}", VariableScope.Field, true, 1, "order.Customer")
        };

        _sessionManagerMock.Setup(s => s.GetVariables(null, 0, "all", null))
            .Returns(topLevel);
        _sessionManagerMock.Setup(s => s.GetVariables(null, 0, "all", "order"))
            .Returns(orderChildren);

        var snapshot = _service.CreateSnapshot(depth: 1);

        snapshot.Variables.Should().HaveCount(2);

        // x has no children (not expandable)
        snapshot.Variables[0].Children.Should().BeNull();

        // order has been expanded
        var order = snapshot.Variables[1];
        order.Children.Should().NotBeNull();
        order.Children.Should().HaveCount(2);
        order.Children![0].Name.Should().Be("Id");
        order.Children![0].Path.Should().Be("order.Id");
        order.Children![0].Value.Should().Be("1");

        // Customer at depth 1 should NOT be expanded further (would need depth=2)
        order.Children![1].Name.Should().Be("Customer");
        order.Children![1].Children.Should().BeNull();
    }

    [Fact]
    public void Depth2_ExpandsTwoLevels()
    {
        var topLevel = new List<Variable>
        {
            new("order", "TestApp.Order", "{TestApp.Order}", VariableScope.Local, true, 2, "order")
        };

        var orderChildren = new List<Variable>
        {
            new("Customer", "TestApp.Customer", "{TestApp.Customer}", VariableScope.Field, true, 1, "order.Customer")
        };

        var customerChildren = new List<Variable>
        {
            new("Name", "System.String", "\"Alice\"", VariableScope.Field, false, Path: "order.Customer.Name")
        };

        _sessionManagerMock.Setup(s => s.GetVariables(null, 0, "all", null))
            .Returns(topLevel);
        _sessionManagerMock.Setup(s => s.GetVariables(null, 0, "all", "order"))
            .Returns(orderChildren);
        _sessionManagerMock.Setup(s => s.GetVariables(null, 0, "all", "order.Customer"))
            .Returns(customerChildren);

        var snapshot = _service.CreateSnapshot(depth: 2);

        var order = snapshot.Variables[0];
        order.Children.Should().HaveCount(1);

        var customer = order.Children![0];
        customer.Children.Should().HaveCount(1);
        customer.Children![0].Name.Should().Be("Name");
        customer.Children![0].Path.Should().Be("order.Customer.Name");
        customer.Children![0].Value.Should().Be("\"Alice\"");
    }

    [Fact]
    public void ExpandedPaths_AreDotSeparated()
    {
        var topLevel = new List<Variable>
        {
            new("obj", "TestApp.Obj", "{TestApp.Obj}", VariableScope.Local, true, 1, "obj")
        };

        var children = new List<Variable>
        {
            new("Field", "System.Int32", "7", VariableScope.Field, false, Path: "obj.Field")
        };

        _sessionManagerMock.Setup(s => s.GetVariables(null, 0, "all", null))
            .Returns(topLevel);
        _sessionManagerMock.Setup(s => s.GetVariables(null, 0, "all", "obj"))
            .Returns(children);

        var snapshot = _service.CreateSnapshot(depth: 1);

        snapshot.Variables[0].Children![0].Path.Should().Be("obj.Field");
    }

    [Fact]
    public void DiffWithExpandedSnapshots_ShowsNestedChanges()
    {
        // Create two snapshots with expanded variables that have different nested values
        var varsA = new List<SnapshotVariable>
        {
            new("order", "order", "TestApp.Order", "{TestApp.Order}", VariableScope.Local,
                Children: new List<SnapshotVariable>
                {
                    new("Name", "order.Name", "System.String", "\"Alice\"", VariableScope.Field)
                })
        };

        var varsB = new List<SnapshotVariable>
        {
            new("order", "order", "TestApp.Order", "{TestApp.Order}", VariableScope.Local,
                Children: new List<SnapshotVariable>
                {
                    new("Name", "order.Name", "System.String", "\"Bob\"", VariableScope.Field)
                })
        };

        _store.Add(new Snapshot("snap-a", "a", DateTimeOffset.UtcNow, 1, 0, "Main", 1, varsA));
        _store.Add(new Snapshot("snap-b", "b", DateTimeOffset.UtcNow, 1, 0, "Main", 1, varsB));

        var diff = _service.DiffSnapshots("snap-a", "snap-b");

        // The flatten-by-path algorithm should find the nested Name change
        diff.Modified.Should().ContainSingle();
        diff.Modified[0].Path.Should().Be("order.Name");
        diff.Modified[0].OldValue.Should().Be("\"Alice\"");
        diff.Modified[0].NewValue.Should().Be("\"Bob\"");
    }
}
