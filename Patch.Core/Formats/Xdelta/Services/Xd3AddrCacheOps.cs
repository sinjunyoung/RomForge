using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public static class Xd3AddrCacheOps
{
    public static void Alloc(Xd3AddrCache cache)
    {
        if (cache.SNear > 0)
            cache.NearArray = new uint[cache.SNear];

        if (cache.SSame > 0)
            cache.SameArray = new uint[cache.SSame * 256];
    }

    public static void Init(Xd3AddrCache cache)
    {
        if (cache.SNear > 0 && cache.NearArray != null)
        {
            Array.Clear(cache.NearArray, 0, cache.NearArray.Length);

            cache.NextSlot = 0;
        }

        if (cache.SSame > 0 && cache.SameArray != null)
            Array.Clear(cache.SameArray, 0, cache.SameArray.Length);
    }

    public static void Update(Xd3AddrCache cache, uint addr)
    {
        if (cache.SNear > 0 && cache.NearArray != null)
        {
            cache.NearArray[cache.NextSlot] = addr;
            cache.NextSlot = (cache.NextSlot + 1) % cache.SNear;
        }

        if (cache.SSame > 0 && cache.SameArray != null)
            cache.SameArray[addr % (cache.SSame * 256)] = addr;
    }

    public static uint DecodeAddress(Xd3AddrCache cache, uint here, uint mode, byte[] buf, ref int pos, int max)
    {
        uint sameStart = 2 + cache.SNear;
        uint val;

        if (mode < sameStart)
        {
            val = Xd3VarInt.ReadSize(buf, ref pos, max);

            if (mode == Xd3Constants.VcdSelf) { }
            else if (mode == Xd3Constants.VcdHere)
                val = here - val;
            else
                val += cache.NearArray![mode - 2];
        }
        else
        {
            if (pos == max)
                throw new Xd3Exception("address underflow");

            uint sameMode = mode - sameStart;

            val = cache.SameArray![sameMode * 256 + buf[pos]];
            pos += 1;
        }

        Update(cache, val);

        return val;
    }
}