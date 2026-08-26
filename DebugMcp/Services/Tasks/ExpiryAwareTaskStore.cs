using System.Collections.Concurrent;
using System.Text.Json;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;

namespace DebugMcp.Services.Tasks;

/// <summary>
/// Wraps <see cref="InMemoryMcpTaskStore"/> to satisfy FR-012: the raw store's
/// <c>GetTaskAsync</c> returns <c>null</c> both for a never-created id and for one whose TTL
/// has elapsed (confirmed empirically — see research.md R1), so the two are indistinguishable
/// on their own. This decorator remembers each created task's expiry instant and throws
/// <see cref="McpTaskExpiredException"/> instead of returning null once that instant has passed,
/// giving callers (and, via the SDK's tasks/get error wrapping, the client) a distinct signal.
/// </summary>
public sealed class ExpiryAwareTaskStore(InMemoryMcpTaskStore inner) : IMcpTaskStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _expiry = new(StringComparer.Ordinal);

    public event Action<InputResponseReceivedEventArgs>? InputResponseReceived
    {
        add => inner.InputResponseReceived += value;
        remove => inner.InputResponseReceived -= value;
    }

    public async Task<McpTaskInfo> CreateTaskAsync(CancellationToken cancellationToken)
    {
        var info = await inner.CreateTaskAsync(cancellationToken);
        if (info.TimeToLive is { } ttl)
        {
            _expiry[info.TaskId] = info.CreatedAt + ttl;
        }
        PruneExpired();
        return info;
    }

    public async Task<McpTaskInfo?> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var info = await inner.GetTaskAsync(taskId, cancellationToken);
        if (info is not null)
        {
            return info;
        }

        if (_expiry.TryGetValue(taskId, out var expiresAt) && expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new McpTaskExpiredException(taskId, expiresAt);
        }

        return null;
    }

    public Task SetCompletedAsync(string taskId, JsonElement result, CancellationToken cancellationToken) =>
        inner.SetCompletedAsync(taskId, result, cancellationToken);

    public Task SetFailedAsync(string taskId, JsonElement error, CancellationToken cancellationToken) =>
        inner.SetFailedAsync(taskId, error, cancellationToken);

    public Task<bool> SetCancelledAsync(string taskId, CancellationToken cancellationToken) =>
        inner.SetCancelledAsync(taskId, cancellationToken);

    public Task ResolveInputRequestsAsync(
        string taskId, IDictionary<string, InputResponse> inputResponses, CancellationToken cancellationToken) =>
        inner.ResolveInputRequestsAsync(taskId, inputResponses, cancellationToken);

    public Task SetInputRequestsAsync(
        string taskId, IDictionary<string, InputRequest> inputRequests, CancellationToken cancellationToken) =>
        inner.SetInputRequestsAsync(taskId, inputRequests, cancellationToken);

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (taskId, expiresAt) in _expiry)
        {
            if (expiresAt + TimeSpan.FromMinutes(10) < now)
            {
                _expiry.TryRemove(taskId, out _);
            }
        }
    }
}

/// <summary>Thrown by <see cref="ExpiryAwareTaskStore"/> for a task id whose TTL has elapsed, distinguishing it from an unknown id (FR-012).</summary>
public sealed class McpTaskExpiredException(string taskId, DateTimeOffset expiredAt)
    : Exception($"Task '{taskId}' expired at {expiredAt:O} and its result is no longer available.")
{
    public string TaskId { get; } = taskId;
    public DateTimeOffset ExpiredAt { get; } = expiredAt;
}
