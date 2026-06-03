using System.Collections.Concurrent;
using DebugMcp.Services.Resources;
using FluentAssertions;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for ResourceNotifier (T006).
/// Verifies subscription tracking, debounce behavior, and notification dispatch.
/// </summary>
public class ResourceNotifierTests
{
    private readonly TestResourceNotifier _notifier;
    private readonly ConcurrentBag<string> _updatedUris = new();
    private readonly SemaphoreSlim _updateSignal = new(0);
    private int _listChangedCount;

    public ResourceNotifierTests()
    {
        _notifier = new TestResourceNotifier(debounceMs: 50);
        _notifier.ResourceUpdated += uri =>
        {
            _updatedUris.Add(uri);
            _updateSignal.Release();
        };
        _notifier.ListChanged += () => _listChangedCount++;
    }

    // Waits for exactly `count` notifications via semaphore — no polling, no thread pool competition.
    private async Task WaitForUpdates(int count, int timeoutMs = 2000)
    {
        for (int i = 0; i < count; i++)
            (await _updateSignal.WaitAsync(timeoutMs)).Should().BeTrue(
                $"notification {i + 1} of {count} should fire within {timeoutMs}ms");
    }

    [Fact]
    public void Subscribe_TracksUri()
    {
        _notifier.Subscribe("debugger://session");

        _notifier.IsSubscribed("debugger://session").Should().BeTrue();
        _notifier.IsSubscribed("debugger://breakpoints").Should().BeFalse();
    }

    [Fact]
    public void Unsubscribe_RemovesUri()
    {
        _notifier.Subscribe("debugger://session");
        _notifier.Unsubscribe("debugger://session");

        _notifier.IsSubscribed("debugger://session").Should().BeFalse();
    }

    [Fact]
    public async Task NotifyResourceUpdated_SubscribedUri_FiresAfterDebounce()
    {
        _notifier.Subscribe("debugger://session");

        _notifier.NotifyResourceUpdated("debugger://session");

        // Should not fire immediately
        _updatedUris.Should().BeEmpty();

        await WaitForUpdates(1);

        _updatedUris.Should().ContainSingle().Which.Should().Be("debugger://session");
    }

    [Fact]
    public async Task NotifyResourceUpdated_UnsubscribedUri_DoesNotFire()
    {
        // Not subscribed
        _notifier.NotifyResourceUpdated("debugger://session");

        await Task.Delay(100);

        _updatedUris.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyResourceUpdated_Debounce_CoalescesRapidCalls()
    {
        _notifier.Subscribe("debugger://session");

        // Fire 5 rapid notifications without any delay between them
        // to ensure they all fall within the 50ms debounce window
        for (int i = 0; i < 5; i++)
        {
            _notifier.NotifyResourceUpdated("debugger://session");
        }

        // Wait for the single coalesced notification
        await WaitForUpdates(1);

        // Brief extra wait to verify no second notification fires
        await Task.Delay(150);

        // Should coalesce to a single notification
        _updatedUris.Should().HaveCount(1);
    }

    [Fact]
    public async Task NotifyResourceUpdated_DifferentResources_Independent()
    {
        _notifier.Subscribe("debugger://session");
        _notifier.Subscribe("debugger://breakpoints");

        _notifier.NotifyResourceUpdated("debugger://session");
        _notifier.NotifyResourceUpdated("debugger://breakpoints");

        await WaitForUpdates(2);

        _updatedUris.Should().HaveCount(2);
        _updatedUris.Should().Contain("debugger://session");
        _updatedUris.Should().Contain("debugger://breakpoints");
    }

    [Fact]
    public void NotifyListChanged_FiresImmediately()
    {
        _notifier.NotifyListChanged();

        _listChangedCount.Should().Be(1);
    }

    [Fact]
    public void NotifyListChanged_MultipleCalls_FiresMultiple()
    {
        _notifier.NotifyListChanged();
        _notifier.NotifyListChanged();

        _listChangedCount.Should().Be(2);
    }

    [Fact]
    public void Dispose_CleansUpTimers()
    {
        _notifier.Subscribe("debugger://session");
        _notifier.NotifyResourceUpdated("debugger://session");

        _notifier.Dispose();

        // Should not throw
    }
}

/// <summary>
/// Test implementation of ResourceNotifier that fires events instead of sending MCP notifications.
/// This avoids the IMcpServer dependency (extension method, can't be mocked).
/// </summary>
internal class TestResourceNotifier : ResourceNotifier
{
    public event Action<string>? ResourceUpdated;
    public event Action? ListChanged;

    public TestResourceNotifier(int debounceMs = 300) : base(debounceMs)
    {
    }

    protected override void OnResourceUpdated(string uri)
    {
        ResourceUpdated?.Invoke(uri);
    }

    protected override void OnListChanged()
    {
        ListChanged?.Invoke();
    }
}
