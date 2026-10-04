using System.Security.Cryptography;
using DolphinTool.Core.Models;

namespace DolphinTool.Core.Services.Wii;

internal sealed class WiiPartitionEncoder : IDisposable
{
    private readonly WiiGroupEncryptor _encryptor = new();
    private readonly byte[] _decrypted = new byte[WiiLayout.GroupDataSize];
    private readonly byte[] _encrypted = new byte[WiiLayout.GroupTotalSize];

    public static long GetGroupCount(IWiiPartitionData data) => (data.Length + WiiLayout.GroupDataSize - 1) / WiiLayout.GroupDataSize;

    public ReadOnlySpan<byte> Encrypted => _encrypted;

    public int EncodeGroup(byte[] key, IWiiPartitionData data, long group, Span<byte> h3Entry)
    {
        long start = group * WiiLayout.GroupDataSize;
        int dataBytes = (int)Math.Min(WiiLayout.GroupDataSize, data.Length - start);

        data.Read(start, _decrypted.AsSpan(0, dataBytes));
        _decrypted.AsSpan(dataBytes).Clear();
        _encryptor.Encrypt(key, _decrypted, Array.Empty<HashException>(), _encrypted);
        SHA1.HashData(_encryptor.H2Table, h3Entry);

        return dataBytes / WiiLayout.BlockDataSize * WiiLayout.BlockTotalSize;
    }

    public void Dispose() => _encryptor.Dispose();
}