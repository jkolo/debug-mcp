using System.Collections.Concurrent;

namespace DebugMcp.Services.Resources;

/// <summary>
/// Manages debounced resource change notifications and subscription tracking.
/// Base class provides debounce logic; derived classes implement notification dispatch.
/// </summary>
public abstract class ResourceNotifier : IDisposable
{
    private readonly ConcurrentDictionary<string, bool> _subscriptions = new();
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new();
    private readonly int _debounceMs;
    private bool _disposed;

    protected ResourceNotifier(int debounceMs = 300)
    {
        _debounceMs = debounceMs;
    }

    /// <summary>Tracks a client subscription for the given URI.</summary>
    public void Subscribe(string uri)
    {
        _subscriptions[uri] = true;
    }

    /// <summary>Removes a client subscription for the given URI.</summary>
    public void Unsubscribe(string uri)
    {
        _subscriptions.TryRemove(uri, out _);
        // Also dispose the debounce timer for this URI
        if (_debounceTimers.TryRemove(uri, out var timer))
            timer.Dispose();
    }

    /// <summary>Checks if a URI has an active subscription.</summary>
    public bool IsSubscribed(string uri)
    {
        return _subscriptions.ContainsKey(uri);
    }

    /// <summary>
    /// Queues a debounced notification for a resource update.
    /// Only fires if the URI is subscribed.
    /// </summary>
    public void NotifyResourceUpdated(string uri)
    {
        if (_disposed || !IsSubscribed(uri))
            return;

        // Reset or create debounce timer
        var timer = _debounceTimers.AddOrUpdate(
            uri,
            _ => new Timer(OnDebounceElapsed, uri, _debounceMs, Timeout.Infinite),
            (_, existing) =>
            {
                existing.Change(_debounceMs, Timeout.Infinite);
                return existing;
            });
    }

    /// <summary>
    /// Fires an immediate list-changed notification.
    /// </summary>
    public void NotifyListChanged()
    {
        if (_disposed) return;
        OnListChanged();
    }

    private void OnDebounceElapsed(object? state)
    {
        if (_disposed) return;
        var uri = (string)state!;
        if (IsSubscribed(uri))
            OnResourceUpdated(uri);
    }

    /// <summary>Called when a debounced resource update notification should be dispatched.</summary>
    protected abstract void OnResourceUpdated(string uri);

    /// <summary>Called when a list-changed notification should be dispatched.</summary>
    protected abstract void OnListChanged();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _debounceTimers)
        {
            kvp.Value.Dispose();
        }
        _debounceTimers.Clear();
        _subscriptions.Clear();
    }
}
