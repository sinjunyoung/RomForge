namespace Vita.Core.Models;

public sealed class PkgHeader
{
    public required int ItemCount { get; init; }

    public required long EncOffset { get; init; }

    public required long EncSize { get; init; }

    public required byte[] Iv { get; init; }

    public required int KeyType { get; init; }

    public required uint ContentType { get; init; }

    public required long ItemsOffset { get; init; }

    public required long ItemsSize { get; init; }

    public required string ContentId { get; init; }

    public const int HeaderSize = 192;

    public const int HeaderExtSize = 64;

    public const int ContentIdOffset = 0x30;

    public const int ContentIdSize = 0x30;
}