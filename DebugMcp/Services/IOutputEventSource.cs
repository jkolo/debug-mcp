namespace DebugMcp.Services;

/// <summary>
/// Exposes the OutputReceived event for timeline recording without coupling to ProcessIoManager.
/// </summary>
public interface IOutputEventSource
{
    /// <summary>
    /// Fired when process output is received.
    /// Arguments: (content, streamName, truncated) where content is clipped at 1024 chars when truncated=true.
    /// </summary>
    event Action<string, string, bool>? OutputReceived;
}
