using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct DbHeader
{
    public const int Size = 32;

    public byte[] Magic;

    public uint Version;

    public ulong FsInfoOffset;

    public ulong ImageSize;

    public uint ImageBlockLen;

    public uint Padding;

    public static DbHeader Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new DbHeader
        {
            Magic = buf[0..4],
            Version = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(4)),
            FsInfoOffset = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(8)),
            ImageSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(16)),
            ImageBlockLen = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(24)),
            Padding = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(28)),
        };
    }
}