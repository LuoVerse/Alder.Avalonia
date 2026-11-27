namespace Alder.Avalonia.Controls;

/// <summary>Partitions large built-in searches over a snapshot of UI-independent values.</summary>
internal static class SearchWork
{
    private static readonly int WorkerCount = Math.Min(4, Environment.ProcessorCount);
    private static readonly ParallelOptions Options = new() { MaxDegreeOfParallelism = WorkerCount, TaskScheduler = TaskScheduler.Default };

    public static bool CanPartition(int count) => count >= 4096 && WorkerCount > 1 && !OperatingSystem.IsBrowser();

    public static bool[] Match(int count, Func<int, bool> predicate)
    {
        var matches = new bool[count];
        Parallel.For(0, WorkerCount, Options, worker =>
        {
            int start = (int)((long)count * worker / WorkerCount);
            int end = (int)((long)count * (worker + 1) / WorkerCount);
            for (int index = start; index < end; index++) matches[index] = predicate(index);
        });
        return matches;
    }
}
