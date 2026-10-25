using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public struct Entry
{
    public const int Size = 8;

    public EntryHalf U;

    public EntryHalf V;

    public static Entry Read(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        f.Read(pos, buf, 0, Size);

        return new Entry
        {
            U = EntryHalf.FromRaw(BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(0))),
            V = EntryHalf.FromRaw(BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(4))),
        };
    }

    public readonly void Write(IRandomAccessFile f, int pos)
    {
        var buf = new byte[Size];

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0), U.ToRaw());
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), V.ToRaw());
        f.Write(pos, buf, 0, Size);
    }

    public readonly bool Equals(Entry other) => U.Flag == other.U.Flag && U.Index == other.U.Index && V.Flag == other.V.Flag && V.Index == other.V.Index;
}