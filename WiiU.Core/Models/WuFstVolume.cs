namespace WiiU.Core.Models;

public sealed class WuFstVolume
{
    public long PartitionBaseOffset;
    public uint OffsetFactor;
    public const long SectorSize = 0x8000;
    public byte[] PartitionTitleKey = [];

    public List<WuFstCluster> Clusters { get; } = [];

    public List<WuFstEntry> Entries { get; } = [];
}