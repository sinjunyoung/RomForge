using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public class DbFileInfo : IFileInfo
{
    public const int InfoSize = 28;

    public int InfoByteLen => InfoSize;

    public uint Next;

    public uint Padding1;

    public uint Block;

    public ulong Size;

    public ulong Padding2;

    public uint GetNext() => Next;

    public void SetNext(uint v) => Next = v;

    public void ReadFrom(byte[] buf, int offset)
    {
        Next = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(offset + 0));
        Padding1 = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(offset + 4));
        Block = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(offset + 8));
        Size = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(offset + 12));
        Padding2 = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(offset + 20));
    }

    public void WriteTo(byte[] buf, int offset)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset + 0), Next);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset + 4), Padding1);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset + 8), Block);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(offset + 12), Size);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(offset + 20), Padding2);
    }

    public IFileInfo Clone() => new DbFileInfo { Next = Next, Padding1 = Padding1, Block = Block, Size = Size, Padding2 = Padding2 };
}