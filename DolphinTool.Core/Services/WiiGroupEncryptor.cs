using DolphinTool.Core.Models;
using System.Security.Cryptography;

namespace DolphinTool.Core.Services;

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

        ComputeHashes(decrypted);
        ApplyExceptions(exceptions);
        EncryptBlocks(decrypted, output);
    }

    private void ComputeHashes(byte[] decrypted)
    {
        byte[] hashes = _hashes;

        Array.Clear(hashes);

        for (int i = 0; i < WiiLayout.BlocksPerGroup; i++)
        {
            var block = decrypted.AsSpan(i * WiiLayout.BlockDataSize, WiiLayout.BlockDataSize);
            var header = hashes.AsSpan(i * WiiLayout.BlockHeaderSize, WiiLayout.BlockHeaderSize);

            for (int j = 0; j < WiiLayout.H0Count; j++)
                SHA1.HashData(block.Slice(j * 0x400, 0x400), header.Slice(j * WiiLayout.HashSize, WiiLayout.HashSize));
        }

        for (int sub = 0; sub < 8; sub++)
        {
            int firstBlock = sub * 8;
            var h1 = hashes.AsSpan(firstBlock * WiiLayout.BlockHeaderSize + WiiLayout.H1Offset, WiiLayout.H1Bytes);

            for (int k = 0; k < 8; k++)
                SHA1.HashData(hashes.AsSpan((firstBlock + k) * WiiLayout.BlockHeaderSize, WiiLayout.H0Bytes), h1.Slice(k * WiiLayout.HashSize, WiiLayout.HashSize));

            for (int k = 1; k < 8; k++)
                h1.CopyTo(hashes.AsSpan((firstBlock + k) * WiiLayout.BlockHeaderSize + WiiLayout.H1Offset, WiiLayout.H1Bytes));

            SHA1.HashData(h1, hashes.AsSpan(WiiLayout.H2Offset + sub * WiiLayout.HashSize, WiiLayout.HashSize));
        }

        var h2 = hashes.AsSpan(WiiLayout.H2Offset, WiiLayout.H2Bytes);

        for (int i = 1; i < WiiLayout.BlocksPerGroup; i++)
            h2.CopyTo(hashes.AsSpan(i * WiiLayout.BlockHeaderSize + WiiLayout.H2Offset, WiiLayout.H2Bytes));
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