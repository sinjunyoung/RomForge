using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct FsInfo
{
    public const int Size = 0x68;

    public uint Unknown;

    public uint BlockLen;

    public ulong DirHashOffset;

    public uint DirBuckets;

    public uint P0;

    public ulong FileHashOffset;

    public uint FileBuckets;

    public uint P1;

    public ulong FatOffset;

    public uint FatSize;

    public uint P2;

    public ulong DataOffset;

    public uint DataBlockCount;

    public uint P3;

    public OffsetOrFatFile DirTable;

    public uint MaxDir;

    public uint P4;

    public OffsetOrFatFile FileTable;

    public uint MaxFile;

    public uint P5;

    public static FsInfo Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new FsInfo
        {
            Unknown = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x00)),
            BlockLen = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x04)),
            DirHashOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x08)),
            DirBuckets = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x10)),
            P0 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x14)),
            FileHashOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x18)),
            FileBuckets = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x20)),
            P1 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x24)),
            FatOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x28)),
            FatSize = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x30)),
            P2 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x34)),
            DataOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x38)),
            DataBlockCount = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x40)),
            P3 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x44)),
            DirTable = OffsetOrFatFile.Read(f, pos + 0x48),
            MaxDir = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x50)),
            P4 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x54)),
            FileTable = OffsetOrFatFile.Read(f, pos + 0x58),
            MaxFile = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x60)),
            P5 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0x64)),
        };
    }

    public readonly void Write(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x00), Unknown);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x04), BlockLen);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x08), DirHashOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x10), DirBuckets);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x14), P0);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x18), FileHashOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x20), FileBuckets);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x24), P1);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x28), FatOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x30), FatSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x34), P2);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x38), DataOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x40), DataBlockCount);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x44), P3);
        DirTable.Write(f, pos + 0x48);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x50), MaxDir);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x54), P4);
        FileTable.Write(f, pos + 0x58);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x60), MaxFile);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x64), P5);
        f.Write(pos, buf, 0, Size);
    }
}