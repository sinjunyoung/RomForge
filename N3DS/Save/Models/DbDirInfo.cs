using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save.Models;

public class DbDirInfo : IDirInfo
{
    public const int InfoSize = 24;

    public int InfoByteLen => InfoSize;

    public uint Next;

    public uint SubDir;

    public uint SubFile;

    public uint GetNext() => Next;

    public void SetNext(uint v) => Next = v;

    public uint GetSubDir() => SubDir;

    public void SetSubDir(uint v) => SubDir = v;

    public uint GetSubFile() => SubFile;

    public void SetSubFile(uint v) => SubFile = v;

    public void ReadFrom(byte[] buf, int offset)
    {
        Next = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(offset + 0));
        SubDir = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(offset + 4));
        SubFile = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(offset + 8));
    }

    public void WriteTo(byte[] buf, int offset)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset + 0), Next);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset + 4), SubDir);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset + 8), SubFile);
    }

    public IDirInfo Clone() => new DbDirInfo { Next = Next, SubDir = SubDir, SubFile = SubFile };

    public static DbDirInfo NewRoot() => new() { Next = 0, SubDir = 0, SubFile = 0 };
}