using System.Security.Cryptography;
using WiiGC.Core.Models;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal sealed class GroupEncryptor : IDisposable
{
    private const int IvSize = 16;

    private static readonly byte[] ZeroIv = new byte[IvSize];

    private readonly byte[] _hashes = new byte[Layout.GroupHeaderSize];
    private readonly Aes _aes = Aes.Create();
    private byte[]? _key;

    public ReadOnlySpan<byte> H2Table => _hashes.AsSpan(Layout.H2Offset, Layout.H2Bytes);

    public void Encrypt(byte[] key, byte[] decrypted, IReadOnlyList<HashException> exceptions, byte[] output)
    {
        if (decrypted.Length != Layout.GroupDataSize || output.Length != Layout.GroupTotalSize)
            throw new ArgumentException("Wii 그룹 버퍼 크기가 올바르지 않습니다.");

        if (_key == null || !_key.AsSpan().SequenceEqual(key))
        {
            _aes.Key = key;
            _key = (byte[])key.Clone();
        }

        HashTree.ComputeHashes(decrypted, _hashes);
        ApplyExceptions(exceptions);
        EncryptBlocks(decrypted, output);
    }

    private void ApplyExceptions(IReadOnlyList<HashException> exceptions)
    {
        for (int i = 0; i < exceptions.Count; i++)
        {
            var exception = exceptions[i];
            int block = exception.Offset / Layout.BlockHeaderSize;
            int offsetInBlock = exception.Offset % Layout.BlockHeaderSize;

            if (block >= Layout.BlocksPerGroup || offsetInBlock + Layout.HashSize > Layout.BlockHeaderSize)
                throw new InvalidDataException("RVZ 해시 예외 오프셋이 올바르지 않습니다.");

            exception.Hash.CopyTo(_hashes, block * Layout.BlockHeaderSize + offsetInBlock);
        }
    }

    private void EncryptBlocks(byte[] decrypted, byte[] output)
    {
        for (int i = 0; i < Layout.BlocksPerGroup; i++)
        {
            var block = output.AsSpan(i * Layout.BlockTotalSize, Layout.BlockTotalSize);

            _aes.EncryptCbc(_hashes.AsSpan(i * Layout.BlockHeaderSize, Layout.BlockHeaderSize), ZeroIv, block[..Layout.BlockHeaderSize], PaddingMode.None);
            _aes.EncryptCbc(decrypted.AsSpan(i * Layout.BlockDataSize, Layout.BlockDataSize), block.Slice(Layout.IvOffset, IvSize), block[Layout.BlockHeaderSize..], PaddingMode.None);
        }
    }

    public void Dispose() => _aes.Dispose();
}