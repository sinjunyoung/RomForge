namespace WiiU.Core.Models;

public sealed class WupContentGroup
{
    public required bool Hashed { get; init; }

    public ushort FstFlags { get; init; }

    public required List<WupFileEntry> Files { get; init; }
}