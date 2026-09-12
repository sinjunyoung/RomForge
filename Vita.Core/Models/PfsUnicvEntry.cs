namespace Vita.Core.Models;

public sealed class PfsUnicvEntry
{
    public required uint FileSectorSize { get; init; }

    public required byte[] DbSeed { get; init; }

    public required uint NSectors { get; init; }

    public required bool HasDbSeed { get; init; }

    public required string TableMagic { get; init; }

    public required long PageNumber { get; init; }
}