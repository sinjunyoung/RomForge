namespace WiiU.Core.Models;

public sealed class OffsetRecord
{
    public const int EntriesPerOffsetRecord = 16;

    public ulong BaseOffset;

    public readonly ushort[] Size = new ushort[EntriesPerOffsetRecord];
}