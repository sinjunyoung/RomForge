using System.Security.Cryptography;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal static class HashTree
{
    public static void ComputeHashes(byte[] decrypted, byte[] hashesOut)
    {
        Array.Clear(hashesOut);

        for (int i = 0; i < Layout.BlocksPerGroup; i++)
        {
            var block = decrypted.AsSpan(i * Layout.BlockDataSize, Layout.BlockDataSize);
            var header = hashesOut.AsSpan(i * Layout.BlockHeaderSize, Layout.BlockHeaderSize);

            for (int j = 0; j < Layout.H0Count; j++)
                SHA1.HashData(block.Slice(j * 0x400, 0x400), header.Slice(j * Layout.HashSize, Layout.HashSize));
        }

        for (int sub = 0; sub < 8; sub++)
        {
            int firstBlock = sub * 8;
            var h1 = hashesOut.AsSpan(firstBlock * Layout.BlockHeaderSize + Layout.H1Offset, Layout.H1Bytes);

            for (int k = 0; k < 8; k++)
                SHA1.HashData(hashesOut.AsSpan((firstBlock + k) * Layout.BlockHeaderSize, Layout.H0Bytes), h1.Slice(k * Layout.HashSize, Layout.HashSize));

            for (int k = 1; k < 8; k++)
                h1.CopyTo(hashesOut.AsSpan((firstBlock + k) * Layout.BlockHeaderSize + Layout.H1Offset, Layout.H1Bytes));

            SHA1.HashData(h1, hashesOut.AsSpan(Layout.H2Offset + sub * Layout.HashSize, Layout.HashSize));
        }

        var h2 = hashesOut.AsSpan(Layout.H2Offset, Layout.H2Bytes);

        for (int i = 1; i < Layout.BlocksPerGroup; i++)
            h2.CopyTo(hashesOut.AsSpan(i * Layout.BlockHeaderSize + Layout.H2Offset, Layout.H2Bytes));
    }

    public static IEnumerable<int> CompareSlots()
    {
        foreach (int slot in Slots(0, 31 * Layout.HashSize))
            yield return slot;

        foreach (int slot in Slots(0x26C, 20))
            yield return slot;

        foreach (int slot in Slots(Layout.H1Offset, Layout.H1Bytes))
            yield return slot;

        foreach (int slot in Slots(0x320, 32))
            yield return slot;

        foreach (int slot in Slots(Layout.H2Offset, Layout.H2Bytes))
            yield return slot;

        foreach (int slot in Slots(0x3E0, 32))
            yield return slot;
    }

    private static IEnumerable<int> Slots(int fieldOffset, int fieldSize)
    {
        for (int l = 0; l < fieldSize; l += Layout.HashSize)
            yield return fieldOffset + Math.Min(l, fieldSize - Layout.HashSize);
    }
}