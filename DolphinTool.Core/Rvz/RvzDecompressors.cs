using DolphinTool.Core.Models;
using DolphinTool.Core.Services.Wia.Lzma;

namespace DolphinTool.Core.Rvz;

internal abstract class RvzDecompressor : IDisposable
{
    public abstract int Decompress(ReadOnlySpan<byte> source, Span<byte> destination);

    public virtual void Dispose()
    {
    }

    public static RvzDecompressor Create(RvzCompressionType type, ReadOnlySpan<byte> compressorData) => type switch
    {
        RvzCompressionType.None => new NoneRvzDecompressor(),
        RvzCompressionType.Zstd => new ZstdRvzDecompressor(),
        RvzCompressionType.Lzma => new LzmaRvzDecompressor(compressorData),
        RvzCompressionType.Bzip2 => throw new NotSupportedException("bzip2 압축 RVZ/WIA는 아직 지원하지 않습니다."),
        RvzCompressionType.Lzma2 => throw new NotSupportedException("LZMA2 압축 RVZ/WIA는 아직 지원하지 않습니다."),
        _ => throw new NotSupportedException($"지원하지 않는 RVZ/WIA 압축 방식입니다: {type}")
    };
}

internal sealed class NoneRvzDecompressor : RvzDecompressor
{
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length < source.Length)
            throw new InvalidDataException("압축 해제 결과가 예상보다 큽니다.");

        source.CopyTo(destination);

        return source.Length;
    }
}

internal sealed class ZstdRvzDecompressor : RvzDecompressor
{
    private readonly ZstdSharp.Decompressor _decompressor = new();

    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination) => _decompressor.Unwrap(source, destination);

    public override void Dispose() => _decompressor.Dispose();
}