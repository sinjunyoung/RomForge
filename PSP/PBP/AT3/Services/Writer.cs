namespace PSP.PBP.AT3.Services;

public sealed class Writer : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly int _frameSize;
    private readonly long _totalSamples;
    private ulong _framesWritten;
    private bool _disposed;

    public Writer(Stream stream, int channels, int frameSize, bool jointStereo, long totalSamples, bool leaveOpen = false)
    {
        if (!stream.CanSeek)
            throw new ArgumentException("Stream must be seekable", nameof(stream));

        _stream = stream;
        _leaveOpen = leaveOpen;
        _frameSize = frameSize;
        _totalSamples = totalSamples;

        var header = Header.Build(channels, frameSize, jointStereo, totalSamples);

        _stream.Write(header, 0, header.Length);
    }

    public void WriteFrame(ReadOnlySpan<byte> frame)
    {
        _stream.Write(frame);

        _framesWritten++;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_framesWritten > 0)
            Header.Finish(_stream, _framesWritten, _frameSize, _totalSamples);

        _stream.Flush();

        if (!_leaveOpen)
            _stream.Dispose();
    }
}