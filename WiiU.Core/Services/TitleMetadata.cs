using System.Buffers.Binary;

namespace WiiU.Core.Services;

public sealed class TitleMetadata
{
    private const int TitleIdOffset = 0x18C;
    private const int TitleVersionOffset = 0x1DC;

    public ulong TitleId { get; }
    public ushort TitleVersion { get; }

    private TitleMetadata(ulong titleId, ushort titleVersion)
    {
        TitleId = titleId;
        TitleVersion = titleVersion;
    }

    public static TitleMetadata Parse(ReadOnlySpan<byte> tmdData)
    {
        if (tmdData.Length < TitleVersionOffset + 2)
            throw new ArgumentException("TMD data is too short.");

        ulong titleId = BinaryPrimitives.ReadUInt64BigEndian(tmdData.Slice(TitleIdOffset, 8));
        ushort titleVersion = BinaryPrimitives.ReadUInt16BigEndian(tmdData.Slice(TitleVersionOffset, 2));

        return new TitleMetadata(titleId, titleVersion);
    }

    public string TitleIdHex => TitleId.ToString("x16");
}