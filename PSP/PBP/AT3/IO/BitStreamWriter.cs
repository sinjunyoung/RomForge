namespace PSP.PBP.AT3.IO;

public sealed class BitStreamWriter
{
    private byte[] _buf = new byte[64];
    private int _size;
    private int _bitsUsed;

    public long SizeInBits => _bitsUsed;

    public int BufSize => _size;

    public ReadOnlySpan<byte> Bytes => _buf.AsSpan(0, _size);

    public void Write(uint val, int n)
    {
        if (n > 23 || n < 0)
            throw new ArgumentOutOfRangeException(nameof(n));

        int bitsLeft = _size * 8 - _bitsUsed;
        int bitsReq = n - bitsLeft;
        int bytesPos = _bitsUsed / 8;
        int overlap = _bitsUsed % 8;

        if (overlap != 0 || bitsReq >= 0)
        {
            int newSize = _size + (bitsReq / 8 + (overlap != 0 ? 2 : 1));

            if (newSize > _buf.Length)
                Array.Resize(ref _buf, Math.Max(newSize, _buf.Length * 2));

            _size = newSize;
        }

        uint t = (val << (32 - n)) >> overlap;
        int count = n / 8 + (overlap != 0 ? 2 : 1);

        for (int i = 0; i < count; ++i)
            _buf[bytesPos + i] |= (byte)(t >> (24 - 8 * i));

        _bitsUsed += n;
    }

    public static int MakeSign(int val, int bits)
    {
        int shift = 32 - bits;

        return (int)((uint)val << shift) >> shift;
    }
}