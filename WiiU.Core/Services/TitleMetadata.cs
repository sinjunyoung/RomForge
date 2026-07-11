// TitleMetadata.cs
//
// Parses a Wii U title.tmd file's header fields (title ID, version). Offsets confirmed
// directly against Cemu's own TMDFileHeaderWiiU struct (src/Cemu/ncrypto/ncrypto.cpp):
//   0x18C : titleId       (uint64 BE)   [not shown in the excerpt we checked, standard TMD layout]
//   0x1D8 : accessRightsMask (uint32 BE)
//   0x1DC : titleVersion  (uint16 BE)
//   0x1DE : numContent    (uint16 BE)
//
// RomForge only needs titleId + titleVersion, to name the "titleId_vVERSION" subfolder a
// .wua expects. Cemu itself notes the console ignores this file for disc-based installs — it's
// only used here for naming purposes, not for any decryption-critical logic.

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
