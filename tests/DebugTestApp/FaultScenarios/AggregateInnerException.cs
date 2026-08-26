namespace DebugTestApp.FaultScenarios;

/// <summary>
/// FR-030 mandatory scenario: an aggregate/inner exception. Ranking must find the fault inside
/// the inner exception's own stack, not the outer <c>Task.Wait()</c> aggregation frame. Fault
/// frame: <see cref="ProcessItem"/> (the inner exception's throwing frame).
/// </summary>
public static class AggregateInnerException
{
    public static void Run()
    {
        var task = Task.Run(() => ProcessItem(null));
        task.Wait();
    }

    private static void ProcessItem(string? sku)
    {
        Console.WriteLine(sku!.Length);
    }
}
