using N3DS.Core.Enums;

namespace N3DS.Core.Models;

public class TmdHeader
{
    public uint SignatureType { get; init; }

    public byte[] Signature { get; init; } = [];

    public string Issuer { get; init; } = string.Empty;

    public byte Version { get; init; }

    public ulong TitleId { get; init; }

    public string TitleIdHex => TitleId.ToString("x16");

    public string TitleIdHighHex => TitleIdHex[..8];

    public string TitleIdLowHex => TitleIdHex[8..];

    public uint SaveSize { get; init; }

    public uint SrlSaveSize { get; init; }

    public ushort TitleVersion { get; init; }

    public ushort ContentCount { get; init; }

    public TitleType TitleType { get; init; }

    public Contents[] Contents { get; init; } = [];

    public ulong TotalContentSize => Contents.Aggregate<Contents, ulong>(0, (s, c) => s + (ulong)c.ContentSize);
}