namespace WiiGC.Core.Services;

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