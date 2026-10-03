using PickPack.Disk.ETC;
using System.Diagnostics;
using System.Text;

namespace PickPack.Disk
{
    public class ProgressReporter
    {
        #region Field

        private readonly object _progressLock = new();
        private Stopwatch? _stopwatch;
        private readonly List<Tuple<double, long>> _progressHistory = [];
        private CancellationToken _ct;
        private DateTime _lastProgressReport = DateTime.MinValue;

        #endregion

        #region Const

        private const int HISTORY_WINDOW_SECONDS = 10;

        #endregion

        #region Event

        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        protected virtual void OnProgressChanged(int percent, string message1, string? message2 = "")
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs
            {
                Percent = percent,
                Message1 = message1,
                Message2 = message2
            });
        }

        #endregion

        #region Public

        public void Initialize(CancellationToken ct)
        {
            _ct = ct;
            _stopwatch = Stopwatch.StartNew();
            _progressHistory.Clear();
        }

        public void ReportProgress(long transferred, long total, string operationMessage)
        {
            lock (_progressLock)
            {
                var now = DateTime.Now;

                if ((now - _lastProgressReport).TotalMilliseconds < 100)
                    return;

                _lastProgressReport = now;
                _ct.ThrowIfCancellationRequested();

                if (_stopwatch == null)
                    throw new InvalidOperationException("ProgressReporter가 초기화되지 않았습니다.");

                double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;

                _progressHistory.Add(Tuple.Create(elapsedSeconds, transferred));

                CleanupProgressHistory(elapsedSeconds);

                double speedMBps = CalculateSpeed();
                double remainingBytes = total - transferred;
                double estimatedRemainingSeconds = speedMBps > 0 ? remainingBytes / (speedMBps * 1024.0 * 1024.0) : 0;
                double percent = ((double)transferred * 100 / total);

                OnProgressChanged((int)Math.Min(percent, 100), $"{operationMessage} ({percent:F1}%)", Format(transferred, total, elapsedSeconds, elapsedSeconds + estimatedRemainingSeconds, speedMBps));
            }
        }

        public void ReportProgressWithInterval(long transferred, long total, string operationMessage, double intervalSeconds)
        {
            lock (_progressLock)
            {
                if (_stopwatch == null)
                    throw new InvalidOperationException("ProgressReporter가 초기화되지 않았습니다.");

                _ct.ThrowIfCancellationRequested();

                double now = _stopwatch.Elapsed.TotalSeconds;

                if (now - (_lastProgressReport != DateTime.MinValue ? (now - (DateTime.Now - _lastProgressReport).TotalSeconds) : 0) < intervalSeconds && transferred < total)
                    return;

                _lastProgressReport = DateTime.Now;

                _progressHistory.Add(Tuple.Create(now, transferred));
                CleanupProgressHistory(now);

                double speedMBps = CalculateSpeed();
                double remainingBytes = total - transferred;
                double estimatedRemainingSeconds = speedMBps > 0 ? remainingBytes / (speedMBps * 1024.0 * 1024.0) : 0;
                double percent = ((double)transferred * 100 / total);

                if (remainingBytes <= 0) 
                    estimatedRemainingSeconds = 0;

                OnProgressChanged((int)Math.Min(percent, 100), $"{operationMessage} ({percent:F1}%)", Format(transferred, total, now, now + estimatedRemainingSeconds, speedMBps));
            }
        }

        public void ReportCompletion(string completionMessage)
        {
            OnProgressChanged(100, completionMessage, null);
        }

        #endregion

        #region Private

        private double CalculateSpeed()
        {
            if (_progressHistory.Count <= 1)
                return 0;

            var first = _progressHistory.First();
            var last = _progressHistory.Last();
            double timeDiff = last.Item1 - first.Item1;
            long bytesDiff = last.Item2 - first.Item2;

            return timeDiff > 0.001 ? (bytesDiff / (1024.0 * 1024.0)) / timeDiff : 0;
        }

        private void CleanupProgressHistory(double currentTime)
        {
            double windowStart = currentTime - HISTORY_WINDOW_SECONDS;

            int removeCount = 0;

            for (int i = 0; i < _progressHistory.Count; i++)
            {
                if (_progressHistory[i].Item1 >= windowStart)
                    break;

                removeCount++;
            }

            if (removeCount > 0)
                _progressHistory.RemoveRange(0, removeCount);
        }

        public static string Format(long bytesTransferred, long totalBytesToTransfer, double elapsedSeconds, double estimatedSeconds, double speedMBps)
        {
            StringBuilder sb = new();

            sb.AppendFormat(@"{0} / {1} | {2:hh\:mm\:ss} / {3:hh\:mm\:ss} | {4:F1}MB/s", FileSize.FormatSize(bytesTransferred), FileSize.FormatSize(totalBytesToTransfer), TimeSpan.FromSeconds(elapsedSeconds), TimeSpan.FromSeconds(estimatedSeconds), speedMBps);

            return sb.ToString();
        }

        #endregion
    }
}