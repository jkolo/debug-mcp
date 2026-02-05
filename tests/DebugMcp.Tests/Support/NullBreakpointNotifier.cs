using DebugMcp.Models.Breakpoints;
using DebugMcp.Services.Breakpoints;

namespace DebugMcp.Tests.Support;

/// <summary>
/// A no-op implementation of IBreakpointNotifier for testing.
/// </summary>
public sealed class NullBreakpointNotifier : IBreakpointNotifier
{
    public static NullBreakpointNotifier Instance { get; } = new();

    public Task SendBreakpointHitAsync(BreakpointNotification notification)
    {
        return Task.CompletedTask;
    }
}
