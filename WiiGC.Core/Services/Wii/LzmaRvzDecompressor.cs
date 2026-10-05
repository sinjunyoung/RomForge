namespace WiiGC.Core.Services.Wii;

internal sealed class LzmaRvzDecompressor : RvzDecompressor
{
    private readonly LzmaFastDecoder _decoder;

    public LzmaRvzDecompressor(ReadOnlySpan<byte> compressorData)
    {
        if (compressorData.Length != 5)
            throw new InvalidDataException("LZMA1 속성 데이터 크기가 올바르지 않습니다.");

        _decoder = new LzmaFastDecoder(compressorData);
    }

    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination) => _decoder.Decode(source, destination);
}