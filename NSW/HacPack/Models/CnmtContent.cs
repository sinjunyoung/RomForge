namespace NSW.HacPack.Models;

public readonly record struct CnmtContent(byte Type, byte IdOffset, byte[] Hash, long Size);