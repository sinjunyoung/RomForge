namespace RomForge.Core.Models.Switch;

internal sealed record BuiltNca(string Path, string FileName, byte[] NcaId, byte[] Hash, long Size);