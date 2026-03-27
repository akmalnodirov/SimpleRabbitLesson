using System.Diagnostics;

var stopwatch = Stopwatch.StartNew();

Log("Program started");

Log("Before calling async method");

await DoWorkAsync();

Log("After async method completed");

async Task DoWorkAsync()
{
    Log("  Inside method - BEFORE await");

    await Task.Delay(500);

    Log("  Inside method - AFTER await");
}


Log("Before calling sync-completing method");

await DoAlreadyCompletedWork();

Log("After sync-completing method");

async Task DoAlreadyCompletedWork()
{
    Log("  Inside sync method - BEFORE await");

    int value = await Task.FromResult(42);

    Log($"  Inside sync method - AFTER await (value={value})");
}


void Log(string label)
{
    var thread = Thread.CurrentThread;

    Console.WriteLine(
        $"[{stopwatch.ElapsedMilliseconds,6}ms] " +
        $"{label,-40} | " +
        $"Thread: {thread.ManagedThreadId,3} | " +
        $"IsPool: {thread.IsThreadPoolThread}");
}