namespace RomForge.Core.Services.Switch;

internal sealed record BuiltNca(string Path, string FileName, byte[] NcaId, byte[] Hash, long Size);