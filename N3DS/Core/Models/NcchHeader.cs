namespace N3DS.Core.Models;

public class NcchHeader
{
    public const int Size = 0x200;

    public byte[] Signature = new byte[0x100];

    public uint Magic;

    public uint ContentSize;

    public ulong PartitionId;

    public ushort MakerCode;

    public ushort Version;

    public uint FirmwareHashMask;

    public ulong ProgramId;

    public byte[] Reserved1 = new byte[0x10];

    public byte[] LogoHash = new byte[0x20];

    public byte[] ProductCode = new byte[0x10];

    public byte[] ExtendedHeaderHash = new byte[0x20];

    public uint ExtendedHeaderSize;

    public uint Reserved2;

    public byte[] Flags = new byte[8];

    public uint PlainRegionOffset;

    public uint PlainRegionSize;

    public uint LogoOffset;

    public uint LogoSize;

    public uint ExefsOffset;

    public uint ExefsSize;

    public uint ExefsHashSize;

    public uint Reserved3;

    public uint RomfsOffset;

    public uint RomfsSize;

    public uint RomfsHashSize;

    public uint Reserved4;

    public byte[] ExefsHash = new byte[0x20];

    public byte[] RomfsHash = new byte[0x20];

    public bool NoCrypto => (Flags[7] & 0x04) != 0;

    public bool FixedKey => (Flags[7] & 0x01) != 0;

    public bool SeedCrypto => (Flags[7] & 0x20) != 0;

    public byte SecondaryKeySlot => Flags[3]; 

    public string ProductCodeString => System.Text.Encoding.ASCII.GetString(ProductCode).TrimEnd('\0');


    public static NcchHeader Parse(byte[] data, int offset = 0)
    {
        var h = new NcchHeader();
        using var ms = new MemoryStream(data, offset, data.Length - offset);
        using var br = new BinaryReader(ms);

        h.Signature = br.ReadBytes(0x100);
        h.Magic = br.ReadUInt32();
        h.ContentSize = br.ReadUInt32();
        h.PartitionId = br.ReadUInt64();
        h.MakerCode = br.ReadUInt16();
        h.Version = br.ReadUInt16();
        h.FirmwareHashMask = br.ReadUInt32();
        h.ProgramId = br.ReadUInt64();
        h.Reserved1 = br.ReadBytes(0x10);
        h.LogoHash = br.ReadBytes(0x20);
        h.ProductCode = br.ReadBytes(0x10);
        h.ExtendedHeaderHash = br.ReadBytes(0x20);
        h.ExtendedHeaderSize = br.ReadUInt32();
        h.Reserved2 = br.ReadUInt32();
        h.Flags = br.ReadBytes(8);
        h.PlainRegionOffset = br.ReadUInt32();
        h.PlainRegionSize = br.ReadUInt32();
        h.LogoOffset = br.ReadUInt32();
        h.LogoSize = br.ReadUInt32();
        h.ExefsOffset = br.ReadUInt32();
        h.ExefsSize = br.ReadUInt32();
        h.ExefsHashSize = br.ReadUInt32();
        h.Reserved3 = br.ReadUInt32();
        h.RomfsOffset = br.ReadUInt32();
        h.RomfsSize = br.ReadUInt32();
        h.RomfsHashSize = br.ReadUInt32();
        h.Reserved4 = br.ReadUInt32();
        h.ExefsHash = br.ReadBytes(0x20);
        h.RomfsHash = br.ReadBytes(0x20);

        return h;
    }
}