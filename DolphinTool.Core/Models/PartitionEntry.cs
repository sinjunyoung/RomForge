namespace DolphinTool.Core.Models;

internal sealed class PartitionEntry
{
    public required byte[] Key { get; init; }

    public required PartitionDataEntry[] DataEntries { get; init; }

    public uint FirstSector => DataEntries[0].FirstSector;

    public long TotalSectors => DataEntries[1].SectorCount != 0 ? (long)DataEntries[1].FirstSector - DataEntries[0].FirstSector + DataEntries[1].SectorCount : DataEntries[0].SectorCount;
}