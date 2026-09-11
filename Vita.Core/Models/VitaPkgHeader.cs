namespace Vita.Core.Models;

public sealed class VitaPkgHeader
{
    public required long MetaOffset { get; init; }

    public required int MetaCount { get; init; }

    public required int ItemCount { get; init; }

    public required long TotalSize { get; init; }

    public required long EncOffset { get; init; }

    public required long EncSize { get; init; }

    public required byte[] Iv { get; init; }

    public required int KeyType { get; init; }

    public required uint ContentType { get; init; }

    public required long ItemsOffset { get; init; }

    public required long ItemsSize { get; init; }

    public const int HeaderSize = 192;

    public const int HeaderExtSize = 64;
}