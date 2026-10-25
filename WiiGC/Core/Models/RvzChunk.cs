namespace WiiGC.Core.Models;

internal readonly record struct RvzChunk(byte[] Data, int Length, List<HashException>[] ExceptionLists);