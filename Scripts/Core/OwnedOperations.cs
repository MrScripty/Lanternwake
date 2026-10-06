namespace Lanternwake.Core;

/// <summary>Retains every pending operation, including cancelled work awaiting termination.</summary>
public sealed class OwnedOperations
{
    private readonly HashSet<Task> _pending = [];
    public int Count => _pending.Count;
    public void Track(Task operation)
    {
        _pending.Add(operation);
    }
    public void ObserveCompleted(Action<Exception> report)
    {
        foreach (var task in _pending.Where(t => t.IsCompleted).ToArray())
        {
            if (task.Exception is { } error) report(error);
            _pending.Remove(task);
        }
    }
    public async Task DrainAsync()
    {
        var pending = _pending.ToArray();
        try { await Task.WhenAll(pending); }
        finally { foreach (var task in pending) _pending.Remove(task); }
    }
}
