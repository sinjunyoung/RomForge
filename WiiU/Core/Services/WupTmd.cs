using System.Buffers.Binary;
using WiiU.Core.Models;

namespace WiiU.Core.Services;

internal static class WupTmd
{
    public static (string TitleIdHex, int TitleVersion, List<WupContent> Contents) Parse(byte[] tmd)
    {
        uint sigType = BinaryPrimitives.ReadUInt32BigEndian(tmd.AsSpan(0, 4));

        int bodyStart = sigType switch
        {
            0x00010000 or 0x00010003 => 0x240,
            0x00010001 or 0x00010004 => 0x140,
            0x00010002 or 0x00010005 => 0x080,
            _ => throw new InvalidDataException($"지원하지 않는 TMD 서명 타입: 0x{sigType:X8}"),
        };

        ulong titleId = BinaryPrimitives.ReadUInt64BigEndian(tmd.AsSpan(bodyStart + 0x4C, 8));
        ushort titleVersion = BinaryPrimitives.ReadUInt16BigEndian(tmd.AsSpan(bodyStart + 0x9C, 2));
        ushort numContents = BinaryPrimitives.ReadUInt16BigEndian(tmd.AsSpan(bodyStart + 0x9E, 2));
        int contentInfoTableOffset = bodyStart + 0xC4;
        int contentTableOffset = contentInfoTableOffset + 64 * 36;
        var contents = new List<WupContent>(numContents);

        for (int i = 0; i < numContents; i++)
        {
            int off = contentTableOffset + i * 48;
            uint cid = BinaryPrimitives.ReadUInt32BigEndian(tmd.AsSpan(off, 4));
            ushort index = BinaryPrimitives.ReadUInt16BigEndian(tmd.AsSpan(off + 4, 2));
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(tmd.AsSpan(off + 6, 2));
            ulong size = BinaryPrimitives.ReadUInt64BigEndian(tmd.AsSpan(off + 8, 8));
            byte[] hash = tmd.AsSpan(off + 16, 32).ToArray();

            contents.Add(new WupContent(cid, index, type, size, hash));
        }

        return (titleId.ToString("x16"), titleVersion, contents);
    }
}