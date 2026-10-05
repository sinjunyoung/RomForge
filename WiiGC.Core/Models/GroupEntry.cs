namespace WiiGC.Core.Models;

internal readonly record struct GroupEntry(uint DataOffset4, uint DataSizeField, uint RvzPackedSize)
{
    public bool IsCompressed => (DataSizeField & 0x80000000u) != 0;

    public int DataSize => (int)(DataSizeField & 0x7FFFFFFFu);

    public long FileOffset => (long)DataOffset4 << 2;
}