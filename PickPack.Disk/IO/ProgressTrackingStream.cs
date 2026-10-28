namespace PickPack.Disk.IO;

public class ProgressTrackingStream(Stream baseStream, long totalLength, Action<int, string, string?> progressCallback, HttpResponseMessage response) : Stream
{
    private long _bytesRead = 0;
    private DateTime _lastProgressReport = DateTime.MinValue;

    public override bool CanRead => baseStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => totalLength;

    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (read > 0)
        {
            _bytesRead += read;
            var now = DateTime.Now;

            if (now - _lastProgressReport > TimeSpan.FromMilliseconds(500))
            {
                int percent = (int)((double)_bytesRead / totalLength * 100);
                string speedInfo = CalculateSpeed();

                progressCallback(percent, "다운로드 중...", speedInfo);

                _lastProgressReport = now;
            }
        }

        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = baseStream.Read(buffer);

        if (read > 0)
        {
            _bytesRead += read;
            var now = DateTime.Now;

            if (now - _lastProgressReport > TimeSpan.FromMilliseconds(500))
            {
                int percent = (int)((double)_bytesRead / totalLength * 100);
                string speedInfo = CalculateSpeed();

                progressCallback(percent, "다운로드 중...", speedInfo);

                _lastProgressReport = now;
            }
        }

        return read;
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    private string CalculateSpeed()
    {
        double mbRead = _bytesRead / (1024.0 * 1024.0);
        double mbTotal = totalLength / (1024.0 * 1024.0);

        return $"{mbRead:F1}/{mbTotal:F1} MB";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            baseStream?.Dispose();
            response?.Dispose();
        }

        base.Dispose(disposing);
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}