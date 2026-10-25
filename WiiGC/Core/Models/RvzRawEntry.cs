namespace WiiGC.Core.Models;

internal readonly record struct RvzRawEntry(long Offset, long Size, uint GroupIndex, uint GroupCount);