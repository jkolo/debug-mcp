using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment.Heuristics;

/// <summary>FR-027: the EmptyCollectionArgument heuristic fires on a variable with children flagged but zero of them, and only those.</summary>
public sealed class EmptyCollectionArgumentTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void Rank_EmptyCollectionFixture_FiresOnTheEmptyListArgument()
    {
        var (frames, exception) = FaultCorpusFixtures.EmptyCollectionFault;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        var frame0 = outcome.Ranking!.Single(r => r.FrameIndex == 0);
        frame0.Reasons.Should().Contain(r => r.Heuristic == SuspicionHeuristics.EmptyCollectionArgument && r.Evidence.Contains("items"));
    }

    [Fact]
    public void Rank_NullDereferenceFixture_NeverFires()
    {
        var (frames, exception) = FaultCorpusFixtures.NullDereference;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        outcome.Ranking!.SelectMany(r => r.Reasons).Should().NotContain(r => r.Heuristic == SuspicionHeuristics.EmptyCollectionArgument);
    }
}
