using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save;

public class DbFileKey : IParentedKey
{
    public const int KeySize = 12;

    public int KeyByteLen => KeySize;

    public uint Parent;

    public ulong Name;

    public uint GetParent() => Parent;

    public void ReadFrom(byte[] buf, int offset)
    {
        Parent = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(offset));
        Name = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(offset + 4));
    }

    public void WriteTo(byte[] buf, int offset)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset), Parent);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(offset + 4), Name);
    }

    public bool Equals(IParentedKey other) => other is DbFileKey k && k.Parent == Parent && k.Name == Name;

    public static DbFileKey New(uint parent, ulong name) => new() { Parent = parent, Name = name };
}