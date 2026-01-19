// Simple test target for debugger attach/launch tests
Console.WriteLine($"TestTargetApp started. PID: {Environment.ProcessId}");
Console.WriteLine("READY");
Console.Out.Flush();

// Wait for termination signal or timeout
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Normal exit
}

Console.WriteLine("TestTargetApp exiting.");
