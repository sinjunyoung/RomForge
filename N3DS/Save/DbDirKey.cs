using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save;

public class DbDirKey : IParentedKey
{
    public const int KeySize = 4;

    public int KeyByteLen => KeySize;

    public uint Parent;

    public uint GetParent() => Parent;

    public void ReadFrom(byte[] buf, int offset) => Parent = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(offset));

    public void WriteTo(byte[] buf, int offset) => BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset), Parent);

    public bool Equals(IParentedKey other) => other is DbDirKey k && k.Parent == Parent;
}