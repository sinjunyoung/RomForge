using System.Security.Cryptography;
using DolphinTool.Core.Models;

namespace DolphinTool.Core.Services.Wii;

internal static class WiiHashTree
{
    public static void ComputeHashes(byte[] decrypted, byte[] hashesOut)
    {
        Array.Clear(hashesOut);

        for (int i = 0; i < WiiLayout.BlocksPerGroup; i++)
        {
            var block = decrypted.AsSpan(i * WiiLayout.BlockDataSize, WiiLayout.BlockDataSize);
            var header = hashesOut.AsSpan(i * WiiLayout.BlockHeaderSize, WiiLayout.BlockHeaderSize);

            for (int j = 0; j < WiiLayout.H0Count; j++)
                SHA1.HashData(block.Slice(j * 0x400, 0x400), header.Slice(j * WiiLayout.HashSize, WiiLayout.HashSize));
        }

        for (int sub = 0; sub < 8; sub++)
        {
            int firstBlock = sub * 8;
            var h1 = hashesOut.AsSpan(firstBlock * WiiLayout.BlockHeaderSize + WiiLayout.H1Offset, WiiLayout.H1Bytes);

            for (int k = 0; k < 8; k++)
                SHA1.HashData(hashesOut.AsSpan((firstBlock + k) * WiiLayout.BlockHeaderSize, WiiLayout.H0Bytes), h1.Slice(k * WiiLayout.HashSize, WiiLayout.HashSize));

            for (int k = 1; k < 8; k++)
                h1.CopyTo(hashesOut.AsSpan((firstBlock + k) * WiiLayout.BlockHeaderSize + WiiLayout.H1Offset, WiiLayout.H1Bytes));

            SHA1.HashData(h1, hashesOut.AsSpan(WiiLayout.H2Offset + sub * WiiLayout.HashSize, WiiLayout.HashSize));
        }

        var h2 = hashesOut.AsSpan(WiiLayout.H2Offset, WiiLayout.H2Bytes);

        for (int i = 1; i < WiiLayout.BlocksPerGroup; i++)
            h2.CopyTo(hashesOut.AsSpan(i * WiiLayout.BlockHeaderSize + WiiLayout.H2Offset, WiiLayout.H2Bytes));
    }

    public static IEnumerable<int> CompareSlots()
    {
        foreach (int slot in Slots(0, 31 * WiiLayout.HashSize))
            yield return slot;

        foreach (int slot in Slots(0x26C, 20))
            yield return slot;

        foreach (int slot in Slots(WiiLayout.H1Offset, WiiLayout.H1Bytes))
            yield return slot;

        foreach (int slot in Slots(0x320, 32))
            yield return slot;

        foreach (int slot in Slots(WiiLayout.H2Offset, WiiLayout.H2Bytes))
            yield return slot;

        foreach (int slot in Slots(0x3E0, 32))
            yield return slot;
    }

    private static IEnumerable<int> Slots(int fieldOffset, int fieldSize)
    {
        for (int l = 0; l < fieldSize; l += WiiLayout.HashSize)
            yield return fieldOffset + Math.Min(l, fieldSize - WiiLayout.HashSize);
    }
}