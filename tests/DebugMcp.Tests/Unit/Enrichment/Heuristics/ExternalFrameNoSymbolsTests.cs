using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment.Heuristics;

/// <summary>FR-027: the ExternalFrameNoSymbols heuristic fires (as a demotion) on frames with no symbols, and only those.</summary>
public sealed class ExternalFrameNoSymbolsTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void Rank_AggregateFixture_FiresOnTheExternalTaskInfraFrame()
    {
        var (frames, exception) = FaultCorpusFixtures.AggregateInnerException;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        var externalFrame = outcome.Ranking!.Single(r => r.FrameIndex == 1);
        externalFrame.Reasons.Should().Contain(r => r.Heuristic == SuspicionHeuristics.ExternalFrameNoSymbols);
    }

    [Fact]
    public void Rank_NullDereferenceFixture_UserFrameNeverGetsExternalFrameReason()
    {
        var (frames, exception) = FaultCorpusFixtures.NullDereference;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        outcome.Ranking!.SelectMany(r => r.Reasons).Should().NotContain(r => r.Heuristic == SuspicionHeuristics.ExternalFrameNoSymbols);
    }
}
