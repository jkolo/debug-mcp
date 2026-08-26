using System.Reflection;
using DebugMcp.Models.Inspection;
using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment;

/// <summary>
/// FR-025: enrichment is strictly additive — every pre-existing field survives unchanged.
/// Enforced two ways: structurally, <see cref="RankedSuspect"/> may only ever *reference* a raw
/// frame by index, never duplicate/replace its contents (this file); and at the wire level, once
/// T067/T070 wire ranking into <c>ExceptionGetContextResult</c>/<c>StacktraceGetResult</c>,
/// `LegacyTextContractTests` continues to assert every pre-existing field name/value — the same
/// mechanism that already enforces FR-021/FR-025 for the US3 migration.
/// </summary>
public sealed class AdditiveOnlyTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void RankedSuspect_OnlyReferencesFrameByIndex_NeverDuplicatesFrameContents()
    {
        var properties = typeof(RankedSuspect).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        properties.Should().BeEquivalentTo(new[] { nameof(RankedSuspect.FrameIndex), nameof(RankedSuspect.Score), nameof(RankedSuspect.Reasons) },
            because: "adding a field like Function/Module/Location here would duplicate data the raw frame already carries, violating FR-025's 'reference, don't restate' shape");
    }

    [Fact]
    public void Rank_NeverMutatesInputFrames()
    {
        var (frames, exception) = FaultCorpusFixtures.NullDereference;
        var before = frames.ToList();

        _ranker.Rank(frames, exception);

        frames.Should().BeEquivalentTo(before, options => options.WithStrictOrdering(),
            because: "the ranker must never alter the raw frames it was given — it only produces new, separate data");
    }
}
