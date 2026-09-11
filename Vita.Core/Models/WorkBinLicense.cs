namespace Vita.Core.Models;

public sealed class WorkBinLicense
{
    public required string ContentId { get; init; }

    public required byte[] Klicensee { get; init; }

    public const int ContentIdOffset = 0x10;

    public const int ContentIdSize = 0x30;

    public const int KlicenseeOffset = 0x50;

    public const int KlicenseeSize = 0x10;

    public const int MinSize = 0x100;
}