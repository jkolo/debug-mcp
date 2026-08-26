using DebugMcp.Models.Inspection;
using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment;

/// <summary>FR-026: when ranking cannot be computed, the outcome says so explicitly rather than omitting the field or failing the call.</summary>
public sealed class RankingUnavailableTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void Rank_NoSymbolsFixture_ReturnsExplicitUnavailableWithReason()
    {
        var (frames, exception) = FaultCorpusFixtures.NoSymbolsAvailable;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Unavailable.Should().NotBeNull();
        outcome.Unavailable!.Reason.Should().NotBeNullOrWhiteSpace();
        outcome.Ranking.Should().BeNull(because: "Ranking and Unavailable are mutually exclusive");
    }

    [Fact]
    public void Rank_NoSymbolsFixture_DoesNotThrow()
    {
        var (frames, exception) = FaultCorpusFixtures.NoSymbolsAvailable;

        var act = () => _ranker.Rank(frames, exception);

        act.Should().NotThrow(because: "ranking-unavailable is a normal outcome, not a failure — the call must still succeed");
    }

    [Fact]
    public void Rank_EmptyFrameList_ReturnsUnavailable()
    {
        var outcome = _ranker.Rank(Array.Empty<AutopsyFrame>(), exception: null);

        outcome.Unavailable.Should().NotBeNull();
    }
}
