using System.Text.RegularExpressions;
using DebugMcp.Models.Inspection;

namespace DebugMcp.Services.Inspection;

/// <inheritdoc cref="ISuspicionRanker"/>
public sealed class SuspicionRanker : ISuspicionRanker
{
    public EnrichmentOutcome Rank(IReadOnlyList<AutopsyFrame> frames, ExceptionDetail? exception)
    {
        if (frames.Count == 0)
        {
            return new EnrichmentOutcome(Unavailable: new RankingUnavailable("no frames available to rank"));
        }

        if (frames.All(f => f.IsExternal))
        {
            return new EnrichmentOutcome(Unavailable: new RankingUnavailable("no symbols available for any frame"));
        }

        var innermostUserFrameIndex = frames.Where(f => !f.IsExternal).Min(f => f.Index);

        var suspects = new List<RankedSuspect>();
        foreach (var frame in frames.OrderBy(f => f.Index))
        {
            var reasons = ReasonsFor(frame, exception, isInnermostUserFrame: !frame.IsExternal && frame.Index == innermostUserFrameIndex);
            if (reasons.Count > 0)
            {
                suspects.Add(new RankedSuspect(frame.Index, reasons.Sum(r => r.Weight), reasons));
            }
        }

        if (suspects.Count == 0)
        {
            return new EnrichmentOutcome(Unavailable: new RankingUnavailable("no evidence found in any frame"));
        }

        var ranked = suspects
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.FrameIndex)
            .ToList();

        return new EnrichmentOutcome(Ranking: ranked);
    }

    private static List<SuspicionReason> ReasonsFor(AutopsyFrame frame, ExceptionDetail? exception, bool isInnermostUserFrame)
    {
        var reasons = new List<SuspicionReason>();

        if (frame.IsExternal)
        {
            reasons.Add(new SuspicionReason(
                SuspicionHeuristics.ExternalFrameNoSymbols, SuspicionHeuristics.ExternalFrameNoSymbolsWeight,
                "frame has no symbols available", frame.Location));
        }

        if (isInnermostUserFrame)
        {
            reasons.Add(new SuspicionReason(
                SuspicionHeuristics.InnermostUserFrame, SuspicionHeuristics.InnermostUserFrameWeight,
                "innermost user-code frame", frame.Location));
        }

        foreach (var variable in AllVariables(frame))
        {
            if (variable.Value == "null")
            {
                reasons.Add(new SuspicionReason(
                    SuspicionHeuristics.NullValuedLocal, SuspicionHeuristics.NullValuedLocalWeight,
                    $"'{variable.Name}' is null", frame.Location));
            }

            if (variable.HasChildren && variable.ChildrenCount == 0)
            {
                reasons.Add(new SuspicionReason(
                    SuspicionHeuristics.EmptyCollectionArgument, SuspicionHeuristics.EmptyCollectionArgumentWeight,
                    $"'{variable.Name}' is empty (0 elements)", frame.Location));
            }

            if (exception is not null && ReferencesVariableName(exception.Message, variable.Name))
            {
                reasons.Add(new SuspicionReason(
                    SuspicionHeuristics.ExceptionMessageReferencesVariable, SuspicionHeuristics.ExceptionMessageReferencesVariableWeight,
                    $"exception message references '{variable.Name}'", frame.Location));
            }
        }

        return reasons;
    }

    private static IEnumerable<Variable> AllVariables(AutopsyFrame frame)
    {
        if (frame.Arguments is not null)
        {
            foreach (var argument in frame.Arguments)
            {
                yield return argument;
            }
        }

        if (frame.Variables?.Locals is not null)
        {
            foreach (var local in frame.Variables.Locals)
            {
                yield return local;
            }
        }
    }

    private static bool ReferencesVariableName(string message, string variableName) =>
        Regex.IsMatch(message, $@"\b{Regex.Escape(variableName)}\b");
}
