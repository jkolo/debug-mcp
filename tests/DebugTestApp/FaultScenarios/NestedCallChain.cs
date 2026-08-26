namespace DebugTestApp.FaultScenarios;

/// <summary>
/// FR-030 mandatory scenario: a fault inside a nested call chain. The null is introduced two
/// frames above where it is dereferenced — the fault frame is where it is dereferenced
/// (<see cref="Deepest"/>), not the outermost caller. Fault frame: <see cref="Deepest"/>.
/// </summary>
public static class NestedCallChain
{
    public static void Outer() => Middle(null);

    public static void Middle(string? customerId) => Deepest(customerId);

    public static void Deepest(string? customerId)
    {
        Console.WriteLine(customerId!.ToUpperInvariant());
    }
}
