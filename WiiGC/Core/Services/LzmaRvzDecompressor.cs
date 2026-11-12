using Common.Localization;

namespace WiiGC.Core.Services;

internal sealed class LzmaRvzDecompressor : RvzDecompressor
{
    private readonly LzmaFastDecoder _decoder;

    public LzmaRvzDecompressor(ReadOnlySpan<byte> compressorData)
    {
        if (compressorData.Length != 5)
            throw new InvalidDataException(Loc.S.ErrLzmaPropsSize);

        _decoder = new LzmaFastDecoder(compressorData);
    }

    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination) => _decoder.Decode(source, destination);
}