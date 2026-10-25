namespace N3DS.Core.Models;

public class CiFinishEntry
{
    public ulong TitleId { get; init; }

    public byte[]? Seed { get; init; }

    public string TitleIdHex => TitleId.ToString("x16");

    public bool HasSeed => Seed is not null;
}