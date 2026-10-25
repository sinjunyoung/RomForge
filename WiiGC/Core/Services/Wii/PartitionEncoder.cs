using System.Security.Cryptography;
using WiiGC.Core.Interfaces;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal sealed class PartitionEncoder : IDisposable
{
    private readonly GroupEncryptor _encryptor = new();
    private readonly byte[] _decrypted = new byte[Layout.GroupDataSize];
    private readonly byte[] _encrypted = new byte[Layout.GroupTotalSize];

    public static long GetGroupCount(IWiiPartitionData data) => (data.Length + Layout.GroupDataSize - 1) / Layout.GroupDataSize;

    public ReadOnlySpan<byte> Encrypted => _encrypted;

    public int EncodeGroup(byte[] key, IWiiPartitionData data, long group, Span<byte> h3Entry)
    {
        long start = group * Layout.GroupDataSize;
        int dataBytes = (int)Math.Min(Layout.GroupDataSize, data.Length - start);

        data.Read(start, _decrypted.AsSpan(0, dataBytes));
        _decrypted.AsSpan(dataBytes).Clear();
        _encryptor.Encrypt(key, _decrypted, [], _encrypted);
        SHA1.HashData(_encryptor.H2Table, h3Entry);

        return dataBytes / Layout.BlockDataSize * Layout.BlockTotalSize;
    }

    public void Dispose() => _encryptor.Dispose();
}