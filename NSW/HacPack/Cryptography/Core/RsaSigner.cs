using System.IO;
using System.Security.Cryptography;

namespace NSW.HacPack.Cryptography.Core;

public static class RsaSigner
{
    public static void GeneratePssSignature(byte[] input, byte[] output, string rsaPrivateKeyPem)
    {
        using var rsa = RSA.Create();

        rsa.ImportFromPem(rsaPrivateKeyPem);

        byte[] sig = rsa.SignData(input, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

        Array.Copy(sig, output, Math.Min(sig.Length, output.Length));
    }

    public static void SignDataWithKeyFile(byte[] input, byte[] output, string keyPath)
    {
        string pem;

        try 
        { 
            pem = File.ReadAllText(keyPath);
        }
        catch 
        { 
            throw new IOException($"Private key is invalid: {keyPath}");
        }

        GeneratePssSignature(input, output, pem);
    }
}