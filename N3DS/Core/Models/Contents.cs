namespace N3DS.Core.Models;

public class Contents
{
    public uint ContentId { get; set; }

    public ushort ContentIndex { get; set; }

    public ushort ContentType { get; set; }

    public long ContentSize { get; set; }

    public byte[] Sha256Hash { get; set; } = new byte[0x20];

    public bool IsEncrypted => (ContentType & 0x0001) != 0;

    public bool IsOptional => (ContentType & 0x2) != 0;

    public string ContentIdHex => ContentId.ToString("x8");

    public string? FilePath { get; init; }

    public string? SdPath { get; init; }
}