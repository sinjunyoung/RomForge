using System.IO;

namespace NSW.HacPack.Models;

public sealed class RomfsNode
{
    public required string Name { get; init; }

    public bool IsDirectory { get; init; }

    public long Size { get; init; }

    public Func<Stream>? Open { get; init; }

    public List<RomfsNode> Children { get; } = [];
}