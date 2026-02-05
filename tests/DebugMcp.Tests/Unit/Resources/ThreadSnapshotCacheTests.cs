using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services.Resources;
using FluentAssertions;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for ThreadSnapshotCache (T004).
/// Verifies caching of thread snapshots with stale flag and timestamps.
/// </summary>
public class ThreadSnapshotCacheTests
{
    private readonly ThreadSnapshotCache _cache = new();

    private static IReadOnlyList<ThreadInfo> CreateThreads(int count = 2)
    {
        return Enumerable.Range(1, count)
            .Select(i => new ThreadInfo(
                Id: i,
                Name: $"Thread {i}",
                State: DebugMcp.Models.Inspection.ThreadState.Stopped,
                IsCurrent: i == 1,
                Location: new SourceLocation("/src/Program.cs", 10 * i)))
            .ToList();
    }

    [Fact]
    public void InitialState_HasNoSnapshot()
    {
        _cache.HasSnapshot.Should().BeFalse();
        _cache.Threads.Should().BeNull();
        _cache.CapturedAt.Should().BeNull();
    }

    [Fact]
    public void Update_StoresThreadsAndTimestamp()
    {
        var threads = CreateThreads();
        var before = DateTimeOffset.UtcNow;

        _cache.Update(threads);

        var after = DateTimeOffset.UtcNow;
        _cache.HasSnapshot.Should().BeTrue();
        _cache.Threads.Should().BeSameAs(threads);
        _cache.CapturedAt.Should().NotBeNull();
        _cache.CapturedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void IsStale_WhenRunning_ReturnsTrue()
    {
        _cache.Update(CreateThreads());

        _cache.IsStale(SessionState.Running).Should().BeTrue();
    }

    [Fact]
    public void IsStale_WhenPaused_ReturnsFalse()
    {
        _cache.Update(CreateThreads());

        _cache.IsStale(SessionState.Paused).Should().BeFalse();
    }

    [Fact]
    public void IsStale_WhenNoSnapshot_ReturnsFalse()
    {
        // No snapshot → nothing to be stale about
        _cache.IsStale(SessionState.Running).Should().BeFalse();
    }

    [Fact]
    public void Update_OverwritesPreviousSnapshot()
    {
        var threads1 = CreateThreads(1);
        _cache.Update(threads1);
        var firstTimestamp = _cache.CapturedAt;

        // Small delay to ensure timestamp changes
        var threads2 = CreateThreads(3);
        _cache.Update(threads2);

        _cache.Threads.Should().BeSameAs(threads2);
        _cache.CapturedAt.Should().BeOnOrAfter(firstTimestamp!.Value);
    }

    [Fact]
    public void Clear_RemovesSnapshot()
    {
        _cache.Update(CreateThreads());
        _cache.HasSnapshot.Should().BeTrue();

        _cache.Clear();

        _cache.HasSnapshot.Should().BeFalse();
        _cache.Threads.Should().BeNull();
        _cache.CapturedAt.Should().BeNull();
    }
}
