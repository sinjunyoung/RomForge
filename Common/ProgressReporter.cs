using System.Diagnostics;
namespace Common;

public class ProgressReporter(string gameTitle, string gameId, long totalEstimated, IProgress<ProgressInfo>? progress)
{
    private readonly Lock _lock = new();
    private readonly Stopwatch _reportSw = Stopwatch.StartNew();
    private readonly Queue<(long ts, long written)> _window = new();
    private readonly long _startTime = Stopwatch.GetTimestamp();
    private readonly double _windowSec = 2.0;
    private long _totalWritten;

    public string Label { get; set; } = gameTitle;

    public Action<long, long> CreateAction()
    {
        return (cur, total) =>
        {
            bool force = cur >= total;

            Report(cur, total, force);
        };
    }

    public void ReportPercent(double percent)
    {
        long current = (long)(percent * totalEstimated);

        Report(current, totalEstimated, current >= totalEstimated);
    }

    public void AddProgress(long bytesRead)
    {
        lock (_lock)
        {
            _totalWritten += bytesRead;

            Report(_totalWritten, totalEstimated, force: false);
        }
    }

    public void ForceReport()
    {
        lock (_lock)
            Report(_totalWritten, totalEstimated, force: true);
    }

    private void Report(long currentWritten, long totalSize, bool force)
    {
        if (!force && _reportSw.ElapsedMilliseconds < 100)
            return;

        long now = Stopwatch.GetTimestamp();

        _window.Enqueue((now, currentWritten));

        double freq = Stopwatch.Frequency;

        while (_window.Count > 1 && (now - _window.Peek().ts) / freq > _windowSec)
            _window.Dequeue();

        double mibPerSec = 0, etaSec = 0;

        if (_window.Count >= 2)
        {
            var (ts, written) = _window.Peek();
            double secSpan = (now - ts) / freq;
            long bytesSpan = currentWritten - written;
            double avgSpeed = currentWritten / ((now - _startTime) / freq);
            double windowSpeed = secSpan > 0 ? bytesSpan / secSpan : 0;
            double progressRatio = totalSize > 0 ? (double)currentWritten / totalSize : 0;
            double blendedSpeed = avgSpeed * (1 - progressRatio) + windowSpeed * progressRatio;

            mibPerSec = blendedSpeed / (1024.0 * 1024.0);
            etaSec = blendedSpeed > 0 ? (totalSize - currentWritten) / blendedSpeed : 0;
        }

        double elapsedSec = (now - _startTime) / freq;
        var elapsed = TimeSpan.FromSeconds(elapsedSec);
        var totalEta = TimeSpan.FromSeconds(elapsedSec + Math.Max(0, etaSec));
        int pct = totalSize > 0 ? (int)(currentWritten * 100 / totalSize) : 0;

        if (pct > 100) 
            pct = 100;

        var r = Utils.CalculateProgress(currentWritten, totalSize, Label);

        progress?.Report(new ProgressInfo(pct, r.label, gameId, $"{mibPerSec:F1} MiB/s", $"{elapsed:mm\\:ss} / {totalEta:mm\\:ss}"));
        _reportSw.Restart();
    }
}