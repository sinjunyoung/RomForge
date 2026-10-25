namespace PSP.PBP.Services;

public static class ExternalConfigBuilder
{
    private const int SlotCount = 33;
    private const int BlockSize = SlotCount * 4;
    private const int RevisionSize = 8;
    private const int TotalSize = RevisionSize + BlockSize * 2;

    private static readonly HashSet<int> ZeroDefaultCmds = [-1, 0x10, 0x17, 0x18, 0x1D, 0x1E];

    public static byte[] Build(bool useFmvFix, uint fmvFixValue, bool useCdTimingFix)
    {
        var data = new byte[TotalSize];

        BitConverter.GetBytes(0x06070070u).CopyTo(data, 0);
        BitConverter.GetBytes(0x06060006u).CopyTo(data, 4);
        WriteBlock(data, RevisionSize, useFmvFix, fmvFixValue, useCdTimingFix);
        WriteBlock(data, RevisionSize + BlockSize, useFmvFix, fmvFixValue, useCdTimingFix);

        return data;
    }

    private static void WriteBlock(byte[] data, int blockStart, bool useFmvFix, uint fmvFixValue, bool useCdTimingFix)
    {
        for (var i = 0; i < SlotCount; i++)
        {
            var cmd = i - 1;
            var slotOffset = blockStart + i * 4;
            var defaultValue = ZeroDefaultCmds.Contains(cmd) ? 0u : 0xFFFFFFFFu;

            BitConverter.GetBytes(defaultValue).CopyTo(data, slotOffset);
        }

        if (useFmvFix)
            WriteSlot(data, blockStart, 0x00, fmvFixValue);

        if (useCdTimingFix)
            WriteSlot(data, blockStart, 0x0B, 0x00010004u);
    }

    private static void WriteSlot(byte[] data, int blockStart, int cmd, uint value)
    {
        var slotIndex = cmd + 1;
        var offset = blockStart + slotIndex * 4;

        BitConverter.GetBytes(value).CopyTo(data, offset);
    }
}