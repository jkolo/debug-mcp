using System.Reflection;
using DebugMcp.Services.Breakpoints;

namespace DebugMcp.Tests.Unit.Breakpoints;

/// <summary>
/// Verifies that polling artifacts (WaitForBreakpointAsync and related fields) have been
/// removed from IBreakpointManager and BreakpointManager as part of feature 030.
/// </summary>
public class BreakpointManagerPollingRemovalTests
{
    [Fact]
    public void IBreakpointManager_DoesNotHave_WaitForBreakpointAsync()
    {
        var interfaceType = typeof(IBreakpointManager);

        var method = interfaceType.GetMethod("WaitForBreakpointAsync",
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().BeNull(
            "WaitForBreakpointAsync was a polling mechanism replaced by debugger/breakpointHit notifications (feature 030)");
    }

    [Fact]
    public void BreakpointManager_DoesNotHave_PendingHitField()
    {
        var implType = typeof(BreakpointManager);

        var field = implType.GetField("_pendingHit",
            BindingFlags.NonPublic | BindingFlags.Instance);

        field.Should().BeNull(
            "_pendingHit is polling state that was removed with WaitForBreakpointAsync in feature 030");
    }

    [Fact]
    public void BreakpointManager_DoesNotHave_HitWaiterField()
    {
        var implType = typeof(BreakpointManager);

        var field = implType.GetField("_hitWaiter",
            BindingFlags.NonPublic | BindingFlags.Instance);

        field.Should().BeNull(
            "_hitWaiter is polling state that was removed with WaitForBreakpointAsync in feature 030");
    }

    [Fact]
    public void BreakpointManager_DoesNotHave_HitLockField()
    {
        var implType = typeof(BreakpointManager);

        var field = implType.GetField("_hitLock",
            BindingFlags.NonPublic | BindingFlags.Instance);

        field.Should().BeNull(
            "_hitLock is polling state that was removed with WaitForBreakpointAsync in feature 030");
    }
}
