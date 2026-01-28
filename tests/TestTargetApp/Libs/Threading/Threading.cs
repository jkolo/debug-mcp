namespace Threading;

public static class ThreadingUtil
{
    public static string GetName() => "Threading";
}

public static class ThreadSpawner
{
    public static void SpawnAndWait(int threadCount)
    {
        using var barrier = new ManualResetEventSlim(false);
        var threads = new Thread[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            threads[i] = new Thread(() =>
            {
                Thread.CurrentThread.Name = $"TestThread-{threadIndex}";
                // Wait for signal - debugger can inspect threads here
                barrier.Wait();
            });
            threads[i].Start();
        }

        // Give threads time to start and block on barrier
        Thread.Sleep(100);

        // Breakpoint here to inspect all threads
        var allAlive = threads.All(t => t.IsAlive);
        Console.WriteLine($"All {threadCount} threads alive: {allAlive}");

        // Signal threads to exit
        barrier.Set();

        // Wait for all threads to complete
        foreach (var thread in threads)
        {
            thread.Join(1000);
        }
    }
}
