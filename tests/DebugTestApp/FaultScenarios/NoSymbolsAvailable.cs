namespace DebugTestApp.FaultScenarios;

/// <summary>
/// FR-030 mandatory scenario: symbols deliberately unavailable. Represents a fault occurring
/// inside code for which no PDB is loaded (e.g. a pre-built third-party assembly) — every frame
/// is external, with no source location. Ranking is expected to yield <c>RankingUnavailable</c>,
/// not a best-effort guess. There is no single "fault frame" here — that is the point.
/// </summary>
public static class NoSymbolsAvailable
{
    public static void Run()
    {
        throw new InvalidOperationException("Simulated fault in a symbol-less module.");
    }
}
