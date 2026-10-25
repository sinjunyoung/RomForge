namespace WiiU.Core.Models;

public sealed class WuFstVolume
{
    public const long SectorSize = 0x8000;

    public long PartitionBaseOffset;

    public uint OffsetFactor;

    public byte[] PartitionTitleKey = [];

    public List<WuFstCluster> Clusters { get; } = [];

    public List<WuFstEntry> Entries { get; } = [];
}