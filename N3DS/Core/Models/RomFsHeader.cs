using System.Buffers.Binary;

namespace N3DS.Core.Models;

public class RomFsHeader
{
    public const int Size = 0x28;

    public const int DataAlignment = 0x10;

    public uint HeaderSize { get; init; }

    public uint DirHashBucketOffset { get; init; }

    public uint DirHashBucketSize { get; init; }

    public uint DirEntryOffset { get; init; }

    public uint DirEntrySize { get; init; }

    public uint FileHashBucketOffset { get; init; }

    public uint FileHashBucketSize { get; init; }

    public uint FileEntryOffset { get; init; }

    public uint FileEntrySize { get; init; }

    public uint DataOffset { get; init; }

    public static RomFsHeader Parse(byte[] data, int offset = 0)
    {
        return new RomFsHeader
        {
            HeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x00)),
            DirHashBucketOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x04)),
            DirHashBucketSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x08)),
            DirEntryOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x0C)),
            DirEntrySize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x10)),
            FileHashBucketOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x14)),
            FileHashBucketSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x18)),
            FileEntryOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x1C)),
            FileEntrySize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x20)),
            DataOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 0x24)),
        };
    }
}