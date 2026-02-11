namespace TestTargetApp;

/// <summary>
/// Async method chain for testing async stack trace features.
/// Chain: RunAsync → ProcessDataAsync → FetchItemAsync → (breakpoint here)
/// </summary>
public static class AsyncTarget
{
    public static async Task RunAsync()
    {
        var result = await ProcessDataAsync("test-item");
        Console.WriteLine($"ASYNC_RESULT:{result}");
        Console.Out.Flush();
    }

    private static async Task<string> ProcessDataAsync(string itemId)
    {
        var data = await FetchItemAsync(itemId);
        return $"Processed: {data}";
    }

    private static async Task<string> FetchItemAsync(string itemId)
    {
        // Simulate async I/O delay
        await Task.Delay(100);

        // Local variables for async variable inspection testing
        var fetchedValue = $"Item-{itemId}-fetched";
        var timestamp = DateTimeOffset.UtcNow;
        var retryCount = 0;

        // This is where a breakpoint should be set for stack trace testing
        var result = ComputeResult(fetchedValue, retryCount);
        return result;
    }

    private static string ComputeResult(string value, int retries)
    {
        // Sync method at bottom of async chain - good breakpoint target
        return $"{value} (retries: {retries})";
    }
}
