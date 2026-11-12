using Common.Localization;
using WiiGC.Core.Enums;

namespace WiiGC.Core.Services;

internal abstract class RvzDecompressor : IDisposable
{
    public abstract int Decompress(ReadOnlySpan<byte> source, Span<byte> destination);

    public virtual void Dispose() { }

    public static RvzDecompressor Create(RvzCompressionType type, ReadOnlySpan<byte> compressorData) => type switch
    {
        RvzCompressionType.None => new NoneRvzDecompressor(),
        RvzCompressionType.Zstd => new ZstdRvzDecompressor(),
        RvzCompressionType.Lzma => new LzmaRvzDecompressor(compressorData),
        RvzCompressionType.Bzip2 => throw new NotSupportedException(Loc.S.ErrBzip2RvzUnsupported),
        RvzCompressionType.Lzma2 => throw new NotSupportedException(Loc.S.ErrLzma2RvzUnsupported),
        _ => throw new NotSupportedException(Loc.S.ErrUnsupportedRvzCompression(type))
    };
}