using DebugMcp.Services.Breakpoints;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Test double for IBreakpointEventSource that allows raising BreakpointResolved events.
/// </summary>
public sealed class FakeBreakpointEventSource : IBreakpointEventSource
{
    public event EventHandler<ResolvedBreakpointHitEventArgs>? BreakpointResolved;

    public void RaiseBreakpointResolved(ResolvedBreakpointHitEventArgs args)
        => BreakpointResolved?.Invoke(this, args);
}
