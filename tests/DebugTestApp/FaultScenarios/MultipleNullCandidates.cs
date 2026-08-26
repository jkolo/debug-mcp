namespace DebugTestApp.FaultScenarios;

/// <summary>
/// Two frames each hold a null local; neither is named by the exception message. Ranking must
/// fall back to the innermost user-code frame as the tiebreaker. Fault frame:
/// <see cref="Innermost"/>.
/// </summary>
public static class MultipleNullCandidates
{
    public static void Run() => Middle();

    private static void Middle()
    {
        string? cachedValue = null; // unrelated null, higher in the chain
        Innermost();
        Console.WriteLine(cachedValue?.Length);
    }

    private static void Innermost()
    {
        string? result = null;
        Console.WriteLine(result!.Length);
    }
}
