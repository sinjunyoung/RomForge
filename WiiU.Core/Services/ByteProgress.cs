namespace WiiU.Core.Services;

public sealed class ByteProgress(long total, Action<long, long, string>? callback)
{
    private long _done;
    private long _lastStep = -1;
    private string _lastLabel = string.Empty;

    public long Total { get; } = total;

    public void Advance(long bytes, string label)
    {
        if (bytes > 0)
            _done = Math.Min(_done + bytes, Total);

        Report(label);
    }

    public void Complete(string label)
    {
        _done = Total;
        _lastStep = -1;

        Report(label);
    }

    private void Report(string label)
    {
        if (callback is null)
            return;

        long step = Total > 0 ? _done * 1000 / Total : 1000;

        if (step == _lastStep && label == _lastLabel)
            return;

        _lastStep = step;
        _lastLabel = label;

        callback(_done, Total, label);
    }
}