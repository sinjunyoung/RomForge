using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save;

public class DbSigner(uint id) : ISigner
{
    public byte[] Block(byte[] data)
    {
        var prefix = new byte[12];

        "CTR-9DB0"u8.CopyTo(prefix);

        BinaryPrimitives.WriteUInt32LittleEndian(prefix.AsSpan(8), id);

        var result = new byte[prefix.Length + data.Length];

        prefix.CopyTo(result, 0);
        data.CopyTo(result, prefix.Length);

        return result;
    }
}