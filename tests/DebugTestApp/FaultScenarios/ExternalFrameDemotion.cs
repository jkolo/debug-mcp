namespace DebugTestApp.FaultScenarios;

/// <summary>
/// A user frame with weak-but-present evidence sits below a library (external, no-symbols)
/// frame in the call chain. Ranking must still prefer the user frame. Fault frame:
/// <see cref="ParseConfig"/>.
/// </summary>
public static class ExternalFrameDemotion
{
    public static void Run()
    {
        // Simulates a call into a symbol-less framework method (e.g. LINQ) which in turn
        // invokes user code that actually holds the offending null.
        Enumerable.Range(0, 1).ToList().ForEach(_ => ParseConfig(null));
    }

    public static void ParseConfig(string? raw)
    {
        Console.WriteLine(raw!.Length);
    }
}
