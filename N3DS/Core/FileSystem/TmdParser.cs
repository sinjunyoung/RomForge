using N3DS.Core.Enums;
using N3DS.Core.Models;
using System.Text;

namespace N3DS.Core.FileSystem;

public class TmdParser
{
    public static TmdHeader Parse(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        return Parse(br);
    }

    public static TmdHeader Parse(BinaryReader br)
    {
        uint sigType = ReadBigEndianUInt32(br);
        byte[] signature;
        int paddingSize;

        switch (sigType)
        {
            case 0x00010003:
                signature = br.ReadBytes(0x200);
                paddingSize = 0x3C;
                break;

            case 0x00010004:
                signature = br.ReadBytes(0x100);
                paddingSize = 0x3C;
                break;

            case 0x00010005:
                signature = br.ReadBytes(0x3C);
                paddingSize = 0x40;
                break;

            default:
                throw new InvalidDataException($"Unknown signature type: 0x{sigType:X8}");
        }

        br.ReadBytes(paddingSize);

        string issuer = Encoding.ASCII.GetString(br.ReadBytes(0x40)).TrimEnd('\0');
        byte version = br.ReadByte();

        br.ReadByte();
        br.ReadByte();
        br.ReadByte();

        _ = ReadBigEndianUInt64(br);
        ulong titleId = ReadBigEndianUInt64(br);
        uint titleTypeRaw = (uint)(titleId >> 32);
        TitleType titleType = (TitleType)titleTypeRaw;
        _ = ReadBigEndianUInt32(br);
        _ = ReadBigEndianUInt16(br);

        uint saveSize = br.ReadUInt32();
        uint srlSaveSize = br.ReadUInt32();

        br.ReadBytes(4);
        br.ReadByte();
        br.ReadBytes(0x31);

        _ = ReadBigEndianUInt32(br);
        ushort titleVersion = ReadBigEndianUInt16(br);
        ushort contentCount = ReadBigEndianUInt16(br);
        _ = ReadBigEndianUInt16(br);

        br.ReadBytes(2);

        _ = br.ReadBytes(0x20);

        br.ReadBytes(0x900);

        var contents = new Contents[contentCount];

        for (int i = 0; i < contentCount; i++)
        {
            uint contentId = ReadBigEndianUInt32(br);
            ushort index = ReadBigEndianUInt16(br);
            ushort type = ReadBigEndianUInt16(br);
            ulong size = ReadBigEndianUInt64(br);
            byte[] hash = br.ReadBytes(0x20);

            contents[i] = new Contents
            {
                ContentId = contentId,
                ContentIndex = index,
                ContentType = type,
                ContentSize = (long)size,
                Sha256Hash = hash
            };
        }

        return new TmdHeader
        {
            SignatureType = sigType,
            Signature = signature,
            Issuer = issuer,
            Version = version,
            TitleId = titleId,
            TitleType = titleType,
            SaveSize = saveSize,
            SrlSaveSize = srlSaveSize,            
            TitleVersion = titleVersion,
            ContentCount = contentCount,
            Contents = contents
        };
    }

    private static uint ReadBigEndianUInt32(BinaryReader br)
    {
        byte[] b = br.ReadBytes(4);

        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }

    private static ulong ReadBigEndianUInt64(BinaryReader br)
    {
        byte[] b = br.ReadBytes(8);
        ulong result = 0;

        for (int i = 0; i < 8; i++)
            result = (result << 8) | b[i];

        return result;
    }

    private static ushort ReadBigEndianUInt16(BinaryReader br)
    {
        byte[] b = br.ReadBytes(2);

        return (ushort)((b[0] << 8) | b[1]);
    }
}