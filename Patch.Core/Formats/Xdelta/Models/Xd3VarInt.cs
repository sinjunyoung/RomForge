namespace Patch.Core.Formats.Xdelta.Models;

public static class Xd3VarInt
{
    public const uint Uint32OflowMask = 0xfe000000U;
    public const ulong Uint64OflowMask = 0xfe00000000000000UL;

    public static uint ReadSize(byte[] buf, ref int pos, int max)
    {
        uint val = 0;

        do
        {
            if (pos == max)
                throw new Xd3Exception("end-of-input in read_integer");

            if ((val & Uint32OflowMask) != 0)
                throw new Xd3Exception("overflow in read_integer");

            byte next = buf[pos++];

            val = (val << 7) | (uint)(next & 0x7f);

            if ((next & 0x80) == 0)
                return val;

        } while (true);
    }

    public static ulong ReadOffset(byte[] buf, ref int pos, int max)
    {
        ulong val = 0;

        do
        {
            if (pos == max)
                throw new Xd3Exception("end-of-input in read_integer");

            if ((val & Uint64OflowMask) != 0)
                throw new Xd3Exception("overflow in read_integer");

            byte next = buf[pos++];

            val = (val << 7) | ((ulong)next & 0x7f);

            if ((next & 0x80) == 0)
                return val;

        } while (true);
    }

    public static uint SizeofUint32(uint num)
    {
        for (int x = 1; x <= 4; x++)
        {
            if (num < (1U << (7 * x)))
                return (uint)x;
        }

        return 5;
    }
}