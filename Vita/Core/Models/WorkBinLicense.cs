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

    public const int WriteSize = 0x200;

    public static readonly byte[] FixedHeader = [0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x02, 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01];
}