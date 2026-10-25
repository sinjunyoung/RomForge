namespace WiiGC.Core.Services;

internal sealed class ProgressReporter(long total, Action<double>? callback)
{
    private long _done;
    private int _lastPermille = -1;

    public void Add(long bytes)
    {
        _done += bytes;

        if (callback == null || total <= 0)
            return;

        int permille = (int)(Math.Min(_done, total) * 1000 / total);

        if (permille == _lastPermille)
            return;

        _lastPermille = permille;

        callback(permille / 1000.0);
    }
}