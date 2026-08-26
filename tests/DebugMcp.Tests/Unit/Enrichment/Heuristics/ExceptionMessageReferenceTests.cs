using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment.Heuristics;

/// <summary>FR-027: the ExceptionMessageReferencesVariable heuristic fires only on the variable the exception message actually names.</summary>
public sealed class ExceptionMessageReferenceTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void Rank_MessageNamesOrderId_FiresOnlyForThatVariableNotSessionToken()
    {
        var (frames, exception) = FaultCorpusFixtures.ExceptionMessageReference;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        var frame0Reasons = outcome.Ranking!.Single(r => r.FrameIndex == 0).Reasons;
        frame0Reasons.Should().Contain(r => r.Heuristic == SuspicionHeuristics.ExceptionMessageReferencesVariable && r.Evidence.Contains("orderId"));
        frame0Reasons.Should().NotContain(r => r.Heuristic == SuspicionHeuristics.ExceptionMessageReferencesVariable && r.Evidence.Contains("sessionToken"));
    }

    [Fact]
    public void Rank_GenericExceptionMessage_NeverFires()
    {
        var (frames, exception) = FaultCorpusFixtures.NestedCallChain;

        var outcome = _ranker.Rank(frames, exception);

        outcome.Ranking.Should().NotBeNull();
        outcome.Ranking!.SelectMany(r => r.Reasons).Should().NotContain(r => r.Heuristic == SuspicionHeuristics.ExceptionMessageReferencesVariable);
    }
}
