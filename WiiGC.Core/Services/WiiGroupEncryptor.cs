using System.Security.Cryptography;
using WiiGC.Core.Models;
using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Services;

internal sealed class WiiGroupEncryptor : IDisposable
{
    private const int IvOffset = 0x3D0;
    private const int IvSize = 16;

    private static readonly byte[] ZeroIv = new byte[IvSize];

    private readonly byte[] _hashes = new byte[WiiLayout.GroupHeaderSize];
    private readonly Aes _aes = Aes.Create();
    private byte[]? _key;

    public ReadOnlySpan<byte> H2Table => _hashes.AsSpan(WiiLayout.H2Offset, WiiLayout.H2Bytes);

    public void Encrypt(byte[] key, byte[] decrypted, IReadOnlyList<HashException> exceptions, byte[] output)
    {
        if (decrypted.Length != WiiLayout.GroupDataSize || output.Length != WiiLayout.GroupTotalSize)
            throw new ArgumentException("Wii 그룹 버퍼 크기가 올바르지 않습니다.");

        if (_key == null || !_key.AsSpan().SequenceEqual(key))
        {
            _aes.Key = key;
            _key = (byte[])key.Clone();
        }

        WiiHashTree.ComputeHashes(decrypted, _hashes);
        ApplyExceptions(exceptions);
        EncryptBlocks(decrypted, output);
    }

    private void ApplyExceptions(IReadOnlyList<HashException> exceptions)
    {
        for (int i = 0; i < exceptions.Count; i++)
        {
            var exception = exceptions[i];
            int block = exception.Offset / WiiLayout.BlockHeaderSize;
            int offsetInBlock = exception.Offset % WiiLayout.BlockHeaderSize;

            if (block >= WiiLayout.BlocksPerGroup || offsetInBlock + WiiLayout.HashSize > WiiLayout.BlockHeaderSize)
                throw new InvalidDataException("RVZ 해시 예외 오프셋이 올바르지 않습니다.");

            exception.Hash.CopyTo(_hashes, block * WiiLayout.BlockHeaderSize + offsetInBlock);
        }
    }

    private void EncryptBlocks(byte[] decrypted, byte[] output)
    {
        for (int i = 0; i < WiiLayout.BlocksPerGroup; i++)
        {
            var block = output.AsSpan(i * WiiLayout.BlockTotalSize, WiiLayout.BlockTotalSize);

            _aes.EncryptCbc(_hashes.AsSpan(i * WiiLayout.BlockHeaderSize, WiiLayout.BlockHeaderSize), ZeroIv, block[..WiiLayout.BlockHeaderSize], PaddingMode.None);
            _aes.EncryptCbc(decrypted.AsSpan(i * WiiLayout.BlockDataSize, WiiLayout.BlockDataSize), block.Slice(IvOffset, IvSize), block[WiiLayout.BlockHeaderSize..], PaddingMode.None);
        }
    }

    public void Dispose() => _aes.Dispose();
}