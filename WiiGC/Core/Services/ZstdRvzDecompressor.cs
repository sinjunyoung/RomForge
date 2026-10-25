namespace WiiGC.Core.Services;

internal sealed class ZstdRvzDecompressor : RvzDecompressor
{
    private readonly ZstdSharp.Decompressor _decompressor = new();

    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination) => _decompressor.Unwrap(source, destination);

    public override void Dispose() => _decompressor.Dispose();
}