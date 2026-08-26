using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment.Heuristics;

/// <summary>FR-027: the InnermostUserFrame heuristic fires exactly once per ranking, on the lowest-index non-external frame.</summary>
public sealed class InnermostUserFrameTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void Rank_DeepChainFixture_FiresOnlyOnFrameZero()
    {
        var (frames, exception) = FaultCorpusFixtures.DeepChainManyExternalFrames;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        var withReason = outcome.Ranking!.Where(r => r.Reasons.Any(reason => reason.Heuristic == SuspicionHeuristics.InnermostUserFrame)).ToList();
        withReason.Should().ContainSingle().Which.FrameIndex.Should().Be(0);
    }

    [Fact]
    public void Rank_ExternalFrameDemotionFixture_SkipsTheExternalFrameForTheBonus()
    {
        var (frames, exception) = FaultCorpusFixtures.ExternalFrameDemotion;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        outcome.Ranking!.Single(r => r.FrameIndex == 1).Reasons
            .Should().NotContain(r => r.Heuristic == SuspicionHeuristics.InnermostUserFrame,
                because: "frame 1 is external — the innermost *user* frame is frame 0");
    }
}
