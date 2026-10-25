namespace RomForge.ViewModels;

public sealed class OverallProgressTracker(Action<int> onChanged)
{
    private readonly object _gate = new();
    private int _total;
    private int _finished;
    private int _failed;
    private int _currentPercent;
    private int _published;
    private readonly Dictionary<object, int> _inFlight = [];
    private readonly HashSet<object> _completedKeys = [];

    public int Total
    {
        get { lock (_gate) return _total; }
    }

    public int Finished
    {
        get { lock (_gate) return _finished; }
    }

    public bool HasFailures
    {
        get { lock (_gate) return _failed > 0; }
    }

    public int CurrentIndex
    {
        get { lock (_gate) return Math.Min(_finished + 1, _total); }
    }

    public int Percent
    {
        get { lock (_gate) return _published; }
    }

    public void Begin(int total)
    {
        lock (_gate)
        {
            _total = Math.Max(total, 0);
            _finished = 0;
            _failed = 0;
            _currentPercent = 0;
            _published = 0;
            _inFlight.Clear();
            _completedKeys.Clear();
            onChanged(0);
        }
    }

    public void StartItem()
    {
        lock (_gate)
            _currentPercent = 0;
    }

    public void Report(int percent)
    {
        if (percent < 0)
            return;

        Update(() => _currentPercent = Math.Min(percent, 99));
    }

    public void CompleteItem(bool success = true) => Update(() =>
    {
        _finished = Math.Min(_finished + 1, _total);
        _currentPercent = 0;

        if (!success)
            _failed++;
    });

    public void StartItem(object key) => Update(() =>
    {
        if (!_completedKeys.Contains(key))
            _inFlight[key] = 0;
    });

    public void Report(object key, int percent)
    {
        if (percent < 0)
            return;

        Update(() =>
        {
            if (!_completedKeys.Contains(key))
                _inFlight[key] = Math.Min(percent, 99);
        });
    }

    public void CompleteItem(object key, bool success = true) => Update(() =>
    {
        if (!_completedKeys.Add(key))
            return;

        _inFlight.Remove(key);

        _finished = Math.Min(_finished + 1, _total);

        if (!success)
            _failed++;
    });

    private void Update(Action change)
    {
        lock (_gate)
        {
            change();

            int next = Compute();

            if (next <= _published)
                return;

            _published = next;

            onChanged(next);
        }
    }

    private int Compute()
    {
        if (_total == 0)
            return 0;

        double done = _finished + (_finished >= _total ? 0 : (_currentPercent + _inFlight.Values.Sum()) / 100.0);

        return (int)Math.Min(100, Math.Floor(done * 100.0 / _total));
    }
}