using DebugMcp.Services;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Test double for IOutputEventSource that allows raising OutputReceived events.
/// </summary>
public sealed class FakeOutputEventSource : IOutputEventSource
{
    public event Action<string, string, bool>? OutputReceived;

    public void RaiseOutput(string content, string stream, bool truncated = false)
        => OutputReceived?.Invoke(content, stream, truncated);
}
