using DolphinTool.Core.Rvz;

namespace DolphinTool.Core.Services.Wia.Lzma;

internal sealed class LzmaRvzDecompressor : RvzDecompressor
{
    private readonly byte[] _properties;

    public LzmaRvzDecompressor(ReadOnlySpan<byte> compressorData)
    {
        if (compressorData.Length != 5)
            throw new InvalidDataException("LZMA1 속성 데이터 크기가 올바르지 않습니다.");

        _properties = compressorData.ToArray();
    }

    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var decoder = new SevenZip.Compression.LZMA.Decoder();
        decoder.SetDecoderProperties(_properties);

        using var input = new MemoryStream(source.ToArray(), false);
        using var output = new MemoryStream(destination.Length);

        decoder.Code(input, output, source.Length, destination.Length, null);

        output.Position = 0;
        int total = 0;

        while (total < output.Length)
        {
            int read = output.Read(destination[total..(int)output.Length]);
            if (read <= 0)
                break;

            total += read;
        }

        return total;
    }
}
