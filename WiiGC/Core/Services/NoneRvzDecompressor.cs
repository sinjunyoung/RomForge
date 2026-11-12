using Common.Localization;

namespace WiiGC.Core.Services;

internal sealed class NoneRvzDecompressor : RvzDecompressor
{
    public override int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length < source.Length)
            throw new InvalidDataException(Loc.S.ErrDecompressedLargerThanExpected);

        source.CopyTo(destination);

        return source.Length;
    }
}