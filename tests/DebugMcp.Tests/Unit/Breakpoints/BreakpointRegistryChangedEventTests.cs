using DebugMcp.Models.Breakpoints;
using DebugMcp.Services.Breakpoints;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Breakpoints;

/// <summary>
/// Tests for BreakpointRegistry.Changed event (T003).
/// Verifies the registry fires a Changed event on Add, Update, Remove, Clear,
/// and on exception breakpoint Add/Update/Remove operations.
/// </summary>
public class BreakpointRegistryChangedEventTests
{
    private readonly BreakpointRegistry _registry;
    private int _changedCount;

    public BreakpointRegistryChangedEventTests()
    {
        var logger = new Mock<ILogger<BreakpointRegistry>>();
        _registry = new BreakpointRegistry(logger.Object);
        _registry.Changed += (_, _) => _changedCount++;
    }

    private static Breakpoint CreateBreakpoint(string id = "bp-1") => new(
        Id: id,
        Location: new BreakpointLocation("/src/Program.cs", 42),
        State: BreakpointState.Pending,
        Enabled: true,
        Verified: false,
        HitCount: 0);

    private static ExceptionBreakpoint CreateExceptionBreakpoint(string id = "ex-1") => new(
        Id: id,
        ExceptionType: "System.NullReferenceException",
        BreakOnFirstChance: true,
        BreakOnSecondChance: true,
        IncludeSubtypes: true,
        Enabled: true,
        Verified: true,
        HitCount: 0);

    [Fact]
    public void Add_FiresChangedEvent()
    {
        _registry.Add(CreateBreakpoint());

        _changedCount.Should().Be(1);
    }

    [Fact]
    public void Add_Duplicate_DoesNotFireChangedEvent()
    {
        var bp = CreateBreakpoint();
        _registry.Add(bp);
        _changedCount = 0;

        _registry.Add(bp); // duplicate — should not fire

        _changedCount.Should().Be(0);
    }

    [Fact]
    public void Update_FiresChangedEvent()
    {
        var bp = CreateBreakpoint();
        _registry.Add(bp);
        _changedCount = 0;

        _registry.Update(bp with { HitCount = 1 });

        _changedCount.Should().Be(1);
    }

    [Fact]
    public void Update_NotFound_DoesNotFireChangedEvent()
    {
        _registry.Update(CreateBreakpoint("bp-nonexistent"));

        _changedCount.Should().Be(0);
    }

    [Fact]
    public void Remove_FiresChangedEvent()
    {
        _registry.Add(CreateBreakpoint());
        _changedCount = 0;

        _registry.Remove("bp-1");

        _changedCount.Should().Be(1);
    }

    [Fact]
    public void Remove_NotFound_DoesNotFireChangedEvent()
    {
        _registry.Remove("bp-nonexistent");

        _changedCount.Should().Be(0);
    }

    [Fact]
    public void Clear_FiresChangedEvent()
    {
        _registry.Add(CreateBreakpoint());
        _changedCount = 0;

        _registry.Clear();

        _changedCount.Should().Be(1);
    }

    [Fact]
    public void AddException_FiresChangedEvent()
    {
        _registry.AddException(CreateExceptionBreakpoint());

        _changedCount.Should().Be(1);
    }

    [Fact]
    public void AddException_Duplicate_DoesNotFireChangedEvent()
    {
        var eb = CreateExceptionBreakpoint();
        _registry.AddException(eb);
        _changedCount = 0;

        _registry.AddException(eb);

        _changedCount.Should().Be(0);
    }

    [Fact]
    public void UpdateException_FiresChangedEvent()
    {
        _registry.AddException(CreateExceptionBreakpoint());
        _changedCount = 0;

        _registry.UpdateException(CreateExceptionBreakpoint() with { HitCount = 5 });

        _changedCount.Should().Be(1);
    }

    [Fact]
    public void RemoveException_FiresChangedEvent()
    {
        _registry.AddException(CreateExceptionBreakpoint());
        _changedCount = 0;

        _registry.RemoveException("ex-1");

        _changedCount.Should().Be(1);
    }

    [Fact]
    public void RemoveException_NotFound_DoesNotFireChangedEvent()
    {
        _registry.RemoveException("ex-nonexistent");

        _changedCount.Should().Be(0);
    }

    [Fact]
    public void MultipleOperations_FireMultipleEvents()
    {
        _registry.Add(CreateBreakpoint("bp-1"));
        _registry.Add(CreateBreakpoint("bp-2"));
        _registry.Update(CreateBreakpoint("bp-1") with { HitCount = 1 });
        _registry.Remove("bp-2");

        _changedCount.Should().Be(4);
    }
}
