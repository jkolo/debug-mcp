using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment.Heuristics;

/// <summary>FR-027: the NullValuedLocal heuristic fires when a frame's argument/local has a "null" value, and only for frames that actually have one.</summary>
public sealed class NullValuedLocalTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void Rank_FrameWithNullLocal_FiresNullValuedLocal()
    {
        var (frames, exception) = FaultCorpusFixtures.NullDereference;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        var frame0 = outcome.Ranking!.Single(r => r.FrameIndex == 0);
        frame0.Reasons.Should().Contain(r => r.Heuristic == SuspicionHeuristics.NullValuedLocal && r.Evidence.Contains("name"));
    }

    [Fact]
    public void Rank_DeepChainFixture_NoFrameHasNullValuedLocalReason()
    {
        var (frames, exception) = FaultCorpusFixtures.DeepChainManyExternalFrames;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        outcome.Ranking!.SelectMany(r => r.Reasons).Should().NotContain(r => r.Heuristic == SuspicionHeuristics.NullValuedLocal);
    }
}
