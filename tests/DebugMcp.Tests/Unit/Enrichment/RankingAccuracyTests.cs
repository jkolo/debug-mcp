using DebugMcp.Services.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment;

/// <summary>SC-007: across the FR-030 corpus, the human-identified fault frame ranks first in at least 8 of 10.</summary>
public sealed class RankingAccuracyTests
{
    private readonly ISuspicionRanker _ranker = new SuspicionRanker();

    [Fact]
    public void Rank_AcrossFaultCorpus_TopFrameMatchesHumanAnswerInAtLeast8Of10()
    {
        var correct = 0;
        var failures = new List<string>();

        foreach (var (name, scenario, expectedFrameIndex) in FaultCorpusFixtures.All)
        {
            var outcome = _ranker.Rank(scenario.Frames, scenario.Exception);

            if (expectedFrameIndex is null)
            {
                // NoSymbolsAvailable: correctly reporting unavailability doesn't count toward the
                // "ranks first" tally — there is no rank to compare (see FaultCorpusFixtures docs).
                if (outcome.Unavailable is null)
                {
                    failures.Add($"{name}: expected RankingUnavailable, got a ranking");
                }
                continue;
            }

            var topFrame = outcome.Ranking?.FirstOrDefault()?.FrameIndex;
            if (topFrame == expectedFrameIndex)
            {
                correct++;
            }
            else
            {
                failures.Add($"{name}: expected top frame {expectedFrameIndex}, got {(topFrame?.ToString() ?? "no ranking")}");
            }
        }

        correct.Should().BeGreaterThanOrEqualTo(8,
            because: $"SC-007 requires >=8/10; failures: {string.Join("; ", failures)}");
    }
}
