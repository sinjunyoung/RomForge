namespace WiiGC.Core.Models;

internal readonly record struct PartitionDataEntry(uint FirstSector, uint SectorCount, uint GroupIndex, uint GroupCount);