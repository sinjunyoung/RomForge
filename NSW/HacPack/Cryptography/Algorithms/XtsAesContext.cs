using System.Security.Cryptography;

namespace NSW.HacPack.Cryptography.Algorithms;

internal sealed class XtsAesContext : IDisposable
{
    private readonly Aes _aes1;
    private readonly Aes _aes2;

    public ICryptoTransform Enc { get; }

    public ICryptoTransform Dec { get; }

    private readonly ICryptoTransform _tweakEnc;

    public XtsAesContext(byte[] key1, byte[] key2)
    {
        _aes2 = CreateEcb(key2);

        _tweakEnc = _aes2.CreateEncryptor();

        _aes1 = CreateEcb(key1);

        Enc = _aes1.CreateEncryptor();
        Dec = _aes1.CreateDecryptor();
    }

    public byte[] ComputeTweak(byte[] iv) => _tweakEnc.TransformFinalBlock(iv, 0, 16);

    private static Aes CreateEcb(byte[] key)
    {
        var aes = Aes.Create();

        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        return aes;
    }

    public void Dispose()
    {
        _tweakEnc.Dispose();
        Enc.Dispose();
        Dec.Dispose();
        _aes1.Dispose();
        _aes2.Dispose();
    }
}