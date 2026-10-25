namespace WiiGC.Core.Models;

internal readonly record struct RvzRegion(long Start, long End, int RawIndex, int PartitionIndex, int DataIndex);