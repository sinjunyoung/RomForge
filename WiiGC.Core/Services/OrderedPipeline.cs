namespace WiiGC.Core.Services;

internal sealed class OrderedPipeline<TContext, TResult, TTag> : IDisposable
{
    private readonly int _window;
    private readonly Func<TContext> _contextFactory;
    private readonly Action<TResult, TTag> _complete;
    private readonly CancellationToken _ct;
    private readonly List<TContext> _contexts = new();
    private readonly Stack<TContext> _idle = new();
    private readonly Queue<(Task<TResult> Task, TContext Context, TTag Tag)> _pending = new();

    public OrderedPipeline(int window, Func<TContext> contextFactory, Action<TResult, TTag> complete, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(window, 1);

        _window = window;
        _contextFactory = contextFactory;
        _complete = complete;
        _ct = ct;
    }

    public void Submit(Func<TContext, TResult> work) => Submit(default!, work);

    public void Submit(TTag tag, Func<TContext, TResult> work)
    {
        _ct.ThrowIfCancellationRequested();

        if (_idle.Count == 0 && _contexts.Count < _window)
        {
            var created = _contextFactory();

            _contexts.Add(created);
            _idle.Push(created);
        }

        if (_idle.Count == 0)
            CompleteOldest();

        var context = _idle.Pop();
        var ct = _ct;

        _pending.Enqueue((Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            return work(context);
        }, CancellationToken.None), context, tag));
    }

    public void Drain()
    {
        while (_pending.Count > 0)
            CompleteOldest();
    }

    private void CompleteOldest()
    {
        var entry = _pending.Dequeue();

        _complete(entry.Task.GetAwaiter().GetResult(), entry.Tag);
        _idle.Push(entry.Context);
    }

    public void Dispose()
    {
        foreach (var entry in _pending)
        {
            try
            {
                entry.Task.Wait();
            }
            catch
            {
            }
        }

        _pending.Clear();

        foreach (var context in _contexts)
        {
            if (context is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
