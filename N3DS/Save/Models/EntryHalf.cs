namespace N3DS.Save.Models;

public struct EntryHalf
{
    public uint Index;

    public uint Flag;

    public static EntryHalf FromRaw(uint raw) => new() { Flag = (raw >> 31) & 1, Index = raw & 0x7FFFFFFFu };

    public readonly uint ToRaw() => (Flag << 31) | (Index & 0x7FFFFFFFu);
}