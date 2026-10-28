namespace N3DS.Core.Services;

public static class RomFsFormat
{
    public const int BlockSize = 0x1000;

    public const uint UnusedEntry = 0xFFFFFFFF;

    public const int Sha256Len = 0x20;

    public const int IvfcHeaderSize = 0x5C;

    public const int IvfcHeaderAlign = 0x10;

    public const int RomFsInfoSize = 0x28;

    public const int DirEntryFixed = 0x18;

    public const int FileEntryFixed = 0x20;

    public static uint AlignUp(uint v, uint a) => (v + a - 1) & ~(a - 1);

    public static ulong AlignUp(ulong v, ulong a) => (v + a - 1) & ~(a - 1);

    public static int AlignUp(int v, int a) => (v + a - 1) & ~(a - 1);

    public static uint AlignUp(uint v, int a) => AlignUp(v, (uint)a);

    public static uint CalcPathHash(uint parentVaddr, string name)
    {
        uint hash = parentVaddr ^ 123456789;

        foreach (char c in name)
        {
            hash = (hash >> 5) | (hash << 27);
            hash ^= c;
        }

        return hash;
    }

    public static uint GetHashTableCount(uint num)
    {
        if (num < 3)
            return 3;

        uint count = num;

        if (count < 19)
        {
            if (count % 2 == 0)
                count++;

            return count;
        }

        while (count % 2 == 0 || count % 3 == 0 || count % 5 == 0 || count % 7 == 0 || count % 11 == 0 || count % 13 == 0 || count % 17 == 0)
            count++;

        return count;
    }

    public static string GetParentPath(string fullPath)
    {
        int lastSlash = fullPath.TrimEnd('/').LastIndexOf('/');
        return lastSlash <= 0 ? "/" : fullPath[..lastSlash];
    }
}