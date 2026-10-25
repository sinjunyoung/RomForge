using N3DS.Core.Crypto;
using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class SignedFile : IRandomAccessFile
{
    private readonly IRandomAccessFile _signature;
    private readonly IRandomAccessFile _data;
    private readonly ISigner _signer;
    private readonly byte[] _key;
    private readonly int _len;

    public int Length => _len;

    public static SignedFile NewUnverified(IRandomAccessFile signature, IRandomAccessFile data, ISigner signer, byte[] key)
    {
        if (signature.Length != 16)
            throw new InvalidOperationException("SignedFile: signature must be 16 bytes");

        return new SignedFile(signature, data, signer, key);
    }

    public static SignedFile New(IRandomAccessFile signature, IRandomAccessFile data, ISigner signer, byte[] key)
    {
        if (signature.Length != 16)
            throw new InvalidOperationException("SignedFile: signature must be 16 bytes");

        var file = new SignedFile(signature, data, signer, key);
        var storedSig = new byte[16];

        file._signature.Read(0, storedSig, 0, 16);

        byte[] calculated = file.CalculateSignature();

        if (!storedSig.AsSpan().SequenceEqual(calculated))
            throw new InvalidDataException("SignedFile: signature mismatch");

        return file;
    }

    private SignedFile(IRandomAccessFile signature, IRandomAccessFile data, ISigner signer, byte[] key)
    {
        _signature = signature;
        _data = data;
        _signer = signer;
        _key = key;
        _len = data.Length;
    }

    private byte[] CalculateSignature()
    {
        var data = new byte[_len];

        _data.Read(0, data, 0, _len);

        byte[] hash = _signer.Hash(data);

        return SdCrypto.AesCmac(_key, hash);
    }

    public void Read(int pos, byte[] buf, int offset, int count) => _data.Read(pos, buf, offset, count);

    public void Write(int pos, byte[] buf, int offset, int count) => _data.Write(pos, buf, offset, count);

    public void Commit()
    {
        byte[] sig = CalculateSignature();
        _signature.Write(0, sig, 0, 16);
    }
}