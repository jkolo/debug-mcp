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
    private readonly List<string> _updatedUris = new();
    private int _listChangedCount;

    public ResourceNotifierTests()
    {
        _notifier = new TestResourceNotifier(debounceMs: 50);
        _notifier.ResourceUpdated += uri => _updatedUris.Add(uri);
        _notifier.ListChanged += () => _listChangedCount++;
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

        // Wait for debounce
        await Task.Delay(100);

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

        // Fire 5 rapid notifications
        for (int i = 0; i < 5; i++)
        {
            _notifier.NotifyResourceUpdated("debugger://session");
            await Task.Delay(10);
        }

        // Wait for debounce window to expire after last call
        await Task.Delay(100);

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

        await Task.Delay(100);

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
