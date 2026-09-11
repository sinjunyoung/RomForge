namespace Vita.Core.Models;

public sealed class PfsUnicvEntry
{
    public required uint FileSectorSize { get; init; }

    public required byte[] DbSeed { get; init; }

    public required uint NSectors { get; init; }
}