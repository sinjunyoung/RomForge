using System.Security.Cryptography;

namespace N3DS.Save.Interfaces;

public interface ISigner
{
    byte[] Block(byte[] data);

    byte[] Hash(byte[] data)
    {
        byte[] block = Block(data);

        return SHA256.HashData(block);
    }
}