namespace DebugTestApp.FaultScenarios;

/// <summary>
/// FR-030 mandatory scenario: an exception crossing an async boundary. The fault is in the
/// awaited method, after the await resumes on the logically-reconstructed frame. Fault frame:
/// <see cref="LoadOrderAsync"/>.
/// </summary>
public static class AsyncBoundary
{
    public static async Task RunAsync()
    {
        await LoadOrderAsync(null);
    }

    private static async Task LoadOrderAsync(string? orderId)
    {
        await Task.Delay(1);
        Console.WriteLine(orderId!.Length);
    }
}
