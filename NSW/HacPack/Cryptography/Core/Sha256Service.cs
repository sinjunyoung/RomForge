using System.IO;
using System.Security.Cryptography;

namespace NSW.HacPack.Cryptography.Core;

public static class Sha256Service
{
    public static byte[] CalculateHash(Stream stream)
    {
        using var sha = SHA256.Create();
        const int bufSize = 0x61A8000;
        byte[] buf = new byte[bufSize];
        int read;

        while ((read = stream.Read(buf, 0, bufSize)) > 0)
            sha.TransformBlock(buf, 0, read, null, 0);

        sha.TransformFinalBlock([], 0, 0);

        return sha.Hash!;
    }
}