namespace NSW.HacPack.Models;

internal sealed record RomfsLayout(RomfsHeader Header, List<RomfsFentCtx> Files, ulong FilePartitionSize, byte[] DirHashBytes, byte[] DirTableBuf, byte[] FileHashBytes, byte[] FileTableBuf);