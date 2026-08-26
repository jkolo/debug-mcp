namespace DebugTestApp.FaultScenarios;

/// <summary>
/// FR-030 mandatory scenario: a null dereference. <see cref="Run"/> throws immediately in the
/// frame that holds the null local — no call chain to walk. Fault frame: <see cref="Run"/>.
/// </summary>
public static class NullDereference
{
    public static void Run()
    {
        string? name = null;
        Console.WriteLine(name!.Length);
    }
}
