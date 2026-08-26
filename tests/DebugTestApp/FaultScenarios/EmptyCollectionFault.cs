namespace DebugTestApp.FaultScenarios;

/// <summary>
/// An empty collection reaches a call that requires at least one element. Fault frame:
/// <see cref="PickFirst"/>, evidenced by the empty <c>items</c> argument rather than a null.
/// </summary>
public static class EmptyCollectionFault
{
    public static void Run()
    {
        PickFirst(new List<int>());
    }

    public static void PickFirst(List<int> items)
    {
        Console.WriteLine(items.First());
    }
}
