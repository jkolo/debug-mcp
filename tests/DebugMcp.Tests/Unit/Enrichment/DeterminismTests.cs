using DebugMcp.Models.Inspection;
using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment;

/// <summary>FR-023, SC-008: identical debuggee state yields byte-identical normalized enrichment output across repeated runs.</summary>
public sealed class DeterminismTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void Rank_SameFixtureReplayedTenTimes_YieldsIdenticalNormalizedOutput()
    {
        var (frames, exception) = FaultCorpusFixtures.MultipleNullCandidates;

        var outcomes = Enumerable.Range(0, 10)
            .Select(_ => Normalize(_ranker.Rank(frames, exception)))
            .ToList();

        outcomes.Should().AllSatisfy(o => o.Should().Be(outcomes[0]));
    }

    /// <summary>
    /// Normalized enrichment output per data-model.md §5: FrameIndex, Score, Heuristic, Weight,
    /// Evidence, and ordering. Volatile runtime facts (addresses, thread IDs, PIDs, durations)
    /// don't exist on this model at all, so no exclusion step is needed beyond a stable string form.
    /// </summary>
    private static string Normalize(EnrichmentOutcome outcome)
    {
        if (outcome.Unavailable is not null)
        {
            return $"unavailable:{outcome.Unavailable.Reason}";
        }

        return string.Join("|", outcome.Ranking!.Select(r =>
            $"{r.FrameIndex}:{r.Score:R}:[{string.Join(",", r.Reasons.Select(reason => $"{reason.Heuristic}={reason.Weight:R}:{reason.Evidence}"))}]"));
    }
}
