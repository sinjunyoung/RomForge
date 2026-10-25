namespace PSP.PBP.AT3.IO;

public sealed class BitStreamReader(byte[] data, int start, int length)
{
    private readonly int _endBit = (start + length) * 8;

    private int _pos = start * 8;

    public bool Overrun { get; private set; }

    public int BitsLeft => _endBit - _pos;

    public uint ReadBit()
    {
        if (_pos >= _endBit)
        {
            Overrun = true;
            _pos++;

            return 0;
        }

        uint bit = (uint)((data[_pos >> 3] >> (7 - (_pos & 7))) & 1);

        _pos++;

        return bit;
    }

    public uint Read(int n)
    {
        uint value = 0;

        for (int i = 0; i < n; i++)
            value = (value << 1) | ReadBit();

        return value;
    }

    public int ReadSigned(int n) => BitStreamWriter.MakeSign((int)Read(n), n);
}