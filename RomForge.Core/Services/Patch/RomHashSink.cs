using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;

namespace RomForge.Core.Services.Patch;

public sealed class RomHashSink : Stream
{
    private const int StageSize = 1024 * 1024;
    private const int QueueCapacity = 4;

    private readonly BlockingCollection<(byte[] Buffer, int Length)> _queue = new(QueueCapacity);
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _externalToken;
    private readonly Task _consumer;
    private readonly long _totalBytes;
    private readonly IProgress<double>? _progress;
    private readonly System.IO.Hashing.Crc32 _crc = new();
    private readonly IncrementalHash _md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
    private readonly IncrementalHash _sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);

    private byte[]? _stage;
    private int _stageLength;
    private long _written;
    private long _hashed;
    private int _lastPercent = -1;
    private Exception? _fault;
    private RomHashResult? _result;
    private bool _disposed;

    public RomHashSink(long totalBytes, IProgress<double>? progress, CancellationToken ct)
    {
        _totalBytes = totalBytes;
        _progress = progress;
        _externalToken = ct;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _consumer = Task.Run(Consume, ct);
    }

    public override bool CanRead => false;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => _written;

    public override long Position
    {
        get => _written;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin == SeekOrigin.Begin ? offset : _written + offset;

        if (target != _written)
            throw new NotSupportedException();

        return _written;
    }

    public override void SetLength(long value)
    {
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfFaulted();

        _cts.Token.ThrowIfCancellationRequested();

        while (buffer.Length > 0)
        {
            _stage ??= ArrayPool<byte>.Shared.Rent(StageSize);

            int copy = Math.Min(StageSize - _stageLength, buffer.Length);

            buffer[..copy].CopyTo(_stage.AsSpan(_stageLength));

            _stageLength += copy;
            _written += copy;
            buffer = buffer[copy..];

            if (_stageLength == StageSize)
                Enqueue();
        }
    }

    public RomHashResult Complete()
    {
        if (_result is { } cached)
            return cached;

        Enqueue();
        _queue.CompleteAdding();
        _consumer.Wait();
        ThrowIfFaulted();
        _externalToken.ThrowIfCancellationRequested();
        _progress?.Report(1.0);

        var result = new RomHashResult(_crc.GetCurrentHashAsUInt32().ToString("x8"), Convert.ToHexString(_md5.GetHashAndReset()).ToLowerInvariant(), Convert.ToHexString(_sha1.GetHashAndReset()).ToLowerInvariant(), _written);

        _result = result;

        return result;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;

            _cts.Cancel();

            try
            {
                _queue.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
            }

            _consumer.Wait();

            if (_stage is not null)
            {
                ArrayPool<byte>.Shared.Return(_stage);
                _stage = null;
            }

            _md5.Dispose();
            _sha1.Dispose();
            _queue.Dispose();
            _cts.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Enqueue()
    {
        if (_stage is null || _stageLength == 0)
            return;

        var stage = _stage;
        int length = _stageLength;

        _stage = null;
        _stageLength = 0;

        try
        {
            _queue.Add((stage, length), _cts.Token);
        }
        catch (Exception ex)
        {
            ArrayPool<byte>.Shared.Return(stage);

            if (ex is OperationCanceledException)
                ThrowIfFaulted();

            throw;
        }
    }

    private void Consume()
    {
        try
        {
            foreach (var (buffer, length) in _queue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    Parallel.Invoke(() => _crc.Append(new ReadOnlySpan<byte>(buffer, 0, length)), () => _md5.AppendData(buffer, 0, length), () => _sha1.AppendData(buffer, 0, length));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                ReportProgress(length);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _fault = ex;

            _cts.Cancel();
        }
    }

    private void ReportProgress(int length)
    {
        _hashed += length;

        if (_progress is null || _totalBytes <= 0)
            return;

        int percent = (int)Math.Min(100, _hashed * 100 / _totalBytes);

        if (percent == _lastPercent)
            return;

        _lastPercent = percent;

        _progress.Report(percent / 100.0);
    }

    private void ThrowIfFaulted()
    {
        if (_fault is not null)
            ExceptionDispatchInfo.Capture(_fault).Throw();
    }
}