namespace WiiGC.Core.Services;

internal interface IIsoSink
{
    void SetLength(long length);

    void Write(long offset, ReadOnlySpan<byte> data);
}