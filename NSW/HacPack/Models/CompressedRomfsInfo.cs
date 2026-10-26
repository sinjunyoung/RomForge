namespace NSW.HacPack.Models;

public sealed record CompressedRomfsInfo(long TableOffset, long TableSize, byte[] TableHeader, int EntryCount);