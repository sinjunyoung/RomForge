namespace WiiGC.Core.Interfaces;

internal interface IRvzInputSource : IDisposable
{
    long Length { get; }

    void Read(long offset, Span<byte> destination);
}